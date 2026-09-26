using Vantage.Core.Collectors.Processes;
using Vantage.Core.Collectors.Systemd;
using Vantage.Core.Models;
using Vantage.Core.Ssh;
using Vantage.Core.Tests.Fakes;
using Xunit;

namespace Vantage.Core.Tests.Collectors;

public class HostActionTests
{
    [Theory]
    [InlineData(ProcessSignal.Terminate, "kill -s TERM 842")]
    [InlineData(ProcessSignal.Kill, "kill -s KILL 842")]
    public void SignalCommand_UsesNamedSignal(ProcessSignal signal, string expected)
    {
        Assert.Equal(expected, SshProcessProvider.BuildSignalCommand(new HostConfig(), 842, signal));
    }

    [Fact]
    public void SignalCommand_WithSudo_IsPrefixed()
    {
        var host = new HostConfig { UseSudo = true };
        Assert.Equal("sudo -n kill -s TERM 77", SshProcessProvider.BuildSignalCommand(host, 77, ProcessSignal.Terminate));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public void SignalCommand_RefusesInitAndKernel(int pid)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            SshProcessProvider.BuildSignalCommand(new HostConfig(), pid, ProcessSignal.Kill));
    }

    [Fact]
    public async Task SshProcessProvider_SignalRunsKill()
    {
        var runner = new FakeSshCommandRunner("");
        var provider = new SshProcessProvider(runner);

        await provider.SignalAsync(new HostConfig(), 311, ProcessSignal.Kill, CancellationToken.None);

        Assert.Equal("kill -s KILL 311", runner.Commands.Single());
    }

    [Fact]
    public async Task MockProcessProvider_PidsAreStableAcrossCalls()
    {
        var provider = new MockProcessProvider();
        var host = new HostConfig { Id = "stable" };

        var first = await provider.GetTopProcessesAsync(host, 100, CancellationToken.None);
        var second = await provider.GetTopProcessesAsync(host, 100, CancellationToken.None);

        Assert.Equal(first.Select(p => p.Pid).OrderBy(p => p), second.Select(p => p.Pid).OrderBy(p => p));
    }

    [Fact]
    public async Task MockProcessProvider_SignalRemovesProcessAndUnknownPidFails()
    {
        var provider = new MockProcessProvider();
        var host = new HostConfig { Id = "signal" };
        var victim = (await provider.GetTopProcessesAsync(host, 100, CancellationToken.None)).First(p => p.Pid > 1);

        await provider.SignalAsync(host, victim.Pid, ProcessSignal.Terminate, CancellationToken.None);
        var after = await provider.GetTopProcessesAsync(host, 100, CancellationToken.None);

        Assert.DoesNotContain(after, p => p.Pid == victim.Pid);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            provider.SignalAsync(host, victim.Pid, ProcessSignal.Terminate, CancellationToken.None));
    }

    [Theory]
    [InlineData(SystemdAction.Start, "systemctl start -- 'nginx.service'")]
    [InlineData(SystemdAction.Stop, "systemctl stop -- 'nginx.service'")]
    [InlineData(SystemdAction.Restart, "systemctl restart -- 'nginx.service'")]
    public void SystemdActionCommand_QuotesUnit(SystemdAction action, string expected)
    {
        Assert.Equal(expected, SshSystemdProvider.BuildActionCommand(new HostConfig(), action, "nginx.service"));
    }

    [Fact]
    public void SystemdActionCommand_WithSudoAndHostileName_StaysQuoted()
    {
        var command = SshSystemdProvider.BuildActionCommand(new HostConfig { UseSudo = true }, SystemdAction.Restart, "x'; reboot");
        Assert.Equal("sudo -n systemctl restart -- 'x'\\''; reboot'", command);
    }

    [Fact]
    public async Task SshSystemdProvider_ExecuteRunsSystemctl()
    {
        var runner = new FakeSshCommandRunner("");
        var provider = new SshSystemdProvider(runner);

        await provider.ExecuteAsync(new HostConfig(), SystemdAction.Restart, "ssh.service", CancellationToken.None);

        Assert.Equal("systemctl restart -- 'ssh.service'", runner.Commands.Single());
    }

    [Fact]
    public async Task MockSystemdProvider_TransitionsUnitState()
    {
        var provider = new MockSystemdProvider();
        var host = new HostConfig { Id = "units" };

        await provider.ExecuteAsync(host, SystemdAction.Stop, "nginx.service", CancellationToken.None);
        var stopped = (await provider.GetServicesAsync(host, CancellationToken.None)).Single(u => u.Unit == "nginx.service");
        await provider.ExecuteAsync(host, SystemdAction.Restart, "backup.service", CancellationToken.None);
        var restarted = (await provider.GetServicesAsync(host, CancellationToken.None)).Single(u => u.Unit == "backup.service");
        var otherHost = await provider.GetServicesAsync(new HostConfig { Id = "other" }, CancellationToken.None);

        Assert.Equal("inactive", stopped.Active);
        Assert.True(restarted.IsRunning);
        Assert.True(otherHost.Single(u => u.Unit == "nginx.service").IsRunning);
    }

    [Theory]
    [InlineData("~/.ssh/id_ed25519", "/home/demo/.ssh/id_ed25519")]
    [InlineData("~", "/home/demo")]
    [InlineData("/etc/ssh/key", "/etc/ssh/key")]
    [InlineData("~other/key", "~other/key")]
    public void ExpandHome_ResolvesTildePrefix(string path, string expected)
    {
        Assert.Equal(expected, ShellArgs.ExpandHome(path, "/home/demo").Replace('\\', '/'));
    }
}
