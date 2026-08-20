using Vantage.Core.Models;

namespace Vantage.Core.Metrics;

public interface IMetricsProvider
{
    Task<HostMetrics> GetMetricsAsync(HostConfig host, CancellationToken cancellationToken);
}
