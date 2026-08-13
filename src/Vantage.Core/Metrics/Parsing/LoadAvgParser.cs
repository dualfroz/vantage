using System.Globalization;

namespace Vantage.Core.Metrics.Parsing;

public readonly record struct LoadAverage(double Load1, double Load5, double Load15);

public static class LoadAvgParser
{
    public static LoadAverage Parse(string procLoadavg)
    {
        var fields = procLoadavg.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (fields.Length < 3)
            throw new FormatException($"Unexpected /proc/loadavg content: '{procLoadavg}'");

        return new LoadAverage(
            double.Parse(fields[0], CultureInfo.InvariantCulture),
            double.Parse(fields[1], CultureInfo.InvariantCulture),
            double.Parse(fields[2], CultureInfo.InvariantCulture));
    }
}
