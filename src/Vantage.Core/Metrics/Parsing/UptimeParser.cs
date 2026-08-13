using System.Globalization;

namespace Vantage.Core.Metrics.Parsing;

public static class UptimeParser
{
    public static TimeSpan Parse(string procUptime)
    {
        var fields = procUptime.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (fields.Length < 1)
            throw new FormatException($"Unexpected /proc/uptime content: '{procUptime}'");

        var seconds = double.Parse(fields[0], CultureInfo.InvariantCulture);
        return TimeSpan.FromSeconds(seconds);
    }
}
