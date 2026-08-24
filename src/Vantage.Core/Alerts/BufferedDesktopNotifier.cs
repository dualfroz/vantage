namespace Vantage.Core.Alerts;

public sealed class BufferedDesktopNotifier : IDesktopNotifier
{
    public readonly record struct Toast(string Title, string Body, AlertSeverity Severity, DateTimeOffset At);

    private readonly object gate = new();
    private readonly List<Toast> pending = new();

    public Task ShowAsync(string title, string body, AlertSeverity severity, CancellationToken cancellationToken = default)
    {
        lock (gate)
            pending.Add(new Toast(title, body, severity, DateTimeOffset.UtcNow));
        return Task.CompletedTask;
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
