using Vantage.Core.Configuration;
using Vantage.Core.Models;
using Vantage.Core.Secrets;
using Vantage.Core.Tests.Fakes;
using Xunit;

namespace Vantage.Core.Tests.Configuration;

public class KeyringConfigStoreTests : IDisposable
{
    private readonly string tempDir = Path.Combine(Path.GetTempPath(), "vantage-keyring-" + Guid.NewGuid().ToString("N"));
    private readonly FakeSecretStore secrets = new();

    private string ConfigPath => Path.Combine(tempDir, "config.json");

    private KeyringConfigStore Store() => new(new JsonConfigStore(ConfigPath), secrets);

    private static HostConfig PasswordHost(string password = "hunter2") => new()
    {
        Id = "edge",
        Name = "edge-01",
        Address = "192.0.2.11",
        Username = "ops",
        AuthMethod = SshAuthMethod.Password,
        Password = password
    };

    private static HostConfig KeyHost(string? passphrase) => new()
    {
        Id = "nas",
        Name = "nas-01",
        Address = "192.0.2.30",
        Username = "ops",
        AuthMethod = SshAuthMethod.PrivateKey,
        PrivateKeyPath = "~/.ssh/id_ed25519",
        PrivateKeyPassphrase = passphrase
    };

    private string Json() => File.ReadAllText(ConfigPath);

    [Fact]
    public async Task Save_MovesSecretsToTheKeyringAndKeepsOnlyReferences()
    {
        var config = new VantageConfig { Hosts = { PasswordHost(), KeyHost("open sesame") } };

        await Store().SaveAsync(config);

        Assert.DoesNotContain("hunter2", Json());
        Assert.DoesNotContain("open sesame", Json());
        Assert.Contains("\"passwordRef\": \"keyring:edge/password\"", Json());
        Assert.Contains("\"privateKeyPassphraseRef\": \"keyring:nas/passphrase\"", Json());
        Assert.Equal("hunter2", secrets.Secrets["keyring:edge/password"]);
        Assert.Equal("open sesame", secrets.Secrets["keyring:nas/passphrase"]);
        Assert.Equal("Vantage password for edge-01", secrets.Labels["keyring:edge/password"]);
        Assert.Equal("hunter2", config.Hosts[0].Password);
    }

    [Fact]
    public async Task Load_ResolvesReferencesFromTheKeyring()
    {
        await Store().SaveAsync(new VantageConfig { Hosts = { PasswordHost(), KeyHost("open sesame") } });

        var store = Store();
        var loaded = await store.LoadAsync();

        Assert.True(store.StoresSecretsInKeyring);
        Assert.Equal("hunter2", loaded.Hosts[0].Password);
        Assert.Equal("open sesame", loaded.Hosts[1].PrivateKeyPassphrase);
        Assert.Empty(loaded.LoadNotices);
    }

    [Fact]
    public async Task PlainSecretsFromAnOldConfig_AreMigratedOnFirstSave()
    {
        await new JsonConfigStore(ConfigPath).SaveAsync(new VantageConfig { Hosts = { PasswordHost() } });
        Assert.Contains("hunter2", Json());

        var store = Store();
        var loaded = await store.LoadAsync();
        Assert.Equal("hunter2", loaded.Hosts[0].Password);
        await store.SaveAsync(loaded);

        Assert.DoesNotContain("hunter2", Json());
        Assert.Equal("hunter2", secrets.Secrets["keyring:edge/password"]);
    }

    [Fact]
    public async Task WithoutKeyring_SecretsStayInPlainText()
    {
        secrets.Available = false;
        var store = Store();

        await store.SaveAsync(new VantageConfig { Hosts = { PasswordHost() } });
        var loaded = await store.LoadAsync();

        Assert.False(store.StoresSecretsInKeyring);
        Assert.Contains("\"password\": \"hunter2\"", Json());
        Assert.Equal("hunter2", loaded.Hosts[0].Password);
        Assert.Empty(secrets.Secrets);
    }

    [Fact]
    public async Task UnreachableKeyring_LoadsWithANoticeAndKeepsTheReference()
    {
        await Store().SaveAsync(new VantageConfig { Hosts = { PasswordHost() } });
        secrets.Available = false;

        var store = Store();
        var loaded = await store.LoadAsync();
        await store.SaveAsync(loaded);

        Assert.Null(loaded.Hosts[0].Password);
        Assert.Contains("edge-01", Assert.Single(loaded.LoadNotices));
        Assert.Contains("\"passwordRef\": \"keyring:edge/password\"", Json());
    }

    [Fact]
    public async Task MissingSecret_IsReportedAndItsReferenceSurvivesASave()
    {
        await Store().SaveAsync(new VantageConfig { Hosts = { PasswordHost() } });
        secrets.Secrets.Clear();

        var store = Store();
        var loaded = await store.LoadAsync();
        await store.SaveAsync(loaded);

        Assert.Contains("no saved password for edge-01", Assert.Single(loaded.LoadNotices));
        Assert.Contains("\"passwordRef\": \"keyring:edge/password\"", Json());
        Assert.Empty(secrets.Deleted);
    }

    [Fact]
    public async Task RemovedHostsAndUnusedSecrets_AreDeletedFromTheKeyring()
    {
        await Store().SaveAsync(new VantageConfig { Hosts = { PasswordHost(), KeyHost("open sesame") } });

        var store = Store();
        var loaded = await store.LoadAsync();
        var switched = KeyHost(null);
        switched.Id = "edge";
        await store.SaveAsync(new VantageConfig { Hosts = { switched } });

        Assert.Equal(new[] { "keyring:edge/password", "keyring:nas/passphrase" }, secrets.Deleted.OrderBy(r => r));
        Assert.Empty(secrets.Secrets);
        Assert.DoesNotContain("Ref", Json());
        Assert.Equal(2, loaded.Hosts.Count);
    }

    [Fact]
    public async Task KeyringWriteFailure_LeavesConfigUntouched()
    {
        await Store().SaveAsync(new VantageConfig { Hosts = { KeyHost(null) } });
        var before = Json();
        secrets.FailWrites = true;

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            Store().SaveAsync(new VantageConfig { Hosts = { PasswordHost() } }));

        Assert.Equal("The keyring prompt was dismissed.", error.Message);
        Assert.Equal(before, Json());
    }

    [Fact]
    public async Task UnavailableSecretStore_ReportsNoKeyring()
    {
        Assert.False(await new UnavailableSecretStore().IsAvailableAsync());
        Assert.IsType<UnavailableSecretStore>(SecretStores.ForCurrentPlatform(null));
    }

    public void Dispose()
    {
        if (Directory.Exists(tempDir))
            Directory.Delete(tempDir, recursive: true);
    }
}
