using Vantage.Core.Configuration;
using Vantage.Core.Models;
using Xunit;

namespace Vantage.Core.Tests.Configuration;

public class JsonConfigStoreTests : IDisposable
{
    private readonly string tempDir = Path.Combine(Path.GetTempPath(), "vantage-tests-" + Guid.NewGuid().ToString("N"));

    private string ConfigPath => Path.Combine(tempDir, "config.json");

    [Fact]
    public async Task LoadAsync_MissingFileReturnsEmptyConfig()
    {
        var store = new JsonConfigStore(ConfigPath);
        var config = await store.LoadAsync();

        Assert.Empty(config.Hosts);
        Assert.Empty(config.HealthChecks);
    }

    [Fact]
    public async Task SaveThenLoad_RoundTripsHostsAndChecks()
    {
        var store = new JsonConfigStore(ConfigPath);
        var original = new VantageConfig
        {
            RefreshIntervalSeconds = 9,
            Hosts = { new HostConfig { Name = "edge", Address = "192.0.2.1", Username = "root", AuthMethod = SshAuthMethod.PrivateKey, UseSudo = true } },
            HealthChecks = { new HealthCheckConfig { Name = "web", Kind = HealthKind.Http, Target = "https://x" } }
        };

        await store.SaveAsync(original);
        var loaded = await store.LoadAsync();

        Assert.Equal(9, loaded.RefreshIntervalSeconds);
        Assert.Single(loaded.Hosts);
        Assert.Equal("edge", loaded.Hosts[0].Name);
        Assert.Equal(SshAuthMethod.PrivateKey, loaded.Hosts[0].AuthMethod);
        Assert.True(loaded.Hosts[0].UseSudo);
        Assert.Equal(HealthKind.Http, loaded.HealthChecks[0].Kind);
    }

    [Fact]
    public async Task SaveAsync_WritesEnumsAsStrings()
    {
        var store = new JsonConfigStore(ConfigPath);
        await store.SaveAsync(new VantageConfig
        {
            Hosts = { new HostConfig { AuthMethod = SshAuthMethod.PrivateKey } }
        });

        var json = await File.ReadAllTextAsync(ConfigPath);
        Assert.Contains("privateKey", json);
        Assert.DoesNotContain("\"authMethod\": 0", json);
    }

    private string Home => Path.Combine(tempDir, "home");

    private async Task WriteLegacyHostAsync(string extraFields = "")
    {
        Directory.CreateDirectory(tempDir);
        await File.WriteAllTextAsync(ConfigPath,
            "{ \"hosts\": [ { \"id\": \"nas\", \"name\": \"nas-01\", \"address\": \"192.0.2.30\", \"username\": \"ops\", " +
            "\"authMethod\": \"agent\"" + extraFields + " } ], \"refreshIntervalSeconds\": 7 }");
    }

    private void CreateHomeKey(string name)
    {
        Directory.CreateDirectory(Path.Combine(Home, ".ssh"));
        File.WriteAllText(Path.Combine(Home, ".ssh", name), "key");
    }

    [Fact]
    public async Task LoadAsync_AgentAuth_MapsToTheDefaultKeyWithANotice()
    {
        await WriteLegacyHostAsync();
        CreateHomeKey("id_rsa");
        CreateHomeKey("id_ed25519");

        var config = await new JsonConfigStore(ConfigPath, Home).LoadAsync();

        var host = Assert.Single(config.Hosts);
        Assert.Equal(SshAuthMethod.PrivateKey, host.AuthMethod);
        Assert.Equal("~/.ssh/id_ed25519", host.PrivateKeyPath);
        Assert.Equal(7, config.RefreshIntervalSeconds);
        Assert.True(config.MigratedOnLoad);
        var notice = Assert.Single(config.LoadNotices);
        Assert.Contains("nas-01 used SSH agent authentication", notice);
        Assert.Contains("~/.ssh/id_ed25519", notice);
    }

    [Fact]
    public async Task LoadAsync_AgentAuth_NoticeIsGoneOnceSaved()
    {
        await WriteLegacyHostAsync();
        CreateHomeKey("id_rsa");
        var store = new JsonConfigStore(ConfigPath, Home);

        await store.SaveAsync(await store.LoadAsync());
        var reloaded = await store.LoadAsync();

        Assert.Empty(reloaded.LoadNotices);
        Assert.False(reloaded.MigratedOnLoad);
        Assert.Equal("~/.ssh/id_rsa", reloaded.Hosts[0].PrivateKeyPath);
        Assert.Contains("\"authMethod\": \"privateKey\"", await File.ReadAllTextAsync(ConfigPath));
    }

    [Fact]
    public async Task LoadAsync_AgentAuth_WithoutDefaultKeyAsksForOne()
    {
        await WriteLegacyHostAsync();

        var config = await new JsonConfigStore(ConfigPath, Home).LoadAsync();

        Assert.Equal(SshAuthMethod.PrivateKey, config.Hosts[0].AuthMethod);
        Assert.Null(config.Hosts[0].PrivateKeyPath);
        Assert.Contains("No default key was found", Assert.Single(config.LoadNotices));
    }

    [Fact]
    public async Task LoadAsync_AgentAuth_KeepsAnExplicitKeyPath()
    {
        await WriteLegacyHostAsync(", \"privateKeyPath\": \"~/.ssh/nas\"");
        CreateHomeKey("id_ed25519");

        var config = await new JsonConfigStore(ConfigPath, Home).LoadAsync();

        Assert.Equal("~/.ssh/nas", config.Hosts[0].PrivateKeyPath);
        Assert.Contains("~/.ssh/nas", Assert.Single(config.LoadNotices));
    }

    [Fact]
    public async Task SaveAsync_OmitsEmptySecretFields()
    {
        var store = new JsonConfigStore(ConfigPath);
        await store.SaveAsync(new VantageConfig { Hosts = { new HostConfig { Name = "edge" } } });

        var json = await File.ReadAllTextAsync(ConfigPath);
        Assert.DoesNotContain("password", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("passphrase", json, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("\"desktopNotifications\": true", json);
    }

    public void Dispose()
    {
        if (Directory.Exists(tempDir))
            Directory.Delete(tempDir, recursive: true);
    }
}
