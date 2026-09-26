using Vantage.Core.Alerts;
using Xunit;

namespace Vantage.Core.Tests.Alerts;

public class DesktopNotifierTests
{
    private sealed class RecordingSender : INotificationSender
    {
        public List<(string Title, string Body, AlertSeverity Severity)> Sent { get; } = new();

        public Task SendAsync(string title, string body, AlertSeverity severity, CancellationToken cancellationToken = default)
        {
            Sent.Add((title, body, severity));
            return Task.CompletedTask;
        }
    }

    private DateTimeOffset now = new(2026, 10, 26, 12, 0, 0, TimeSpan.Zero);

    private BufferedDesktopNotifier Notifier(RecordingSender sender) => new(sender, () => now);

    [Fact]
    public async Task Show_SendsToTheDesktopAndKeepsTheInMemoryList()
    {
        var sender = new RecordingSender();
        var notifier = Notifier(sender);

        await notifier.ShowAsync("Alert: High CPU", "cpu > 90", AlertSeverity.Critical);

        Assert.Equal(("Alert: High CPU", "cpu > 90", AlertSeverity.Critical), Assert.Single(sender.Sent));
        Assert.Equal("Alert: High CPU", Assert.Single(notifier.DrainPending()).Title);
    }

    [Fact]
    public async Task Disabled_OnlyBuffers()
    {
        var sender = new RecordingSender();
        var notifier = Notifier(sender);
        notifier.SendToDesktop = false;

        await notifier.ShowAsync("Alert: High CPU", "cpu > 90", AlertSeverity.Warning);

        Assert.Empty(sender.Sent);
        Assert.Single(notifier.DrainPending());
    }

    [Fact]
    public async Task Burst_IsRateLimitedAndTheNextNotificationMentionsWhatWasHeldBack()
    {
        var sender = new RecordingSender();
        var notifier = Notifier(sender);

        for (var i = 0; i < 6; i++)
        {
            await notifier.ShowAsync($"Alert {i}", "body", AlertSeverity.Warning);
            now = now.AddSeconds(5);
        }

        Assert.Equal(BufferedDesktopNotifier.DefaultMaxPerWindow, sender.Sent.Count);
        Assert.Equal(6, notifier.DrainPending().Count);

        now = now.AddSeconds(31);
        await notifier.ShowAsync("Alert 6", "body", AlertSeverity.Warning);

        Assert.Equal(BufferedDesktopNotifier.DefaultMaxPerWindow + 1, sender.Sent.Count);
        Assert.Equal("body\n3 more alerts were not shown as notifications; see the Alerts tab.", sender.Sent[^1].Body);
    }

    [Fact]
    public async Task RateLimit_FollowsTheConfiguredMaximum()
    {
        var sender = new RecordingSender();
        var notifier = Notifier(sender);
        notifier.MaxPerWindow = 5;

        for (var i = 0; i < 8; i++)
            await notifier.ShowAsync($"Alert {i}", "body", AlertSeverity.Warning);

        Assert.Equal(5, sender.Sent.Count);
    }

    [Fact]
    public async Task PendingBuffer_KeepsOnlyTheNewestToasts()
    {
        var notifier = new BufferedDesktopNotifier();

        for (var i = 0; i < BufferedDesktopNotifier.PendingCapacity + 50; i++)
            await notifier.ShowAsync($"Alert {i}", "body", AlertSeverity.Info);

        var pending = notifier.DrainPending();
        Assert.Equal(BufferedDesktopNotifier.PendingCapacity, pending.Count);
        Assert.Equal("Alert 50", pending[0].Title);
        Assert.Equal($"Alert {BufferedDesktopNotifier.PendingCapacity + 49}", pending[^1].Title);
    }

    [Fact]
    public async Task WithoutSender_OnlyBuffers()
    {
        var notifier = new BufferedDesktopNotifier();

        await notifier.ShowAsync("Alert", "body", AlertSeverity.Info);

        Assert.Single(notifier.DrainPending());
    }

    [Fact]
    public void FreedesktopSender_MapsUrgencyAndEscapesMarkup()
    {
        Assert.Equal(2, FreedesktopNotificationSender.Urgency(AlertSeverity.Critical));
        Assert.Equal(1, FreedesktopNotificationSender.Urgency(AlertSeverity.Warning));
        Assert.Equal(0, FreedesktopNotificationSender.Urgency(AlertSeverity.Info));
        Assert.Equal("cpu &gt; 90 &amp; load &lt; 2", FreedesktopNotificationSender.EscapeMarkup("cpu > 90 & load < 2"));
    }
}
