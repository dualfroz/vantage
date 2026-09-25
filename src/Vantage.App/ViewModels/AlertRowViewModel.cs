using System.Globalization;
using Vantage.App.Converters;
using Vantage.Core.Alerts;

namespace Vantage.App.ViewModels;

public sealed class AlertRowViewModel : ViewModelBase
{
    public AlertRowViewModel(AlertStatus status, string hostName)
    {
        Status = status;
        HostName = hostName;
    }

    public AlertStatus Status { get; }

    public string HostName { get; }

    public string RuleName => Status.RuleName;

    public string Metric => Status.Metric;

    public string ConditionText =>
        string.Create(CultureInfo.InvariantCulture, $"{Status.Metric} {Status.ComparisonSymbol} {Status.Threshold:0.##}");

    public string ValueText =>
        string.Create(CultureInfo.InvariantCulture, $"now {Status.LastValue:0.##}");

    public string StateText => Status.State.ToString();

    public string SeverityText => Status.Severity.ToString();

    public string SinceText => Status.State == AlertState.Ok
        ? ""
        : "since " + Status.Since.ToLocalTime().ToString("HH:mm:ss", CultureInfo.InvariantCulture);

    public StatusTone Tone => Status.State switch
    {
        AlertState.Firing => StatusTone.Down,
        AlertState.Pending => StatusTone.Warn,
        _ => StatusTone.Ok
    };
}
