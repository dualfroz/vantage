using System.Text.Json;
using Vantage.Core.Alerts;
using Vantage.Core.Tests.Fakes;
using Xunit;

namespace Vantage.Core.Tests.Alerts;

public class AlertSinkTests
{
    private static readonly DateTimeOffset T0 = new(2026, 8, 26, 12, 0, 0, TimeSpan.Zero);

    private static AlertNotification Fired() => new(
        "rule-1", "High CPU", "vps01", "cpu", AlertSeverity.Critical,
        AlertTransitionKind.Fired, 95.5, 90, ">", TimeSpan.FromMinutes(1), T0);

    [Fact]
    public async Task WebhookSink_PostsJsonPayload()
    {
        var handler = StubHttpMessageHandler.Capturing();
        var sink = new WebhookAlertSink(new HttpClient(handler), new Uri("https://hooks.example/vantage"));

        await sink.NotifyAsync(Fired());

        var request = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal("https://hooks.example/vantage", request.RequestUri!.ToString());

        using var body = JsonDocument.Parse(handler.Bodies[0]);
        var root = body.RootElement;
        Assert.Equal("firing", root.GetProperty("status").GetString());
        Assert.Equal("critical", root.GetProperty("severity").GetString());
        Assert.Equal("High CPU", root.GetProperty("rule").GetString());
        Assert.Equal("vps01", root.GetProperty("host").GetString());
        Assert.Equal(90, root.GetProperty("threshold").GetDouble());
    }

    [Fact]
    public async Task LogSink_AppendsSummaryLine()
    {
        var log = new InMemoryAlertLog();
        var sink = new LogAlertSink(log);

        await sink.NotifyAsync(Fired());

        var entry = Assert.Single(log.Recent(10));
        Assert.Equal(AlertSeverity.Critical, entry.Severity);
        Assert.Contains("FIRING", entry.Line);
        Assert.Contains("vps01", entry.Line);
    }

    [Fact]
    public async Task DesktopSink_RoutesToNotifier()
    {
        var notifier = new BufferedDesktopNotifier();
        var sink = new DesktopAlertSink(notifier);

        await sink.NotifyAsync(Fired());

        var toast = Assert.Single(notifier.DrainPending());
        Assert.Contains("High CPU", toast.Title);
        Assert.Equal(AlertSeverity.Critical, toast.Severity);
    }

    [Fact]
    public void InMemoryAlertLog_KeepsMostRecentWithinCapacity()
    {
        var log = new InMemoryAlertLog(capacity: 3);
        for (var i = 0; i < 5; i++)
            log.Append(new AlertLogEntry(T0.AddSeconds(i), AlertSeverity.Info, $"line {i}"));

        var recent = log.Recent(10);
        Assert.Equal(3, recent.Count);
        Assert.Equal("line 4", recent[0].Line);
        Assert.Equal("line 2", recent[2].Line);
    }
}
