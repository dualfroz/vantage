using Vantage.Core.Models;

namespace Vantage.Core.Health;

public interface IHealthChecker
{
    Task<HealthCheckResult> CheckAsync(HealthCheckConfig check, CancellationToken cancellationToken);
}
