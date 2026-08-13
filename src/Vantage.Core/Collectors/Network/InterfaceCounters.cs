namespace Vantage.Core.Collectors.Network;

public readonly record struct InterfaceCounters(
    string Name,
    long RxBytes,
    long TxBytes,
    long RxPackets,
    long TxPackets)
{
    public bool IsLoopback => Name == "lo";
}

public sealed record NetworkSnapshot(DateTimeOffset CollectedAt, IReadOnlyList<InterfaceCounters> Interfaces)
{
    public InterfaceCounters? Find(string name)
    {
        foreach (var counters in Interfaces)
            if (counters.Name == name)
                return counters;
        return null;
    }

    public long TotalRxBytes => Interfaces.Where(i => !i.IsLoopback).Sum(i => i.RxBytes);
    public long TotalTxBytes => Interfaces.Where(i => !i.IsLoopback).Sum(i => i.TxBytes);
}
