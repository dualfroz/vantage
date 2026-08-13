using Vantage.Core.Models;

namespace Vantage.Core.Collectors.Systemd;

public interface ISystemdProvider
{
    Task<IReadOnlyList<SystemdUnit>> GetServicesAsync(HostConfig host, CancellationToken cancellationToken);
}
