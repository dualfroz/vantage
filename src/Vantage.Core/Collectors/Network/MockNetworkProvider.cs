using System.Collections.Concurrent;
using Vantage.Core.Models;

namespace Vantage.Core.Collectors.Network;

public sealed class MockNetworkProvider : INetworkProvider
{
    private sealed class Accumulator
    {
        public long Rx;
        public long Tx;
        public long RxPackets;
        public long TxPackets;
        public long DockerRx = 610_000_000;
        public long DockerTx = 1_450_000_000;
        public long TunnelRx = 82_000_000;
        public long TunnelTx = 37_000_000;
        public DateTimeOffset LastAt;
        public Random Jitter = new();
    }

    private readonly ConcurrentDictionary<string, Accumulator> state = new();
    private readonly Func<DateTimeOffset> clock;

    public MockNetworkProvider(Func<DateTimeOffset>? clock = null)
    {
        this.clock = clock ?? (() => DateTimeOffset.UtcNow);
    }

    public Task<NetworkSnapshot> GetSnapshotAsync(HostConfig host, CancellationToken cancellationToken)
    {
        var now = clock();
        var accumulator = state.GetOrAdd(host.Id, _ => new Accumulator
        {
            Rx = 4_000_000_000,
            Tx = 900_000_000,
            LastAt = now
        });

        var seconds = Math.Max(0, (now - accumulator.LastAt).TotalSeconds);
        var rxRate = 200_000 + accumulator.Jitter.NextDouble() * 2_000_000;
        var txRate = 80_000 + accumulator.Jitter.NextDouble() * 500_000;

        accumulator.Rx += (long)(rxRate * seconds);
        accumulator.Tx += (long)(txRate * seconds);
        accumulator.RxPackets += (long)(rxRate * seconds / 1400);
        accumulator.TxPackets += (long)(txRate * seconds / 1400);
        accumulator.DockerRx += (long)(txRate * 0.6 * seconds);
        accumulator.DockerTx += (long)(rxRate * 0.7 * seconds);
        accumulator.TunnelRx += (long)((5_000 + accumulator.Jitter.NextDouble() * 40_000) * seconds);
        accumulator.TunnelTx += (long)((2_000 + accumulator.Jitter.NextDouble() * 15_000) * seconds);
        accumulator.LastAt = now;

        var interfaces = new[]
        {
            new InterfaceCounters("lo", 12_000_000, 12_000_000, 40_000, 40_000),
            new InterfaceCounters("eth0", accumulator.Rx, accumulator.Tx, accumulator.RxPackets, accumulator.TxPackets),
            new InterfaceCounters("docker0", accumulator.DockerRx, accumulator.DockerTx, accumulator.DockerRx / 900, accumulator.DockerTx / 900),
            new InterfaceCounters("wg0", accumulator.TunnelRx, accumulator.TunnelTx, accumulator.TunnelRx / 600, accumulator.TunnelTx / 600)
        };

        return Task.FromResult(new NetworkSnapshot(now, interfaces));
    }
}
