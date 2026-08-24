namespace Vantage.Core.Alerts;

public enum AlertComparison
{
    GreaterThan,
    GreaterOrEqual,
    LessThan,
    LessOrEqual
}

public enum AlertSeverity
{
    Info,
    Warning,
    Critical
}

public sealed class AlertRule
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "";
    public string? HostId { get; set; }
    public string Metric { get; set; } = "";
    public AlertComparison Comparison { get; set; } = AlertComparison.GreaterThan;
    public double Threshold { get; set; }
    public TimeSpan For { get; set; } = TimeSpan.Zero;
    public AlertSeverity Severity { get; set; } = AlertSeverity.Warning;
    public bool Enabled { get; set; } = true;

    public bool AppliesTo(string hostId) => HostId is null || HostId == hostId;

    public bool IsBreaching(double value) => Comparison switch
    {
        AlertComparison.GreaterThan => value > Threshold,
        AlertComparison.GreaterOrEqual => value >= Threshold,
        AlertComparison.LessThan => value < Threshold,
        AlertComparison.LessOrEqual => value <= Threshold,
        _ => false
    };

    public string ComparisonSymbol => Comparison switch
    {
        AlertComparison.GreaterThan => ">",
        AlertComparison.GreaterOrEqual => ">=",
        AlertComparison.LessThan => "<",
        AlertComparison.LessOrEqual => "<=",
        _ => "?"
    };
}
