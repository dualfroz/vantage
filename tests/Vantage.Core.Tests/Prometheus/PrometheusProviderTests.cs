using Vantage.Core.Models;
using Vantage.Core.Prometheus;
using Vantage.Core.Tests.Fakes;
using Vantage.Core.Tests.Fixtures;
using Xunit;

namespace Vantage.Core.Tests.Prometheus;

public class PrometheusProviderTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 26, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Mapper_MapsMemoryDiskLoadAndUptime()
    {
        var samples = PrometheusTextParser.Parse(PrometheusFixtures.NodeScrape1());
        var metrics = PrometheusNodeMapper.Map(samples, "/", cpuPercent: 0, Now);

        Assert.Equal(8_000_000_000, metrics.MemoryTotalBytes);
        Assert.Equal(6_000_000_000, metrics.MemoryUsedBytes);
        Assert.Equal(75.0, metrics.MemoryUsedPercent, 1);
        Assert.Equal(100_000_000_000, metrics.DiskTotalBytes);
        Assert.Equal(40_000_000_000, metrics.DiskUsedBytes);
        Assert.Equal(0.5, metrics.Load1, 3);
        Assert.Equal(TimeSpan.FromSeconds(86400), metrics.Uptime);
    }

    [Fact]
    public void Mapper_SelectsFilesystemByMountpoint()
    {
        var samples = PrometheusTextParser.Parse(PrometheusFixtures.NodeScrape1());
        var boot = PrometheusNodeMapper.Map(samples, "/boot", cpuPercent: 0, Now);

        Assert.Equal(500_000_000, boot.DiskTotalBytes);
        Assert.Equal(100_000_000, boot.DiskUsedBytes);
    }

    [Fact]
    public void CpuPercent_ComputedFromCounterDelta()
    {
        var first = PrometheusNodeMapper.ExtractCpuCounters(PrometheusTextParser.Parse(PrometheusFixtures.NodeScrape1()));
        var second = PrometheusNodeMapper.ExtractCpuCounters(PrometheusTextParser.Parse(PrometheusFixtures.NodeScrape2()));

        Assert.Equal(20.0, PrometheusNodeMapper.CpuPercent(first, second), 1);
    }

    [Fact]
    public async Task Provider_FirstScrapeZeroCpu_SecondScrapeComputesCpu()
    {
        var handler = StubHttpMessageHandler.Sequence(PrometheusFixtures.NodeScrape1(), PrometheusFixtures.NodeScrape2());
        var provider = new PrometheusMetricsProvider(new HttpClient(handler), () => Now);
        var host = new HostConfig { Id = "h1", Address = "192.0.2.1", MetricsEndpoint = "http://192.0.2.1:9100/metrics" };

        var first = await provider.GetMetricsAsync(host, CancellationToken.None);
        var second = await provider.GetMetricsAsync(host, CancellationToken.None);

        Assert.Equal(0, first.CpuUsagePercent);
        Assert.Equal(20.0, second.CpuUsagePercent, 1);
        Assert.Equal(75.0, second.MemoryUsedPercent, 1);
    }

    [Fact]
    public void ResolveMetricsEndpoint_DefaultsToNodeExporterPort()
    {
        var host = new HostConfig { Address = "192.0.2.5" };
        Assert.Equal("http://192.0.2.5:9100/metrics", host.ResolveMetricsEndpoint());
    }
}
