using System.Collections.Concurrent;
using System.Globalization;
using Vantage.App.Converters;
using Vantage.Core.Alerts;

namespace Vantage.App.ViewModels;

public sealed class AlertTransitionRecorder : IAlertSink
{
    private readonly ConcurrentQueue<AlertNotification> pending = new();

    public Task NotifyAsync(AlertNotification notification, CancellationToken cancellationToken = default)
    {
        pending.Enqueue(notification);
        return Task.CompletedTask;
    }

    public IReadOnlyList<AlertNotification> Drain()
    {
        var drained = new List<AlertNotification>();
        while (pending.TryDequeue(out var notification))
            drained.Add(notification);
        return drained;
    }
}

public sealed class AlertTransitionViewModel : ViewModelBase
{
    public AlertTransitionViewModel(AlertNotification notification, string hostName)
    {
        Notification = notification;
        HostName = hostName;
    }

    public AlertNotification Notification { get; }

    public string HostName { get; }

    public string RuleName => Notification.RuleName;

    public string KindText => Notification.Kind == AlertTransitionKind.Fired ? "Fired" : "Resolved";

    public string TimeText => Notification.Timestamp.ToLocalTime().ToString("HH:mm:ss", CultureInfo.InvariantCulture);

    public string DetailText => string.Create(
        CultureInfo.InvariantCulture,
        $"{Notification.Metric} {Notification.ComparisonSymbol} {Notification.Threshold:0.##}, was {Notification.Value:0.##}");

    public StatusTone Tone => Notification.Kind == AlertTransitionKind.Fired ? StatusTone.Down : StatusTone.Ok;
}
