using System.Net;
using Vantage.Core.Hosts;
using Vantage.Core.Models;
using Vantage.Core.Ssh;
using Vantage.Core.Tests.Fakes;
using Vantage.Core.Tests.Fixtures;
using Xunit;

namespace Vantage.Core.Tests.Hosts;

public class ConnectionTesterTests
{
    private static HostConfig Host(bool useSudo = false) => new()
    {
        Name = "edge",
        Address = "192.0.2.10",
        Username = "ops",
        PrivateKeyPath = "~/.ssh/id_ed25519",
        UseSudo = useSudo
    };

    private sealed class ThrowingRunner : ISshCommandRunner
    {
        public Task<string> RunAsync(HostConfig host, string command, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("Permission denied (publickey).");
    }

    private sealed class HangingRunner : ISshCommandRunner
    {
        public Task<string> RunAsync(HostConfig host, string command, CancellationToken cancellationToken) =>
            new TaskCompletionSource<string>().Task;
    }

    [Fact]
    public async Task Ssh_Success_ReportsUnameOutput()
    {
        var runner = new FakeSshCommandRunner("Linux 6.8.0-45-generic x86_64\n");
        var tester = new SshHostConnectionTester(runner);

        var result = await tester.TestAsync(Host(), CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.False(result.Simulated);
        var step = Assert.Single(result.Steps);
        Assert.Equal("SSH", step.Name);
        Assert.Equal("Linux 6.8.0-45-generic x86_64", step.Detail);
        Assert.NotNull(step.LatencyMs);
        Assert.Equal(SshHostConnectionTester.ProbeCommand, runner.Commands.Single());
    }

    [Fact]
    public async Task Ssh_Failure_ReportsRunnerMessage()
    {
        var tester = new SshHostConnectionTester(new ThrowingRunner());

        var result = await tester.TestAsync(Host(), CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Equal("Permission denied (publickey).", result.Steps.Single().Detail);
    }

    private sealed class HostKeyRejectingRunner : ISshCommandRunner
    {
        private readonly HostKeyStatus status;

        public HostKeyRejectingRunner(HostKeyStatus status)
        {
            this.status = status;
        }

        public Task<string> RunAsync(HostConfig host, string command, CancellationToken cancellationToken)
        {
            var presented = HostKeyCandidate.From(host.Address, host.Port, HostKeyFixtures.Blob(HostKeyFixtures.Ed25519));
            var conflicts = status == HostKeyStatus.Changed
                ? new[] { new KnownHostMatch("ssh-ed25519", HostKeyFixtures.OtherEd25519Fingerprint, "known_hosts", 3) }
                : Array.Empty<KnownHostMatch>();
            throw new HostKeyVerificationException(new HostKeyCheck(status, presented, conflicts));
        }
    }

    [Fact]
    public async Task Ssh_UnknownHostKey_OffersTheKeyForTrust()
    {
        var runner = new HostKeyRejectingRunner(HostKeyStatus.Unknown);
        var tester = new SshHostConnectionTester(runner);

        var result = await tester.TestAsync(Host(useSudo: true), CancellationToken.None);

        Assert.False(result.Succeeded);
        var step = Assert.Single(result.Steps);
        Assert.Equal("Host key", step.Name);
        Assert.Contains("192.0.2.10:22 is not known", step.Detail);
        Assert.Equal(HostKeyFixtures.Ed25519Fingerprint, result.UnknownHostKey!.Fingerprint);
    }

    [Fact]
    public async Task Ssh_ChangedHostKey_FailsWithoutOfferingTrust()
    {
        var tester = new SshHostConnectionTester(new HostKeyRejectingRunner(HostKeyStatus.Changed));

        var result = await tester.TestAsync(Host(), CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Null(result.UnknownHostKey);
        var detail = result.Steps.Single().Detail;
        Assert.StartsWith("HOST KEY CHANGED", detail);
        Assert.Contains(HostKeyFixtures.Ed25519Fingerprint, detail);
        Assert.Contains(HostKeyFixtures.OtherEd25519Fingerprint, detail);
    }

    [Fact]
    public async Task Ssh_NoAnswer_TimesOut()
    {
        var tester = new SshHostConnectionTester(new HangingRunner(), timeout: TimeSpan.FromMilliseconds(50));

        var result = await tester.TestAsync(Host(), CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.StartsWith("No answer within", result.Steps.Single().Detail);
    }

    [Fact]
    public async Task UseSudo_AddsPasswordlessSudoCheck()
    {
        var runner = new FakeSshCommandRunner("Linux\n");
        var tester = new SshHostConnectionTester(runner);

        var result = await tester.TestAsync(Host(useSudo: true), CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.Equal(new[] { "SSH", "sudo" }, result.Steps.Select(s => s.Name));
        Assert.Equal("sudo -n true", runner.Commands[1]);
    }

    [Fact]
    public async Task MetricsEndpoint_IsProbedWhenClientGiven()
    {
        var handler = StubHttpMessageHandler.WithStatus(HttpStatusCode.ServiceUnavailable);
        var tester = new SshHostConnectionTester(new FakeSshCommandRunner("Linux\n"), new HttpClient(handler));

        var result = await tester.TestAsync(Host(), CancellationToken.None);

        Assert.False(result.Succeeded);
        var metrics = result.Steps.Single(s => s.Name == "Metrics endpoint");
        Assert.False(metrics.Succeeded);
        Assert.Equal("http://192.0.2.10:9100/metrics answered HTTP 503", metrics.Detail);
    }

    [Fact]
    public async Task InvalidSettings_AreReportedWithoutConnecting()
    {
        var runner = new FakeSshCommandRunner("unused");
        var tester = new SshHostConnectionTester(runner);

        var result = await tester.TestAsync(new HostConfig(), CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Empty(runner.Commands);
        Assert.Contains(result.Steps, s => s.Detail == "Address is required.");
    }

    [Fact]
    public async Task Mock_ValidSettings_AreSimulatedNotConnected()
    {
        var result = await new MockHostConnectionTester().TestAsync(Host(), CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.True(result.Simulated);
        Assert.Equal(MockHostConnectionTester.SimulatedDetail, result.Steps.Single().Detail);
    }

    [Fact]
    public async Task Mock_InvalidSettings_Fail()
    {
        var result = await new MockHostConnectionTester().TestAsync(new HostConfig(), CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.False(result.Simulated);
    }
}
