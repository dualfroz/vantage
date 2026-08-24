namespace Vantage.Core.Alerts;

public sealed class DesktopAlertSink : IAlertSink
{
    private readonly IDesktopNotifier notifier;

    public DesktopAlertSink(IDesktopNotifier notifier)
    {
        this.notifier = notifier;
    }

    public Task NotifyAsync(AlertNotification notification, CancellationToken cancellationToken = default)
    {
        var title = notification.Kind == AlertTransitionKind.Fired
            ? $"Alert: {notification.RuleName}"
            : $"Resolved: {notification.RuleName}";
        return notifier.ShowAsync(title, notification.Summary(), notification.Severity, cancellationToken);
    }
}
