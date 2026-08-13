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
        accumulator.LastAt = now;

        var interfaces = new[]
        {
            new InterfaceCounters("lo", 12_000_000, 12_000_000, 40_000, 40_000),
            new InterfaceCounters("eth0", accumulator.Rx, accumulator.Tx, accumulator.RxPackets, accumulator.TxPackets)
        };

        return Task.FromResult(new NetworkSnapshot(now, interfaces));
    }
}
