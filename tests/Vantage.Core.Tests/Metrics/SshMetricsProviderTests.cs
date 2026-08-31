using Vantage.Core.Metrics;
using Vantage.Core.Models;
using Vantage.Core.Tests.Fakes;
using Vantage.Core.Tests.Fixtures;
using Xunit;

namespace Vantage.Core.Tests.Metrics;

public class SshMetricsProviderTests
{
    [Fact]
    public async Task GetMetricsAsync_ParsesBundleFromRunner()
    {
        var runner = new FakeSshCommandRunner(MetricsFixtures.FullBundle());
        var provider = new SshMetricsProvider(runner);

        var metrics = await provider.GetMetricsAsync(new HostConfig { DiskMount = "/srv" }, CancellationToken.None);

        Assert.Equal(80.0, metrics.CpuUsagePercent, 1);
        Assert.Equal(75.0, metrics.MemoryUsedPercent, 1);
    }

    [Fact]
    public async Task GetMetricsAsync_UsesConfiguredMountInCommand()
    {
        var runner = new FakeSshCommandRunner(MetricsFixtures.FullBundle());
        var provider = new SshMetricsProvider(runner);

        await provider.GetMetricsAsync(new HostConfig { DiskMount = "/srv/data" }, CancellationToken.None);

        Assert.Contains("df -B1 -P '/srv/data'", runner.Commands.Single());
    }
}
