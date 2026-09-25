using Vantage.Core.Models;

namespace Vantage.Core.Collectors.Processes;

public enum ProcessSignal
{
    Terminate,
    Kill
}

public interface IProcessProvider
{
    Task<IReadOnlyList<ProcessInfo>> GetTopProcessesAsync(HostConfig host, int count, CancellationToken cancellationToken);

    Task SignalAsync(HostConfig host, int pid, ProcessSignal signal, CancellationToken cancellationToken);
}
