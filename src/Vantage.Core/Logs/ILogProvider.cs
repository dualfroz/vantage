using Vantage.Core.Models;

namespace Vantage.Core.Logs;

public interface ILogProvider
{
    Task<IReadOnlyList<LogLine>> ReadAsync(HostConfig host, LogSource source, LogQuery query, CancellationToken cancellationToken);
}
