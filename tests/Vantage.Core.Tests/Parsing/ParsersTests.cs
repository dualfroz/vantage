using Vantage.Core.Metrics.Parsing;
using Vantage.Core.Tests.Fixtures;
using Xunit;

namespace Vantage.Core.Tests.Parsing;

public class ParsersTests
{
    [Fact]
    public void LoadAvgParser_ReadsFirstThreeValues()
    {
        var load = LoadAvgParser.Parse(MetricsFixtures.Loadavg);
        Assert.Equal(0.50, load.Load1, 3);
        Assert.Equal(0.40, load.Load5, 3);
        Assert.Equal(0.30, load.Load15, 3);
    }

    [Fact]
    public void UptimeParser_ReadsSeconds()
    {
        var uptime = UptimeParser.Parse(MetricsFixtures.Uptime);
        Assert.Equal(123456.78, uptime.TotalSeconds, 2);
    }

    [Fact]
    public void MemInfoParser_UsesMemAvailable()
    {
        var memory = MemInfoParser.Parse(MetricsFixtures.Meminfo);
        Assert.Equal(8000000L * 1024, memory.TotalBytes);
        Assert.Equal(6000000L * 1024, memory.UsedBytes);
    }

    [Fact]
    public void MemInfoParser_FallsBackToMemFreeWhenNoAvailable()
    {
        var memory = MemInfoParser.Parse("MemTotal: 4000 kB\nMemFree: 1000 kB\n");
        Assert.Equal(4000L * 1024, memory.TotalBytes);
        Assert.Equal(3000L * 1024, memory.UsedBytes);
    }

    [Fact]
    public void DfParser_ReadsTotalAndUsedFromDataRow()
    {
        var disk = DfParser.Parse(MetricsFixtures.Df);
        Assert.Equal(100000000000L, disk.TotalBytes);
        Assert.Equal(40000000000L, disk.UsedBytes);
    }

    [Fact]
    public void ProcStatParser_SumsTotalAndIdle()
    {
        var times = ProcStatParser.Parse(MetricsFixtures.Stat1);
        Assert.Equal(1000, times.TotalTime);
        Assert.Equal(800, times.IdleTime);
    }

    [Fact]
    public void CpuUsage_ComputesBusyFractionFromTwoSnapshots()
    {
        var before = ProcStatParser.Parse(MetricsFixtures.Stat1);
        var after = ProcStatParser.Parse(MetricsFixtures.Stat2);
        Assert.Equal(80.0, CpuUsage.Compute(before, after), 1);
    }

    [Fact]
    public void CpuUsage_ReturnsZeroWhenNoDelta()
    {
        var times = new CpuTimes(800, 1000);
        Assert.Equal(0, CpuUsage.Compute(times, times));
    }
}
