using Vantage.Core.Collectors.Processes;
using Vantage.Core.Models;
using Vantage.Core.Tests.Fakes;
using Vantage.Core.Tests.Fixtures;
using Xunit;

namespace Vantage.Core.Tests.Collectors;

public class ProcessTests
{
    [Fact]
    public void Parser_ReadsAllColumns()
    {
        var processes = ProcessListParser.Parse(CollectorFixtures.PsTopOutput);

        Assert.Equal(5, processes.Count);
        var node = processes.Single(p => p.Pid == 842);
        Assert.Equal("node", node.User);
        Assert.Equal(12.6, node.CpuPercent, 3);
        Assert.Equal(6.3, node.MemoryPercent, 3);
        Assert.Equal("node server.js", node.Command);
    }

    [Fact]
    public void TopByCpu_SortsDescendingAndLimits()
    {
        var top = ProcessListParser.TopByCpu(CollectorFixtures.PsTopOutput, 2);

        Assert.Equal(2, top.Count);
        Assert.Equal(842, top[0].Pid);
        Assert.Equal(311, top[1].Pid);
    }

    [Fact]
    public void Parser_IgnoresLinesWithoutNumericPid()
    {
        var processes = ProcessListParser.Parse("PID USER %CPU %MEM COMMAND\n123 root 1.0 2.0 init\n");
        Assert.Single(processes);
        Assert.Equal(123, processes[0].Pid);
    }

    [Fact]
    public void BuildCommand_SortsByCpuAndLimits()
    {
        var command = SshProcessProvider.BuildCommand(7);
        Assert.Contains("--sort=-pcpu", command);
        Assert.Contains("head -n 7", command);
    }

    [Fact]
    public async Task SshProcessProvider_ParsesRunnerOutput()
    {
        var runner = new FakeSshCommandRunner(CollectorFixtures.PsTopOutput);
        var provider = new SshProcessProvider(runner);

        var top = await provider.GetTopProcessesAsync(new HostConfig(), 3, CancellationToken.None);

        Assert.Equal(3, top.Count);
        Assert.Equal(842, top[0].Pid);
    }

    [Fact]
    public async Task MockProcessProvider_ReturnsTopNSortedByCpu()
    {
        var provider = new MockProcessProvider();
        var top = await provider.GetTopProcessesAsync(new HostConfig { Id = "h1" }, 4, CancellationToken.None);

        Assert.Equal(4, top.Count);
        for (var i = 1; i < top.Count; i++)
            Assert.True(top[i - 1].CpuPercent >= top[i].CpuPercent);
    }
}
