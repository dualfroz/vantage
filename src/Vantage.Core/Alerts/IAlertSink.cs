namespace Vantage.Core.Alerts;

public interface IAlertSink
{
    Task NotifyAsync(AlertNotification notification, CancellationToken cancellationToken = default);
}
