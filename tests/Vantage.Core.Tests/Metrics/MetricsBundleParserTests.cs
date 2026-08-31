using Vantage.Core.Metrics;
using Vantage.Core.Tests.Fixtures;
using Xunit;

namespace Vantage.Core.Tests.Metrics;

public class MetricsBundleParserTests
{
    [Fact]
    public void Parse_BuildsHostMetricsFromFullBundle()
    {
        var metrics = MetricsBundleParser.Parse(MetricsFixtures.FullBundle());

        Assert.Equal(80.0, metrics.CpuUsagePercent, 1);
        Assert.Equal(8000000L * 1024, metrics.MemoryTotalBytes);
        Assert.Equal(6000000L * 1024, metrics.MemoryUsedBytes);
        Assert.Equal(75.0, metrics.MemoryUsedPercent, 1);
        Assert.Equal(100000000000L, metrics.DiskTotalBytes);
        Assert.Equal(40000000000L, metrics.DiskUsedBytes);
        Assert.Equal(40.0, metrics.DiskUsedPercent, 1);
        Assert.Equal(0.50, metrics.Load1, 3);
        Assert.Equal(123456.78, metrics.Uptime.TotalSeconds, 2);
    }

    [Fact]
    public void Parse_ThrowsWhenSectionMissing()
    {
        var truncated = "@@vantage:loadavg\n" + MetricsFixtures.Loadavg + "\n";
        Assert.Throws<FormatException>(() => MetricsBundleParser.Parse(truncated));
    }
}
