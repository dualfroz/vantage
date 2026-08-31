using Vantage.Core.Health;
using Vantage.Core.Metrics;
using Vantage.Core.Models;

namespace Vantage.App.Tests.Fakes;

public sealed class ThrowingMetricsProvider : IMetricsProvider
{
    public const string Message = "host unreachable";

    public Task<HostMetrics> GetMetricsAsync(HostConfig host, CancellationToken cancellationToken) =>
        throw new InvalidOperationException(Message);
}

public sealed class FakeHealthChecker : IHealthChecker
{
    private readonly HealthCheckResult result;

    public FakeHealthChecker(HealthCheckResult result)
    {
        this.result = result;
    }

    public int Calls { get; private set; }

    public Task<HealthCheckResult> CheckAsync(HealthCheckConfig check, CancellationToken cancellationToken)
    {
        Calls++;
        return Task.FromResult(result);
    }
}
