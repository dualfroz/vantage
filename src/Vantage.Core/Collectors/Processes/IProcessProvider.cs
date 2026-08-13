using Vantage.Core.Models;

namespace Vantage.Core.Collectors.Processes;

public interface IProcessProvider
{
    Task<IReadOnlyList<ProcessInfo>> GetTopProcessesAsync(HostConfig host, int count, CancellationToken cancellationToken);
}
