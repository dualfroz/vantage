namespace Vantage.Core.Collectors.Network;

public readonly record struct InterfaceThroughput(string Name, double RxBytesPerSecond, double TxBytesPerSecond);

public static class NetworkThroughput
{
    public static IReadOnlyList<InterfaceThroughput> Between(NetworkSnapshot previous, NetworkSnapshot current)
    {
        var elapsed = (current.CollectedAt - previous.CollectedAt).TotalSeconds;
        if (elapsed <= 0)
            return Array.Empty<InterfaceThroughput>();

        var rates = new List<InterfaceThroughput>();
        foreach (var now in current.Interfaces)
        {
            var before = previous.Find(now.Name);
            if (before is null)
                continue;

            rates.Add(new InterfaceThroughput(
                now.Name,
                Rate(before.Value.RxBytes, now.RxBytes, elapsed),
                Rate(before.Value.TxBytes, now.TxBytes, elapsed)));
        }

        return rates;
    }

    private static double Rate(long before, long after, double elapsedSeconds)
    {
        var delta = after - before;
        if (delta < 0)
            return 0;
        return delta / elapsedSeconds;
    }
}
