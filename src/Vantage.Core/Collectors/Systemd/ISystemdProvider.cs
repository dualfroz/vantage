using Vantage.Core.Models;

namespace Vantage.Core.Collectors.Systemd;

public enum SystemdAction
{
    Start,
    Stop,
    Restart
}

public interface ISystemdProvider
{
    Task<IReadOnlyList<SystemdUnit>> GetServicesAsync(HostConfig host, CancellationToken cancellationToken);

    Task ExecuteAsync(HostConfig host, SystemdAction action, string unit, CancellationToken cancellationToken);
}
