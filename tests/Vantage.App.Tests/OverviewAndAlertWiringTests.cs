using System.Collections.ObjectModel;
using Vantage.App.ViewModels;
using Vantage.Core.Alerts;
using Vantage.Core.Configuration;
using Vantage.Core.Docker;
using Vantage.Core.Health;
using Vantage.Core.History;
using Vantage.Core.Metrics;
using Vantage.Core.Models;
using Xunit;

namespace Vantage.App.Tests;

public class OverviewAndAlertWiringTests
{
    private sealed class FixedMetricsProvider : IMetricsProvider
    {
        private readonly double cpu;

        public FixedMetricsProvider(double cpu)
        {
            this.cpu = cpu;
        }

        public Task<HostMetrics> GetMetricsAsync(HostConfig host, CancellationToken cancellationToken) =>
            Task.FromResult(new HostMetrics
            {
                CpuUsagePercent = cpu,
                MemoryUsedBytes = 90,
                MemoryTotalBytes = 100,
                DiskUsedBytes = 10,
                DiskTotalBytes = 100,
                Load1 = 1,
                CollectedAt = new DateTimeOffset(2026, 8, 26, 12, 0, 0, TimeSpan.Zero)
            });
    }

    private static async Task<IConfigStore> SeededStore(AlertRule rule)
    {
        var store = new JsonConfigStore(
            Path.Combine(Path.GetTempPath(), "vantage-wire-" + Guid.NewGuid().ToString("N"), "config.json"));
        await store.SaveAsync(new VantageConfig
        {
            Hosts = { new HostConfig { Id = "h1", Name = "vps01" } },
            AlertRules = { rule }
        });
        return store;
    }

    [Fact]
    public void Overview_AggregatesOnlineOfflineAndPeaks()
    {
        var hosts = new ObservableCollection<HostViewModel>
        {
            new(new HostConfig { Name = "a" }, 10),
            new(new HostConfig { Name = "b" }, 10),
            new(new HostConfig { Name = "c" }, 10)
        };
        hosts[0].ApplyMetrics(new HostMetrics { CpuUsagePercent = 20, MemoryTotalBytes = 100, MemoryUsedBytes = 30 });
        hosts[1].ApplyMetrics(new HostMetrics { CpuUsagePercent = 80, MemoryTotalBytes = 100, MemoryUsedBytes = 60 });
        hosts[2].ApplyError("down");

        var overview = new OverviewViewModel(hosts);
        overview.Refresh();

        Assert.Equal(3, overview.TotalHosts);
        Assert.Equal(2, overview.OnlineHosts);
        Assert.Equal(1, overview.OfflineHosts);
        Assert.Equal(50, overview.AverageCpuPercent, 1);
        Assert.Equal(80, overview.PeakCpuPercent, 1);
        Assert.Equal("b", overview.PeakCpuHost);
    }

    [Fact]
    public async Task Refresh_RecordsHistoryAndFiresAlerts()
    {
        await using var store = SqliteMetricsStore.InMemory();
        var sink = new CollectingSink();
        var rule = new AlertRule { Name = "CPU", Metric = "cpu", Comparison = AlertComparison.GreaterOrEqual, Threshold = 80, For = TimeSpan.Zero };
        var engine = new AlertEngine(Array.Empty<AlertRule>(), new IAlertSink[] { sink });

        var viewModel = new MainWindowViewModel(
            new FixedMetricsProvider(95),
            new MockDockerProvider(),
            new HealthChecker(new HttpClient()),
            await SeededStore(rule),
            store,
            engine);

        await viewModel.InitializeAsync();
        await viewModel.RefreshSelectedHostAsync(CancellationToken.None);

        Assert.True(await store.CountAsync() >= 6);
        Assert.Contains(sink.Received, n => n.Kind == AlertTransitionKind.Fired);
        Assert.Equal(1, viewModel.FiringAlertCount);
        Assert.NotEmpty(viewModel.Alerts);
    }

    [Fact]
    public async Task Refresh_BelowThreshold_DoesNotFire()
    {
        await using var store = SqliteMetricsStore.InMemory();
        var sink = new CollectingSink();
        var rule = new AlertRule { Name = "CPU", Metric = "cpu", Comparison = AlertComparison.GreaterOrEqual, Threshold = 80, For = TimeSpan.Zero };
        var engine = new AlertEngine(Array.Empty<AlertRule>(), new IAlertSink[] { sink });

        var viewModel = new MainWindowViewModel(
            new FixedMetricsProvider(15),
            new MockDockerProvider(),
            new HealthChecker(new HttpClient()),
            await SeededStore(rule),
            store,
            engine);

        await viewModel.InitializeAsync();
        await viewModel.RefreshSelectedHostAsync(CancellationToken.None);

        Assert.Empty(sink.Received);
        Assert.Equal(0, viewModel.FiringAlertCount);
    }

    private sealed class CollectingSink : IAlertSink
    {
        public List<AlertNotification> Received { get; } = new();

        public Task NotifyAsync(AlertNotification notification, CancellationToken cancellationToken = default)
        {
            Received.Add(notification);
            return Task.CompletedTask;
        }
    }
}
