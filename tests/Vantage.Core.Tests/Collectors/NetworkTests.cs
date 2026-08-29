using Vantage.Core.Collectors.Network;
using Vantage.Core.Models;
using Vantage.Core.Tests.Fakes;
using Vantage.Core.Tests.Fixtures;
using Xunit;

namespace Vantage.Core.Tests.Collectors;

public class NetworkTests
{
    [Fact]
    public void NetDevParser_ExtractsPerInterfaceCounters()
    {
        var interfaces = NetDevParser.Parse(CollectorFixtures.ProcNetDev);

        Assert.Equal(3, interfaces.Count);
        var eth0 = interfaces.Single(i => i.Name == "eth0");
        Assert.Equal(9_000_000_000, eth0.RxBytes);
        Assert.Equal(2_000_000_000, eth0.TxBytes);
        Assert.Equal(1_200_000, eth0.RxPackets);
        Assert.Equal(980_000, eth0.TxPackets);
        Assert.True(interfaces.Single(i => i.Name == "lo").IsLoopback);
    }

    [Fact]
    public void NetDevParser_SkipsHeaderLines()
    {
        var interfaces = NetDevParser.Parse(CollectorFixtures.ProcNetDev);
        Assert.DoesNotContain(interfaces, i => i.Name.Contains("Inter") || i.Name.Contains("face"));
    }

    [Fact]
    public void Throughput_ComputesBytesPerSecondFromDelta()
    {
        var t0 = new DateTimeOffset(2026, 8, 26, 12, 0, 0, TimeSpan.Zero);
        var before = new NetworkSnapshot(t0, NetDevParser.Parse(CollectorFixtures.ProcNetDev));
        var after = new NetworkSnapshot(t0.AddSeconds(10), NetDevParser.Parse(CollectorFixtures.ProcNetDevLater));

        var rates = NetworkThroughput.Between(before, after);
        var eth0 = rates.Single(r => r.Name == "eth0");

        Assert.Equal(1000, eth0.RxBytesPerSecond, 3);
        Assert.Equal(400, eth0.TxBytesPerSecond, 3);
    }

    [Fact]
    public void Throughput_CounterResetYieldsZeroNotNegative()
    {
        var t0 = new DateTimeOffset(2026, 8, 26, 12, 0, 0, TimeSpan.Zero);
        var before = new NetworkSnapshot(t0, new[] { new InterfaceCounters("eth0", 9_000_000_000, 8_000_000_000, 0, 0) });
        var after = new NetworkSnapshot(t0.AddSeconds(5), new[] { new InterfaceCounters("eth0", 1000, 500, 0, 0) });

        var eth0 = NetworkThroughput.Between(before, after).Single();
        Assert.Equal(0, eth0.RxBytesPerSecond);
        Assert.Equal(0, eth0.TxBytesPerSecond);
    }

    [Fact]
    public async Task SshNetworkProvider_ReadsProcNetDev()
    {
        var runner = new FakeSshCommandRunner(CollectorFixtures.ProcNetDev);
        var provider = new SshNetworkProvider(runner);

        var snapshot = await provider.GetSnapshotAsync(new HostConfig { Id = "h1" }, CancellationToken.None);

        Assert.Equal("cat /proc/net/dev", runner.Commands.Single());
        Assert.Equal(2_000_000_000, snapshot.TotalTxBytes);
    }

    [Fact]
    public async Task MockNetworkProvider_CounterIncreasesOverTime()
    {
        var now = new DateTimeOffset(2026, 8, 26, 12, 0, 0, TimeSpan.Zero);
        var provider = new MockNetworkProvider(() => now);
        var host = new HostConfig { Id = "h1" };

        var first = await provider.GetSnapshotAsync(host, CancellationToken.None);
        now = now.AddSeconds(60);
        var second = await provider.GetSnapshotAsync(host, CancellationToken.None);

        Assert.True(second.Find("eth0")!.Value.RxBytes > first.Find("eth0")!.Value.RxBytes);
    }
}
