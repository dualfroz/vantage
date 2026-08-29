using Vantage.Core.Alerts;
using Xunit;

namespace Vantage.Core.Tests.Alerts;

public class AlertRuleEvaluatorTests
{
    private static readonly DateTimeOffset T0 = new(2026, 8, 26, 12, 0, 0, TimeSpan.Zero);

    private static AlertRule CpuRule(TimeSpan forDuration) => new()
    {
        Id = "rule-cpu",
        Name = "High CPU",
        Metric = "cpu",
        Comparison = AlertComparison.GreaterThan,
        Threshold = 90,
        For = forDuration
    };

    [Fact]
    public void Breach_HeldForDuration_FiresOnceThenStaysFiring()
    {
        var evaluator = new AlertRuleEvaluator(CpuRule(TimeSpan.FromSeconds(60)), "h1");

        Assert.Null(evaluator.Update(95, T0));
        Assert.Equal(AlertState.Pending, evaluator.State);

        Assert.Null(evaluator.Update(96, T0.AddSeconds(30)));
        Assert.Equal(AlertState.Pending, evaluator.State);

        var fired = evaluator.Update(97, T0.AddSeconds(60));
        Assert.NotNull(fired);
        Assert.Equal(AlertTransitionKind.Fired, fired!.Kind);
        Assert.Equal(AlertState.Firing, evaluator.State);

        Assert.Null(evaluator.Update(98, T0.AddSeconds(90)));
        Assert.Equal(AlertState.Firing, evaluator.State);
    }

    [Fact]
    public void Recovery_AfterFiring_EmitsResolved()
    {
        var evaluator = new AlertRuleEvaluator(CpuRule(TimeSpan.Zero), "h1");

        var fired = evaluator.Update(95, T0);
        Assert.Equal(AlertTransitionKind.Fired, fired!.Kind);

        var resolved = evaluator.Update(40, T0.AddSeconds(10));
        Assert.NotNull(resolved);
        Assert.Equal(AlertTransitionKind.Resolved, resolved!.Kind);
        Assert.Equal(AlertState.Ok, evaluator.State);
    }

    [Fact]
    public void BreachThatClearsBeforeDuration_NeverFires()
    {
        var evaluator = new AlertRuleEvaluator(CpuRule(TimeSpan.FromSeconds(60)), "h1");

        Assert.Null(evaluator.Update(95, T0));
        Assert.Null(evaluator.Update(97, T0.AddSeconds(30)));
        Assert.Null(evaluator.Update(20, T0.AddSeconds(45)));
        Assert.Equal(AlertState.Ok, evaluator.State);

        Assert.Null(evaluator.Update(95, T0.AddSeconds(50)));
        Assert.Equal(AlertState.Pending, evaluator.State);
    }

    [Fact]
    public void ValueNeverBreaches_ProducesNoTransition()
    {
        var evaluator = new AlertRuleEvaluator(CpuRule(TimeSpan.Zero), "h1");

        for (var i = 0; i < 5; i++)
            Assert.Null(evaluator.Update(50, T0.AddSeconds(i * 10)));

        Assert.Equal(AlertState.Ok, evaluator.State);
    }

    [Fact]
    public void LessThanComparison_FiresWhenBelowThreshold()
    {
        var rule = new AlertRule
        {
            Name = "Disk low",
            Metric = "disk",
            Comparison = AlertComparison.LessThan,
            Threshold = 10,
            For = TimeSpan.Zero
        };
        var evaluator = new AlertRuleEvaluator(rule, "h1");

        Assert.Null(evaluator.Update(50, T0));
        var fired = evaluator.Update(5, T0.AddSeconds(5));
        Assert.Equal(AlertTransitionKind.Fired, fired!.Kind);
    }
}
