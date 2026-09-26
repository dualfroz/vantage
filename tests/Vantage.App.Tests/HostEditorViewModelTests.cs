using Vantage.App.Converters;
using Vantage.App.Tests.Fakes;
using Vantage.App.ViewModels;
using Vantage.Core.Hosts;
using Vantage.Core.Models;
using Vantage.Core.Ssh;
using Xunit;

namespace Vantage.App.Tests;

public class HostEditorViewModelTests
{
    private sealed class Harness
    {
        public List<HostConfig> Saved { get; } = new();
        public List<string> Removed { get; } = new();

        public HostEditorViewModel Build(IHostConnectionTester? tester = null, IHostKeyStore? hostKeys = null) =>
            new(tester ?? new MockHostConnectionTester(),
                host => { Saved.Add(host); return Task.CompletedTask; },
                id => { Removed.Add(id); return Task.CompletedTask; },
                hostKeys);
    }

    private static readonly HostKeyCandidate UnknownKey =
        HostKeyCandidate.From("192.0.2.11", 22, Convert.FromBase64String("AAAAC3NzaC1lZDI1NTE5AAAAIBK2uZ3mWIj+F5aNjoRzq111oT6F3WKpzIeGmlD8yLXI"));

    private static ConnectionTestResult UnknownHostKeyResult() => new(false, false, new[]
    {
        new ConnectionTestStep("Host key", false, "The host key of 192.0.2.11:22 is not known.", null)
    }, UnknownKey);

    private static ConnectionTestResult WorkingResult() => new(true, false, new[]
    {
        new ConnectionTestStep("SSH", true, "Linux 6.8.0 x86_64", 12)
    });

    private static HostConfig Existing() => new()
    {
        Id = "edge",
        Name = "edge-01",
        Address = "192.0.2.11",
        Username = "ops",
        AuthMethod = SshAuthMethod.PrivateKey,
        PrivateKeyPath = "~/.ssh/id_ed25519"
    };

    [Fact]
    public void Load_SelectsFirstHostAndStartsClean()
    {
        var editor = new Harness().Build();

        editor.Load(new[] { Existing() });

        var draft = Assert.Single(editor.Drafts);
        Assert.Same(draft, editor.SelectedDraft);
        Assert.False(draft.IsDirty);
        Assert.Equal("ops@192.0.2.11:22", draft.EndpointText);
    }

    [Fact]
    public void EditingAField_MarksDraftDirtyAndRevertRestores()
    {
        var editor = new Harness().Build();
        editor.Load(new[] { Existing() });
        var draft = editor.SelectedDraft!;

        draft.Port = 2222;
        Assert.True(draft.IsDirty);

        editor.DiscardCommand.Execute(null);

        Assert.False(draft.IsDirty);
        Assert.Equal(22m, draft.Port);
    }

    [Fact]
    public async Task Save_InvalidDraft_ShowsErrorsAndDoesNotPersist()
    {
        var harness = new Harness();
        var editor = harness.Build();

        editor.AddHostCommand.Execute(null);
        await editor.SaveCommand.ExecuteAsync(null);

        Assert.Empty(harness.Saved);
        Assert.True(editor.HasValidationErrors);
        Assert.Contains("Address is required.", editor.ValidationErrors);
        Assert.Contains("Username is required.", editor.ValidationErrors);
    }

    [Fact]
    public async Task Save_NewHost_PersistsAndClearsDirtyFlag()
    {
        var harness = new Harness();
        var editor = harness.Build();
        editor.AddHostCommand.Execute(null);
        var draft = editor.SelectedDraft!;
        draft.Name = "storage-01";
        draft.Address = "198.51.100.21";
        draft.Username = "backup";
        draft.UseSudo = true;

        await editor.SaveCommand.ExecuteAsync(null);

        var saved = Assert.Single(harness.Saved);
        Assert.Equal("storage-01", saved.Name);
        Assert.Equal("198.51.100.21", saved.Address);
        Assert.True(saved.UseSudo);
        Assert.Equal(draft.Id, saved.Id);
        Assert.False(draft.IsNew);
        Assert.False(draft.IsDirty);
        Assert.Equal("Saved storage-01.", editor.StatusMessage);
    }

    [Fact]
    public async Task SwitchingToPassword_DropsKeyFieldsFromSavedConfig()
    {
        var harness = new Harness();
        var editor = harness.Build();
        editor.Load(new[] { Existing() });
        var draft = editor.SelectedDraft!;

        draft.AuthMethodIndex = 1;
        Assert.True(draft.UsesPassword);
        Assert.False(draft.UsesPrivateKey);
        draft.Password = "s3cret";
        await editor.SaveCommand.ExecuteAsync(null);

        var saved = Assert.Single(harness.Saved);
        Assert.Equal(SshAuthMethod.Password, saved.AuthMethod);
        Assert.Equal("s3cret", saved.Password);
        Assert.Null(saved.PrivateKeyPath);
    }

    [Fact]
    public void Discard_OnNewHost_RemovesTheDraft()
    {
        var editor = new Harness().Build();
        editor.Load(new[] { Existing() });

        editor.AddHostCommand.Execute(null);
        Assert.Equal(2, editor.Drafts.Count);
        editor.DiscardCommand.Execute(null);

        Assert.Single(editor.Drafts);
        Assert.Equal("edge", editor.SelectedDraft!.Id);
    }

    [Fact]
    public async Task Remove_RequiresConfirmation()
    {
        var harness = new Harness();
        var editor = harness.Build();
        editor.Load(new[] { Existing() });

        editor.RequestRemoveCommand.Execute(null);
        Assert.True(editor.IsConfirmingRemove);
        Assert.Empty(harness.Removed);

        await editor.ConfirmRemoveCommand.ExecuteAsync(null);

        Assert.Equal(new[] { "edge" }, harness.Removed);
        Assert.Empty(editor.Drafts);
        Assert.True(editor.HasNoHosts);
        Assert.False(editor.HasSelection);
    }

    [Fact]
    public void CancelRemove_KeepsHost()
    {
        var harness = new Harness();
        var editor = harness.Build();
        editor.Load(new[] { Existing() });

        editor.RequestRemoveCommand.Execute(null);
        editor.CancelRemoveCommand.Execute(null);

        Assert.False(editor.IsConfirmingRemove);
        Assert.Single(editor.Drafts);
        Assert.Empty(harness.Removed);
    }

    [Fact]
    public async Task TestConnection_InMockMode_SaysTheConnectionWasSimulated()
    {
        var editor = new Harness().Build();
        editor.Load(new[] { Existing() });

        await editor.TestConnectionCommand.ExecuteAsync(null);

        Assert.True(editor.HasTestResult);
        Assert.Equal("Settings valid, connection simulated", editor.TestSummary);
        Assert.Equal(StatusTone.Warn, editor.TestTone);
        Assert.Equal(MockHostConnectionTester.SimulatedDetail, editor.TestSteps.Single().Detail);
    }

    [Fact]
    public async Task TestConnection_UsesUnsavedValuesAndReportsFailure()
    {
        var tester = new FakeConnectionTester(new ConnectionTestResult(false, false, new[]
        {
            new ConnectionTestStep("SSH", false, "Permission denied (publickey).", null)
        }));
        var editor = new Harness().Build(tester);
        editor.Load(new[] { Existing() });
        editor.SelectedDraft!.Port = 2200;

        await editor.TestConnectionCommand.ExecuteAsync(null);

        Assert.Equal(2200, tester.Tested.Single().Port);
        Assert.Equal("Connection failed", editor.TestSummary);
        Assert.Equal(StatusTone.Down, editor.TestTone);
        Assert.Equal(StatusTone.Down, editor.TestSteps.Single().Tone);
    }

    [Fact]
    public async Task TestConnection_UnknownHostKey_OffersTrustWithTheFingerprint()
    {
        var editor = new Harness().Build(new SequenceConnectionTester(UnknownHostKeyResult()), new FakeHostKeyStore());
        editor.Load(new[] { Existing() });

        await editor.TestConnectionCommand.ExecuteAsync(null);

        Assert.True(editor.HasUnknownHostKey);
        Assert.Equal($"192.0.2.11:22 presented ssh-ed25519 {UnknownKey.Fingerprint}", editor.UnknownHostKeyText);
        Assert.Equal("Connection failed", editor.TestSummary);
    }

    [Fact]
    public async Task TrustHostKey_SavesTheKeyAndTestsAgain()
    {
        var hostKeys = new FakeHostKeyStore();
        var tester = new SequenceConnectionTester(UnknownHostKeyResult(), WorkingResult());
        var editor = new Harness().Build(tester, hostKeys);
        editor.Load(new[] { Existing() });
        await editor.TestConnectionCommand.ExecuteAsync(null);

        await editor.TrustHostKeyCommand.ExecuteAsync(null);

        Assert.Same(UnknownKey, Assert.Single(hostKeys.Trusted));
        Assert.Equal(2, tester.Tested.Count);
        Assert.False(editor.HasUnknownHostKey);
        Assert.Equal("Connection works", editor.TestSummary);
        Assert.Equal($"Trusted ssh-ed25519 {UnknownKey.Fingerprint} for 192.0.2.11:22 and saved it to /tmp/vantage/known_hosts.", editor.StatusMessage);
    }

    [Fact]
    public async Task UnknownHostKey_IsForgottenWhenAnotherHostIsSelected()
    {
        var editor = new Harness().Build(new SequenceConnectionTester(UnknownHostKeyResult()), new FakeHostKeyStore());
        var other = Existing();
        other.Id = "other";
        editor.Load(new[] { Existing(), other });
        await editor.TestConnectionCommand.ExecuteAsync(null);

        editor.SelectedDraft = editor.Drafts[1];

        Assert.False(editor.HasUnknownHostKey);
        Assert.Null(editor.UnknownHostKey);
    }

    [Fact]
    public async Task UnknownHostKey_IsNotOfferedWithoutAHostKeyStore()
    {
        var editor = new Harness().Build(new SequenceConnectionTester(UnknownHostKeyResult()));
        editor.Load(new[] { Existing() });

        await editor.TestConnectionCommand.ExecuteAsync(null);

        Assert.False(editor.HasUnknownHostKey);
    }

    [Fact]
    public void SecretStorageNote_DependsOnTheKeyring()
    {
        var editor = new Harness().Build();
        Assert.Contains("plain text in config.json", editor.SecretStorageNote);

        editor.SecretsInKeyring = true;

        Assert.Contains("desktop keyring", editor.SecretStorageNote);
        Assert.DoesNotContain("plain text", editor.SecretStorageNote);
    }

    [Fact]
    public async Task TestConnection_InvalidDraft_DoesNotCallTester()
    {
        var tester = new FakeConnectionTester(new ConnectionTestResult(true, false, Array.Empty<ConnectionTestStep>()));
        var editor = new Harness().Build(tester);
        editor.AddHostCommand.Execute(null);

        await editor.TestConnectionCommand.ExecuteAsync(null);

        Assert.Empty(tester.Tested);
        Assert.True(editor.HasValidationErrors);
    }
}
