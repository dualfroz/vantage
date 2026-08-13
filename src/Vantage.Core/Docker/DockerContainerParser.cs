using Vantage.Core.Models;

namespace Vantage.Core.Docker;

public static class DockerContainerParser
{
    public const string Format = "{{.ID}}\t{{.Names}}\t{{.Image}}\t{{.State}}\t{{.Status}}";

    public static IReadOnlyList<DockerContainer> Parse(string psOutput)
    {
        var containers = new List<DockerContainer>();

        foreach (var rawLine in psOutput.Replace("\r\n", "\n").Split('\n'))
        {
            var line = rawLine.TrimEnd();
            if (line.Length == 0)
                continue;

            var fields = line.Split('\t');
            if (fields.Length < 5)
                continue;

            containers.Add(new DockerContainer
            {
                Id = fields[0].Trim(),
                Name = fields[1].Trim(),
                Image = fields[2].Trim(),
                State = MapState(fields[3].Trim()),
                StatusText = fields[4].Trim()
            });
        }

        return containers;
    }

    private static ContainerState MapState(string state) => state.ToLowerInvariant() switch
    {
        "running" => ContainerState.Running,
        "exited" => ContainerState.Exited,
        "paused" => ContainerState.Paused,
        "restarting" => ContainerState.Restarting,
        "created" => ContainerState.Created,
        "dead" => ContainerState.Dead,
        _ => ContainerState.Other
    };
}
