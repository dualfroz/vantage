using Vantage.Core.Models;
using Vantage.Core.Ssh;

namespace Vantage.Core.Collectors.Processes;

public sealed class SshProcessProvider : IProcessProvider
{
    private readonly ISshCommandRunner runner;

    public SshProcessProvider(ISshCommandRunner runner)
    {
        this.runner = runner;
    }

    public static string BuildCommand(int count)
    {
        var limit = Math.Max(1, count);
        return $"ps -eo pid,user:20,pcpu,pmem,comm --sort=-pcpu --no-headers | head -n {limit}";
    }

    public async Task<IReadOnlyList<ProcessInfo>> GetTopProcessesAsync(HostConfig host, int count, CancellationToken cancellationToken)
    {
        var output = await runner.RunAsync(host, BuildCommand(count), cancellationToken).ConfigureAwait(false);
        return ProcessListParser.TopByCpu(output, count);
    }
}
