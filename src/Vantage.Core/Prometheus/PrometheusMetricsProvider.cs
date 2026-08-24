using System.Collections.Concurrent;
using Vantage.Core.Metrics;
using Vantage.Core.Models;

namespace Vantage.Core.Prometheus;

public sealed class PrometheusMetricsProvider : IMetricsProvider
{
    private readonly HttpClient httpClient;
    private readonly Func<DateTimeOffset> clock;
    private readonly ConcurrentDictionary<string, CpuCounters> cpuState = new();

    public PrometheusMetricsProvider(HttpClient httpClient, Func<DateTimeOffset>? clock = null)
    {
        this.httpClient = httpClient;
        this.clock = clock ?? (() => DateTimeOffset.UtcNow);
    }

    public async Task<HostMetrics> GetMetricsAsync(HostConfig host, CancellationToken cancellationToken)
    {
        var endpoint = host.ResolveMetricsEndpoint();
        var exposition = await httpClient.GetStringAsync(endpoint, cancellationToken).ConfigureAwait(false);
        var samples = PrometheusTextParser.Parse(exposition);

        var current = PrometheusNodeMapper.ExtractCpuCounters(samples);
        var hadPrevious = cpuState.TryGetValue(host.Id, out var previous);
        cpuState[host.Id] = current;
        var cpuPercent = hadPrevious ? PrometheusNodeMapper.CpuPercent(previous, current) : 0;

        return PrometheusNodeMapper.Map(samples, host.DiskMount, cpuPercent, clock());
    }
}
