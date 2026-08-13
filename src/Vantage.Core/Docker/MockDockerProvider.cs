using System.Collections.Concurrent;
using Vantage.Core.Models;

namespace Vantage.Core.Docker;

public sealed class MockDockerProvider : IDockerProvider
{
    private readonly ConcurrentDictionary<string, List<DockerContainer>> byHost = new();

    public Task<IReadOnlyList<DockerContainer>> ListContainersAsync(HostConfig host, CancellationToken cancellationToken)
    {
        var containers = byHost.GetOrAdd(host.Id, _ => Seed());
        lock (containers)
            return Task.FromResult<IReadOnlyList<DockerContainer>>(containers.ToArray());
    }

    public Task ExecuteAsync(HostConfig host, DockerAction action, string containerId, CancellationToken cancellationToken)
    {
        var containers = byHost.GetOrAdd(host.Id, _ => Seed());
        lock (containers)
        {
            var index = containers.FindIndex(c => c.Id == containerId);
            if (index < 0)
                return Task.CompletedTask;

            containers[index] = Transition(containers[index], action);
        }

        return Task.CompletedTask;
    }

    private static DockerContainer Transition(DockerContainer container, DockerAction action) => action switch
    {
        DockerAction.Stop => container with { State = ContainerState.Exited, StatusText = "Exited (0) just now" },
        DockerAction.Start => container with { State = ContainerState.Running, StatusText = "Up 1 second" },
        DockerAction.Restart => container with { State = ContainerState.Running, StatusText = "Up 1 second (restarted)" },
        _ => container
    };

    private static List<DockerContainer> Seed() => new()
    {
        new DockerContainer { Id = "c1a2b3c4d5e6", Name = "caddy", Image = "caddy:2", State = ContainerState.Running, StatusText = "Up 6 days" },
        new DockerContainer { Id = "f7e8d9c0b1a2", Name = "postgres", Image = "postgres:16", State = ContainerState.Running, StatusText = "Up 6 days (healthy)" },
        new DockerContainer { Id = "1234abcd5678", Name = "grafana", Image = "grafana/grafana:11", State = ContainerState.Running, StatusText = "Up 2 days" },
        new DockerContainer { Id = "9f8e7d6c5b4a", Name = "backup-runner", Image = "alpine:3.20", State = ContainerState.Exited, StatusText = "Exited (0) 3 hours ago" }
    };
}
