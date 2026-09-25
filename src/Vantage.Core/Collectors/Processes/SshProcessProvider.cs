using System.Globalization;
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

    public static string BuildSignalCommand(HostConfig host, int pid, ProcessSignal signal)
    {
        if (pid <= 1)
            throw new ArgumentOutOfRangeException(nameof(pid), "Refusing to signal PID 0 or 1.");

        var name = signal switch
        {
            ProcessSignal.Terminate => "TERM",
            ProcessSignal.Kill => "KILL",
            _ => throw new ArgumentOutOfRangeException(nameof(signal))
        };

        return ShellArgs.Privileged(host, $"kill -s {name} {pid.ToString(CultureInfo.InvariantCulture)}");
    }

    public async Task<IReadOnlyList<ProcessInfo>> GetTopProcessesAsync(HostConfig host, int count, CancellationToken cancellationToken)
    {
        var output = await runner.RunAsync(host, BuildCommand(count), cancellationToken).ConfigureAwait(false);
        return ProcessListParser.TopByCpu(output, count);
    }

    public async Task SignalAsync(HostConfig host, int pid, ProcessSignal signal, CancellationToken cancellationToken)
    {
        await runner.RunAsync(host, BuildSignalCommand(host, pid, signal), cancellationToken).ConfigureAwait(false);
    }
}
