namespace Vantage.Core.Alerts;

public sealed class LogAlertSink : IAlertSink
{
    private readonly IAlertLog log;

    public LogAlertSink(IAlertLog log)
    {
        this.log = log;
    }

    public Task NotifyAsync(AlertNotification notification, CancellationToken cancellationToken = default)
    {
        log.Append(new AlertLogEntry(notification.Timestamp, notification.Severity, notification.Summary()));
        return Task.CompletedTask;
    }
}
