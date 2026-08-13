using System.Globalization;

namespace Vantage.Core.Metrics.Parsing;

public readonly record struct MemoryInfo(long UsedBytes, long TotalBytes);

public static class MemInfoParser
{
    public static MemoryInfo Parse(string procMeminfo)
    {
        long? totalKb = null;
        long? availableKb = null;
        long? freeKb = null;

        foreach (var rawLine in procMeminfo.Split('\n'))
        {
            var line = rawLine.Trim();
            if (line.Length == 0)
                continue;

            var colon = line.IndexOf(':');
            if (colon <= 0)
                continue;

            var key = line[..colon].Trim();
            var value = ParseKilobytes(line[(colon + 1)..]);

            switch (key)
            {
                case "MemTotal":
                    totalKb = value;
                    break;
                case "MemAvailable":
                    availableKb = value;
                    break;
                case "MemFree":
                    freeKb = value;
                    break;
            }
        }

        if (totalKb is null)
            throw new FormatException("MemTotal not found in /proc/meminfo");

        var availabilityKb = availableKb ?? freeKb ?? 0;
        var usedKb = Math.Max(0, totalKb.Value - availabilityKb);

        return new MemoryInfo(usedKb * 1024, totalKb.Value * 1024);
    }

    private static long ParseKilobytes(string valueWithUnit)
    {
        var fields = valueWithUnit.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return long.Parse(fields[0], CultureInfo.InvariantCulture);
    }
}
