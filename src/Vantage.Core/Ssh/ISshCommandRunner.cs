using Vantage.Core.Models;

namespace Vantage.Core.Ssh;

public interface ISshCommandRunner
{
    Task<string> RunAsync(HostConfig host, string command, CancellationToken cancellationToken);
}
