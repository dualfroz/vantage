using System.Globalization;

namespace Vantage.Core.Metrics.Parsing;

public readonly record struct DiskUsage(long UsedBytes, long TotalBytes);

public static class DfParser
{
    public static DiskUsage Parse(string dfOutput)
    {
        var lines = dfOutput
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(l => l.Trim())
            .Where(l => l.Length > 0)
            .ToArray();

        if (lines.Length < 2)
            throw new FormatException("df output has no data row");

        var dataLine = lines[^1];
        var fields = dataLine.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (fields.Length < 5)
            throw new FormatException($"Unexpected df row: '{dataLine}'");

        var totalBytes = long.Parse(fields[^5], CultureInfo.InvariantCulture);
        var usedBytes = long.Parse(fields[^4], CultureInfo.InvariantCulture);

        return new DiskUsage(usedBytes, totalBytes);
    }
}
