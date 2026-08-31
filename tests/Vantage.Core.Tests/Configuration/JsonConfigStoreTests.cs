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
            Hosts = { new HostConfig { Name = "edge", Address = "10.0.0.1", Username = "root", AuthMethod = SshAuthMethod.PrivateKey } },
            HealthChecks = { new HealthCheckConfig { Name = "web", Kind = HealthKind.Http, Target = "https://x" } }
        };

        await store.SaveAsync(original);
        var loaded = await store.LoadAsync();

        Assert.Equal(9, loaded.RefreshIntervalSeconds);
        Assert.Single(loaded.Hosts);
        Assert.Equal("edge", loaded.Hosts[0].Name);
        Assert.Equal(SshAuthMethod.PrivateKey, loaded.Hosts[0].AuthMethod);
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

    public void Dispose()
    {
        if (Directory.Exists(tempDir))
            Directory.Delete(tempDir, recursive: true);
    }
}
