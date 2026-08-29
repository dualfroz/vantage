using Vantage.Core.Alerts;
using Vantage.Core.Models;
using Xunit;

namespace Vantage.Core.Tests.Alerts;

public class AlertEngineTests
{
    private static readonly DateTimeOffset T0 = new(2026, 8, 26, 12, 0, 0, TimeSpan.Zero);

    private sealed class CollectingSink : IAlertSink
    {
        public List<AlertNotification> Received { get; } = new();

        public Task NotifyAsync(AlertNotification notification, CancellationToken cancellationToken = default)
        {
            Received.Add(notification);
            return Task.CompletedTask;
        }
    }

    private static HostMetrics MetricsWithCpu(double cpu) => new()
    {
        CpuUsagePercent = cpu,
        MemoryTotalBytes = 100,
        MemoryUsedBytes = 50,
        CollectedAt = T0
    };

    [Fact]
    public async Task Ingest_FiresSinkWhenThresholdBreached()
    {
        var rule = new AlertRule { Name = "CPU", Metric = "cpu", Threshold = 80, For = TimeSpan.Zero };
        var sink = new CollectingSink();
        var engine = new AlertEngine(new[] { rule }, new[] { sink });

        await engine.IngestAsync("h1", MetricsWithCpu(85), T0);

        Assert.Single(sink.Received);
        Assert.Equal(AlertTransitionKind.Fired, sink.Received[0].Kind);
        Assert.Equal("h1", sink.Received[0].HostId);
        Assert.Single(engine.ActiveAlerts());
    }

    [Fact]
    public async Task Ingest_BelowThreshold_NotifiesNothing()
    {
        var rule = new AlertRule { Name = "CPU", Metric = "cpu", Threshold = 80, For = TimeSpan.Zero };
        var sink = new CollectingSink();
        var engine = new AlertEngine(new[] { rule }, new[] { sink });

        await engine.IngestAsync("h1", MetricsWithCpu(20), T0);

        Assert.Empty(sink.Received);
        Assert.Empty(engine.ActiveAlerts());
    }

    [Fact]
    public async Task HostScopedRule_IgnoresOtherHosts()
    {
        var rule = new AlertRule { Name = "CPU", Metric = "cpu", Threshold = 50, For = TimeSpan.Zero, HostId = "h1" };
        var sink = new CollectingSink();
        var engine = new AlertEngine(new[] { rule }, new[] { sink });

        await engine.IngestAsync("h2", MetricsWithCpu(99), T0);
        Assert.Empty(sink.Received);

        await engine.IngestAsync("h1", MetricsWithCpu(99), T0);
        Assert.Single(sink.Received);
    }

    [Fact]
    public async Task WildcardRule_TracksEachHostIndependently()
    {
        var rule = new AlertRule { Name = "CPU", Metric = "cpu", Threshold = 80, For = TimeSpan.Zero };
        var sink = new CollectingSink();
        var engine = new AlertEngine(new[] { rule }, new[] { sink });

        await engine.IngestAsync("h1", MetricsWithCpu(85), T0);
        await engine.IngestAsync("h2", MetricsWithCpu(90), T0);

        Assert.Equal(2, sink.Received.Count);
        Assert.Equal(2, engine.ActiveAlerts().Count);
    }

    [Fact]
    public async Task SinkThrow_DoesNotBreakEvaluationAndSurfacesError()
    {
        var rule = new AlertRule { Name = "CPU", Metric = "cpu", Threshold = 80, For = TimeSpan.Zero };
        var throwing = new ThrowingSink();
        var engine = new AlertEngine(new[] { rule }, new[] { throwing });
        var failures = 0;
        engine.SinkFailed += (_, _, _) => failures++;

        await engine.IngestAsync("h1", MetricsWithCpu(85), T0);

        Assert.Equal(1, failures);
        Assert.Single(engine.ActiveAlerts());
    }

    private sealed class ThrowingSink : IAlertSink
    {
        public Task NotifyAsync(AlertNotification notification, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("boom");
    }
}
