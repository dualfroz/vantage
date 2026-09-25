using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Vantage.App.Converters;
using Vantage.Core.Hosts;
using Vantage.Core.Models;
using Vantage.Core.Ssh;

namespace Vantage.App.ViewModels;

public partial class HostEditorViewModel : ViewModelBase
{
    private readonly IHostConnectionTester connectionTester;
    private readonly Func<HostConfig, Task> saveHost;
    private readonly Func<string, Task> removeHost;
    private readonly IHostKeyStore? hostKeys;

    [ObservableProperty] private HostDraftViewModel? selectedDraft;
    [ObservableProperty] private bool isBusy;
    [ObservableProperty] private bool isTesting;
    [ObservableProperty] private bool isConfirmingRemove;
    [ObservableProperty] private string? statusMessage;
    [ObservableProperty] private string? lastError;
    [ObservableProperty] private string? testSummary;
    [ObservableProperty] private StatusTone testTone = StatusTone.Neutral;
    [ObservableProperty] private HostKeyCandidate? unknownHostKey;
    [ObservableProperty] private bool secretsInKeyring;

    public HostEditorViewModel(
        IHostConnectionTester connectionTester,
        Func<HostConfig, Task> saveHost,
        Func<string, Task> removeHost,
        IHostKeyStore? hostKeys = null)
    {
        this.connectionTester = connectionTester;
        this.saveHost = saveHost;
        this.removeHost = removeHost;
        this.hostKeys = hostKeys;
    }

    public ObservableCollection<HostDraftViewModel> Drafts { get; } = new();

    public ObservableCollection<string> ValidationErrors { get; } = new();

    public ObservableCollection<ConnectionStepViewModel> TestSteps { get; } = new();

    public bool HasSelection => SelectedDraft is not null;

    public bool HasNoHosts => Drafts.Count == 0;

    public bool HasValidationErrors => ValidationErrors.Count > 0;

    public bool HasTestResult => TestSteps.Count > 0;

    public string RemoveConfirmText => SelectedDraft is null
        ? ""
        : $"Remove {SelectedDraft.DisplayName} from Vantage? Its saved history stays in the database.";

    public bool HasUnknownHostKey => UnknownHostKey is not null && hostKeys is not null;

    public string UnknownHostKeyText => UnknownHostKey is null
        ? ""
        : $"{UnknownHostKey.Endpoint} presented {UnknownHostKey.KeyType} {UnknownHostKey.Fingerprint}";

    public string SecretStorageNote => SecretsInKeyring
        ? "Passwords and key passphrases are kept in the desktop keyring; config.json only stores a reference to them."
        : "No desktop keyring is available, so passwords and key passphrases are saved in plain text in config.json.";

    public void Load(IEnumerable<HostConfig> hosts)
    {
        var selectedId = SelectedDraft?.Id;
        Drafts.Clear();
        foreach (var host in hosts)
            Drafts.Add(new HostDraftViewModel(host, isNew: false));

        SelectedDraft = Drafts.FirstOrDefault(d => d.Id == selectedId) ?? Drafts.FirstOrDefault();
        OnPropertyChanged(nameof(HasNoHosts));
    }

    [RelayCommand]
    private void AddHost()
    {
        var draft = new HostDraftViewModel(new HostConfig
        {
            Port = 22,
            AuthMethod = SshAuthMethod.PrivateKey,
            PrivateKeyPath = "~/.ssh/id_ed25519",
            DiskMount = "/"
        }, isNew: true);

        Drafts.Add(draft);
        SelectedDraft = draft;
        StatusMessage = null;
        OnPropertyChanged(nameof(HasNoHosts));
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        var draft = SelectedDraft;
        if (draft is null)
            return;

        var host = draft.ToConfig();
        if (!Validate(host))
            return;

        IsBusy = true;
        LastError = null;
        StatusMessage = null;
        try
        {
            await saveHost(host);
            draft.MarkSaved(host);
            StatusMessage = $"Saved {draft.DisplayName}.";
        }
        catch (Exception ex)
        {
            LastError = "Could not save: " + ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void Discard()
    {
        var draft = SelectedDraft;
        if (draft is null)
            return;

        if (draft.IsNew)
        {
            Drafts.Remove(draft);
            SelectedDraft = Drafts.FirstOrDefault();
            OnPropertyChanged(nameof(HasNoHosts));
        }
        else
        {
            draft.Revert();
        }

        ClearFeedback();
    }

    [RelayCommand]
    private void RequestRemove()
    {
        if (SelectedDraft is null)
            return;

        if (SelectedDraft.IsNew)
        {
            Discard();
            return;
        }

        IsConfirmingRemove = true;
    }

    [RelayCommand]
    private void CancelRemove() => IsConfirmingRemove = false;

    [RelayCommand]
    private async Task ConfirmRemoveAsync()
    {
        var draft = SelectedDraft;
        IsConfirmingRemove = false;
        if (draft is null)
            return;

        IsBusy = true;
        LastError = null;
        try
        {
            await removeHost(draft.Id);
            Drafts.Remove(draft);
            SelectedDraft = Drafts.FirstOrDefault();
            StatusMessage = $"Removed {draft.DisplayName}.";
            OnPropertyChanged(nameof(HasNoHosts));
        }
        catch (Exception ex)
        {
            LastError = "Could not remove: " + ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task TestConnectionAsync(CancellationToken cancellationToken)
    {
        var draft = SelectedDraft;
        if (draft is null)
            return;

        TestSteps.Clear();
        TestSummary = null;
        TestTone = StatusTone.Neutral;
        UnknownHostKey = null;
        OnPropertyChanged(nameof(HasTestResult));

        var host = draft.ToConfig();
        if (!Validate(host))
            return;

        IsTesting = true;
        try
        {
            var result = await connectionTester.TestAsync(host, cancellationToken);
            if (!ReferenceEquals(draft, SelectedDraft))
                return;

            foreach (var step in result.Steps)
                TestSteps.Add(new ConnectionStepViewModel(step));
            UnknownHostKey = result.UnknownHostKey;

            (TestSummary, TestTone) = result switch
            {
                { Succeeded: true, Simulated: true } => ("Settings valid, connection simulated", StatusTone.Warn),
                { Succeeded: true } => ("Connection works", StatusTone.Ok),
                _ => ("Connection failed", StatusTone.Down)
            };
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            TestSteps.Add(new ConnectionStepViewModel(new ConnectionTestStep("Test", false, ex.Message, null)));
            TestSummary = "Connection failed";
            TestTone = StatusTone.Down;
        }
        finally
        {
            IsTesting = false;
            OnPropertyChanged(nameof(HasTestResult));
        }
    }

    [RelayCommand]
    private async Task TrustHostKeyAsync(CancellationToken cancellationToken)
    {
        var key = UnknownHostKey;
        if (key is null || hostKeys is null)
            return;

        try
        {
            hostKeys.Trust(key);
        }
        catch (Exception ex)
        {
            LastError = "Could not trust the host key: " + ex.Message;
            return;
        }

        UnknownHostKey = null;
        StatusMessage = $"Trusted {key.KeyType} {key.Fingerprint} for {key.Endpoint} and saved it to {hostKeys.TrustedKeysPath}.";
        await TestConnectionAsync(cancellationToken);
    }

    partial void OnUnknownHostKeyChanged(HostKeyCandidate? value)
    {
        OnPropertyChanged(nameof(HasUnknownHostKey));
        OnPropertyChanged(nameof(UnknownHostKeyText));
    }

    partial void OnSecretsInKeyringChanged(bool value) => OnPropertyChanged(nameof(SecretStorageNote));

    partial void OnSelectedDraftChanged(HostDraftViewModel? value)
    {
        IsConfirmingRemove = false;
        ClearFeedback();
        OnPropertyChanged(nameof(HasSelection));
        OnPropertyChanged(nameof(RemoveConfirmText));
    }

    private bool Validate(HostConfig host)
    {
        ValidationErrors.Clear();
        foreach (var error in HostConfigValidator.Validate(host))
            ValidationErrors.Add(error);
        OnPropertyChanged(nameof(HasValidationErrors));
        return ValidationErrors.Count == 0;
    }

    private void ClearFeedback()
    {
        ValidationErrors.Clear();
        TestSteps.Clear();
        TestSummary = null;
        TestTone = StatusTone.Neutral;
        UnknownHostKey = null;
        LastError = null;
        OnPropertyChanged(nameof(HasValidationErrors));
        OnPropertyChanged(nameof(HasTestResult));
    }
}

public sealed class ConnectionStepViewModel : ViewModelBase
{
    public ConnectionStepViewModel(ConnectionTestStep step)
    {
        Step = step;
    }

    public ConnectionTestStep Step { get; }

    public string Name => Step.Name;
    public string Detail => Step.Detail;
    public bool Succeeded => Step.Succeeded;
    public StatusTone Tone => Step.Succeeded ? StatusTone.Ok : StatusTone.Down;
    public string LatencyText => Step.LatencyMs is { } ms ? ms.ToString("0", CultureInfo.InvariantCulture) + " ms" : "";
}
