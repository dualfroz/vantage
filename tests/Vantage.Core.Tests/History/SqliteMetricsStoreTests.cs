using Vantage.Core.History;
using Vantage.Core.Models;
using Xunit;

namespace Vantage.Core.Tests.History;

public class SqliteMetricsStoreTests
{
    private static readonly DateTimeOffset Origin = new(2026, 8, 26, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task RecordAndQuery_ReturnsPointsInTimeOrder()
    {
        await using var store = SqliteMetricsStore.InMemory();

        await store.RecordAsync(new MetricSample("h1", MetricNames.Cpu, Origin, 10));
        await store.RecordAsync(new MetricSample("h1", MetricNames.Cpu, Origin.AddSeconds(30), 30));
        await store.RecordAsync(new MetricSample("h1", MetricNames.Cpu, Origin.AddSeconds(15), 20));

        var points = await store.QueryAsync("h1", MetricNames.Cpu, Origin, Origin.AddMinutes(1));

        Assert.Equal(new[] { 10.0, 20.0, 30.0 }, points.Select(p => p.Value));
        Assert.True(points[0].Timestamp < points[1].Timestamp);
        Assert.True(points[1].Timestamp < points[2].Timestamp);
    }

    [Fact]
    public async Task Query_IsScopedToHostAndMetricAndWindow()
    {
        await using var store = SqliteMetricsStore.InMemory();

        await store.RecordAsync(new MetricSample("h1", MetricNames.Cpu, Origin, 55));
        await store.RecordAsync(new MetricSample("h2", MetricNames.Cpu, Origin, 99));
        await store.RecordAsync(new MetricSample("h1", MetricNames.Memory, Origin, 42));
        await store.RecordAsync(new MetricSample("h1", MetricNames.Cpu, Origin.AddHours(2), 77));

        var points = await store.QueryAsync("h1", MetricNames.Cpu, Origin.AddMinutes(-1), Origin.AddMinutes(1));

        Assert.Single(points);
        Assert.Equal(55, points[0].Value);
    }

    [Fact]
    public async Task RecordHostMetrics_ExpandsStandardSeries()
    {
        await using var store = SqliteMetricsStore.InMemory();
        var metrics = new HostMetrics
        {
            CpuUsagePercent = 40,
            MemoryUsedBytes = 3_000_000_000,
            MemoryTotalBytes = 4_000_000_000,
            DiskUsedBytes = 50,
            DiskTotalBytes = 100,
            Load1 = 1.2,
            Load5 = 1.1,
            Load15 = 0.9,
            CollectedAt = Origin
        };

        await store.RecordHostMetricsAsync("h1", metrics);

        var cpu = await store.QueryAsync("h1", MetricNames.Cpu, Origin.AddMinutes(-1), Origin.AddMinutes(1));
        var disk = await store.QueryAsync("h1", MetricNames.Disk, Origin.AddMinutes(-1), Origin.AddMinutes(1));

        Assert.Equal(40, cpu.Single().Value);
        Assert.Equal(50, disk.Single().Value);
        Assert.Equal(6, await store.CountAsync());
    }

    [Fact]
    public async Task PruneOlderThan_DropsExpiredRowsOnly()
    {
        await using var store = SqliteMetricsStore.InMemory();

        await store.RecordAsync(new MetricSample("h1", MetricNames.Cpu, Origin.AddHours(-10), 1));
        await store.RecordAsync(new MetricSample("h1", MetricNames.Cpu, Origin.AddHours(-1), 2));
        await store.RecordAsync(new MetricSample("h1", MetricNames.Cpu, Origin, 3));

        var removed = await store.PruneOlderThanAsync(Origin.AddHours(-5));

        Assert.Equal(1, removed);
        Assert.Equal(2, await store.CountAsync());

        var survivors = await store.QueryAsync("h1", MetricNames.Cpu, Origin.AddDays(-1), Origin.AddDays(1));
        Assert.Equal(new[] { 2.0, 3.0 }, survivors.Select(p => p.Value));
    }

    [Fact]
    public async Task PruneOlderThan_WithCutoffBeforeAllData_RemovesNothing()
    {
        await using var store = SqliteMetricsStore.InMemory();
        await store.RecordAsync(new MetricSample("h1", MetricNames.Cpu, Origin, 3));

        var removed = await store.PruneOlderThanAsync(Origin.AddDays(-1));

        Assert.Equal(0, removed);
        Assert.Equal(1, await store.CountAsync());
    }
}
