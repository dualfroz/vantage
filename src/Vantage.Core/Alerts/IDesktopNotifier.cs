namespace Vantage.Core.Alerts;

public interface IDesktopNotifier
{
    Task ShowAsync(string title, string body, AlertSeverity severity, CancellationToken cancellationToken = default);
}
