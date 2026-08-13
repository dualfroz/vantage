using System.Globalization;

namespace Vantage.Core.Collectors.Network;

public static class NetDevParser
{
    public static IReadOnlyList<InterfaceCounters> Parse(string procNetDev)
    {
        var interfaces = new List<InterfaceCounters>();

        foreach (var rawLine in procNetDev.Replace("\r\n", "\n").Split('\n'))
        {
            var colon = rawLine.IndexOf(':');
            if (colon <= 0)
                continue;

            var name = rawLine[..colon].Trim();
            if (name.Length == 0 || name.Contains('|'))
                continue;

            var fields = rawLine[(colon + 1)..]
                .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (fields.Length < 16)
                continue;

            interfaces.Add(new InterfaceCounters(
                Name: name,
                RxBytes: ParseLong(fields[0]),
                RxPackets: ParseLong(fields[1]),
                TxBytes: ParseLong(fields[8]),
                TxPackets: ParseLong(fields[9])));
        }

        return interfaces;
    }

    private static long ParseLong(string value) => long.Parse(value, CultureInfo.InvariantCulture);
}
