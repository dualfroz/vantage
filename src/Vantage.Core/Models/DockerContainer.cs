namespace Vantage.Core.Models;

public enum ContainerState
{
    Running,
    Exited,
    Paused,
    Restarting,
    Created,
    Dead,
    Other
}

public sealed record DockerContainer
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";
    public string Image { get; init; } = "";
    public ContainerState State { get; init; } = ContainerState.Other;
    public string StatusText { get; init; } = "";

    public bool IsRunning => State == ContainerState.Running;
}
