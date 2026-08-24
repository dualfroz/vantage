namespace Vantage.Core.Alerts;

public sealed class AlertRuleEvaluator
{
    private readonly AlertRule rule;
    private readonly string hostId;
    private DateTimeOffset? breachingSince;

    public AlertRuleEvaluator(AlertRule rule, string hostId)
    {
        this.rule = rule;
        this.hostId = hostId;
    }

    public AlertState State { get; private set; } = AlertState.Ok;
    public DateTimeOffset Since { get; private set; }
    public double LastValue { get; private set; }

    public AlertNotification? Update(double value, DateTimeOffset now)
    {
        LastValue = value;

        if (rule.IsBreaching(value))
            return OnBreach(value, now);

        return OnClear(value, now);
    }

    private AlertNotification? OnBreach(double value, DateTimeOffset now)
    {
        breachingSince ??= now;

        if (State == AlertState.Firing)
            return null;

        var breachedFor = now - breachingSince.Value;
        if (breachedFor >= rule.For)
        {
            State = AlertState.Firing;
            Since = now;
            return Notification(AlertTransitionKind.Fired, value, now);
        }

        if (State != AlertState.Pending)
        {
            State = AlertState.Pending;
            Since = breachingSince.Value;
        }

        return null;
    }

    private AlertNotification? OnClear(double value, DateTimeOffset now)
    {
        breachingSince = null;
        var wasFiring = State == AlertState.Firing;
        var changed = State != AlertState.Ok;

        State = AlertState.Ok;
        if (changed)
            Since = now;

        return wasFiring ? Notification(AlertTransitionKind.Resolved, value, now) : null;
    }

    public AlertStatus Snapshot() =>
        new(rule.Id, rule.Name, hostId, rule.Metric, rule.Severity, State, LastValue, rule.Threshold, rule.ComparisonSymbol, Since);

    private AlertNotification Notification(AlertTransitionKind kind, double value, DateTimeOffset now) =>
        new(rule.Id, rule.Name, hostId, rule.Metric, rule.Severity, kind, value, rule.Threshold, rule.ComparisonSymbol, rule.For, now);
}
