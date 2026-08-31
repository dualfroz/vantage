using Vantage.Core.Metrics;
using Vantage.Core.Models;
using Xunit;

namespace Vantage.Core.Tests.Metrics;

public class MockMetricsProviderTests
{
    [Fact]
    public async Task GetMetricsAsync_ProducesValuesInPlausibleRanges()
    {
        var provider = new MockMetricsProvider(() => DateTimeOffset.UnixEpoch.AddSeconds(500));
        var host = new HostConfig { Id = "host-a", Name = "a" };

        var metrics = await provider.GetMetricsAsync(host, CancellationToken.None);

        Assert.InRange(metrics.CpuUsagePercent, 0, 100);
        Assert.InRange(metrics.MemoryUsedPercent, 0, 100);
        Assert.True(metrics.MemoryTotalBytes > 0);
        Assert.True(metrics.MemoryUsedBytes <= metrics.MemoryTotalBytes);
        Assert.True(metrics.Uptime > TimeSpan.Zero);
    }

    [Fact]
    public async Task GetMetricsAsync_VariesOverTimeButKeepsTotalsStable()
    {
        var now = DateTimeOffset.UnixEpoch;
        var provider = new MockMetricsProvider(() => now);
        var host = new HostConfig { Id = "host-b" };

        now = DateTimeOffset.UnixEpoch.AddSeconds(1000);
        var first = await provider.GetMetricsAsync(host, CancellationToken.None);
        now = DateTimeOffset.UnixEpoch.AddSeconds(1012);
        var second = await provider.GetMetricsAsync(host, CancellationToken.None);

        Assert.NotEqual(first.CpuUsagePercent, second.CpuUsagePercent);
        Assert.Equal(first.MemoryTotalBytes, second.MemoryTotalBytes);
        Assert.Equal(first.DiskTotalBytes, second.DiskTotalBytes);
    }

    [Fact]
    public async Task GetMetricsAsync_IsDeterministicPerHostIdentity()
    {
        var at = DateTimeOffset.UnixEpoch.AddSeconds(2000);
        var providerOne = new MockMetricsProvider(() => at);
        var providerTwo = new MockMetricsProvider(() => at);

        var one = await providerOne.GetMetricsAsync(new HostConfig { Id = "same" }, CancellationToken.None);
        var two = await providerTwo.GetMetricsAsync(new HostConfig { Id = "same" }, CancellationToken.None);

        Assert.Equal(one.MemoryTotalBytes, two.MemoryTotalBytes);
        Assert.Equal(one.DiskTotalBytes, two.DiskTotalBytes);
    }
}
