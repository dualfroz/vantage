using Vantage.Core.Models;
using Vantage.Core.Ssh;

namespace Vantage.Core.Metrics;

public sealed class SshMetricsProvider : IMetricsProvider
{
    private readonly ISshCommandRunner runner;

    public SshMetricsProvider(ISshCommandRunner runner)
    {
        this.runner = runner;
    }

    public async Task<HostMetrics> GetMetricsAsync(HostConfig host, CancellationToken cancellationToken)
    {
        var command = MetricsCommand.Build(host.DiskMount);
        var bundle = await runner.RunAsync(host, command, cancellationToken).ConfigureAwait(false);
        return MetricsBundleParser.Parse(bundle);
    }
}
