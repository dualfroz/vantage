namespace Vantage.Core.Alerts;

public enum AlertState
{
    Ok,
    Pending,
    Firing
}

public enum AlertTransitionKind
{
    Fired,
    Resolved
}

public sealed record AlertNotification(
    string RuleId,
    string RuleName,
    string HostId,
    string Metric,
    AlertSeverity Severity,
    AlertTransitionKind Kind,
    double Value,
    double Threshold,
    string ComparisonSymbol,
    TimeSpan For,
    DateTimeOffset Timestamp)
{
    public string Summary()
    {
        var scope = $"{Metric} {ComparisonSymbol} {Threshold:0.##}";
        return Kind == AlertTransitionKind.Fired
            ? $"FIRING [{Severity}] {RuleName} on {HostId}: {scope} (is {Value:0.##})"
            : $"RESOLVED [{Severity}] {RuleName} on {HostId}: {scope} (is {Value:0.##})";
    }
}

public sealed record AlertStatus(
    string RuleId,
    string RuleName,
    string HostId,
    string Metric,
    AlertSeverity Severity,
    AlertState State,
    double LastValue,
    double Threshold,
    string ComparisonSymbol,
    DateTimeOffset Since);
