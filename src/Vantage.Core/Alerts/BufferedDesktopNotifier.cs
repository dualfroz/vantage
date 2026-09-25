namespace Vantage.Core.Alerts;

public sealed class BufferedDesktopNotifier : IDesktopNotifier
{
    public const int DefaultMaxPerWindow = 3;
    public const int PendingCapacity = 200;
    public static readonly TimeSpan RateWindow = TimeSpan.FromMinutes(1);

    public readonly record struct Toast(string Title, string Body, AlertSeverity Severity, DateTimeOffset At);

    private readonly object gate = new();
    private readonly Queue<Toast> pending = new();
    private readonly Queue<DateTimeOffset> recentlySent = new();
    private readonly INotificationSender? sender;
    private readonly Func<DateTimeOffset> now;
    private int heldBack;

    public BufferedDesktopNotifier(INotificationSender? sender = null, Func<DateTimeOffset>? now = null)
    {
        this.sender = sender;
        this.now = now ?? (() => DateTimeOffset.UtcNow);
    }

    public bool SendToDesktop { get; set; } = true;

    public int MaxPerWindow { get; set; } = DefaultMaxPerWindow;

    public Task ShowAsync(string title, string body, AlertSeverity severity, CancellationToken cancellationToken = default)
    {
        string outgoingBody;
        lock (gate)
        {
            var at = now();
            pending.Enqueue(new Toast(title, body, severity, at));
            while (pending.Count > PendingCapacity)
                pending.Dequeue();
            if (sender is null || !SendToDesktop)
                return Task.CompletedTask;

            while (recentlySent.Count > 0 && at - recentlySent.Peek() >= RateWindow)
                recentlySent.Dequeue();

            if (recentlySent.Count >= MaxPerWindow)
            {
                heldBack++;
                return Task.CompletedTask;
            }

            recentlySent.Enqueue(at);
            outgoingBody = heldBack == 0
                ? body
                : $"{body}\n{heldBack} more {(heldBack == 1 ? "alert was" : "alerts were")} not shown as notifications; see the Alerts tab.";
            heldBack = 0;
        }

        return sender.SendAsync(title, outgoingBody, severity, cancellationToken);
    }

    public IReadOnlyList<Toast> DrainPending()
    {
        lock (gate)
        {
            var drained = pending.ToArray();
            pending.Clear();
            return drained;
        }
    }
}
