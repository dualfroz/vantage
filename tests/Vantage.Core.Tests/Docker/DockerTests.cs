using Vantage.Core.Docker;
using Vantage.Core.Models;
using Vantage.Core.Tests.Fakes;
using Xunit;

namespace Vantage.Core.Tests.Docker;

public class DockerTests
{
    private const string PsOutput =
        "c1a2\tcaddy\tcaddy:2\trunning\tUp 6 days\n" +
        "9f8e\tbackup\talpine:3.20\texited\tExited (0) 3 hours ago\n" +
        "aa11\tpaused-svc\tredis:7\tpaused\tUp 2 hours (Paused)\n";

    [Fact]
    public void Parser_MapsFieldsAndStates()
    {
        var containers = DockerContainerParser.Parse(PsOutput);

        Assert.Equal(3, containers.Count);
        Assert.Equal("caddy", containers[0].Name);
        Assert.Equal(ContainerState.Running, containers[0].State);
        Assert.True(containers[0].IsRunning);
        Assert.Equal(ContainerState.Exited, containers[1].State);
        Assert.Equal(ContainerState.Paused, containers[2].State);
    }

    [Fact]
    public async Task SshDockerProvider_ListParsesRunnerOutput()
    {
        var runner = new FakeSshCommandRunner(PsOutput);
        var provider = new SshDockerProvider(runner);

        var containers = await provider.ListContainersAsync(new HostConfig(), CancellationToken.None);

        Assert.Equal(3, containers.Count);
        Assert.Contains("docker ps -a", runner.Commands.Single());
    }

    [Fact]
    public async Task SshDockerProvider_ExecuteBuildsRestartCommand()
    {
        var runner = new FakeSshCommandRunner(_ => "");
        var provider = new SshDockerProvider(runner);

        await provider.ExecuteAsync(new HostConfig(), DockerAction.Restart, "abc123", CancellationToken.None);

        Assert.Equal("docker restart 'abc123'", runner.Commands.Single());
    }

    [Fact]
    public async Task MockDockerProvider_StopThenStartTransitionsState()
    {
        var provider = new MockDockerProvider();
        var host = new HostConfig { Id = "h1" };

        var initial = await provider.ListContainersAsync(host, CancellationToken.None);
        var running = initial.First(c => c.IsRunning);

        await provider.ExecuteAsync(host, DockerAction.Stop, running.Id, CancellationToken.None);
        var afterStop = await provider.ListContainersAsync(host, CancellationToken.None);
        Assert.Equal(ContainerState.Exited, afterStop.First(c => c.Id == running.Id).State);

        await provider.ExecuteAsync(host, DockerAction.Start, running.Id, CancellationToken.None);
        var afterStart = await provider.ListContainersAsync(host, CancellationToken.None);
        Assert.Equal(ContainerState.Running, afterStart.First(c => c.Id == running.Id).State);
    }
}
