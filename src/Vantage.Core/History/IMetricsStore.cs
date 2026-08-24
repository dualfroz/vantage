using Vantage.Core.Models;

namespace Vantage.Core.History;

public interface IMetricsStore
{
    Task RecordAsync(MetricSample sample, CancellationToken cancellationToken = default);

    Task RecordManyAsync(IEnumerable<MetricSample> samples, CancellationToken cancellationToken = default);

    Task RecordHostMetricsAsync(string hostId, HostMetrics metrics, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<MetricPoint>> QueryAsync(
        string hostId,
        string metric,
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken cancellationToken = default);

    Task<int> PruneOlderThanAsync(DateTimeOffset cutoff, CancellationToken cancellationToken = default);

    Task<long> CountAsync(CancellationToken cancellationToken = default);
}
