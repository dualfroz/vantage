using Vantage.Core.Models;

namespace Vantage.Core.Docker;

public enum DockerAction
{
    Start,
    Stop,
    Restart
}

public interface IDockerProvider
{
    Task<IReadOnlyList<DockerContainer>> ListContainersAsync(HostConfig host, CancellationToken cancellationToken);

    Task ExecuteAsync(HostConfig host, DockerAction action, string containerId, CancellationToken cancellationToken);
}
