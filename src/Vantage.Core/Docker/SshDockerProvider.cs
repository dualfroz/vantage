using Vantage.Core.Models;
using Vantage.Core.Ssh;

namespace Vantage.Core.Docker;

public sealed class SshDockerProvider : IDockerProvider
{
    private readonly ISshCommandRunner runner;

    public SshDockerProvider(ISshCommandRunner runner)
    {
        this.runner = runner;
    }

    public async Task<IReadOnlyList<DockerContainer>> ListContainersAsync(HostConfig host, CancellationToken cancellationToken)
    {
        var command = $"docker ps -a --no-trunc --format {ShellArgs.Quote(DockerContainerParser.Format)}";
        var output = await runner.RunAsync(host, command, cancellationToken).ConfigureAwait(false);
        return DockerContainerParser.Parse(output);
    }

    public async Task ExecuteAsync(HostConfig host, DockerAction action, string containerId, CancellationToken cancellationToken)
    {
        var verb = action switch
        {
            DockerAction.Start => "start",
            DockerAction.Stop => "stop",
            DockerAction.Restart => "restart",
            _ => throw new ArgumentOutOfRangeException(nameof(action))
        };

        var command = $"docker {verb} {ShellArgs.Quote(containerId)}";
        await runner.RunAsync(host, command, cancellationToken).ConfigureAwait(false);
    }
}
