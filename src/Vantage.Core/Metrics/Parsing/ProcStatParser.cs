using System.Globalization;

namespace Vantage.Core.Metrics.Parsing;

public readonly record struct CpuTimes(long IdleTime, long TotalTime);

public static class ProcStatParser
{
    public static CpuTimes Parse(string procStat)
    {
        foreach (var rawLine in procStat.Split('\n'))
        {
            var line = rawLine.Trim();
            if (!line.StartsWith("cpu ", StringComparison.Ordinal) && line != "cpu")
                continue;

            var fields = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            long total = 0;
            long idle = 0;
            for (var i = 1; i < fields.Length; i++)
            {
                var value = long.Parse(fields[i], CultureInfo.InvariantCulture);
                total += value;
                if (i == 4 || i == 5)
                    idle += value;
            }

            return new CpuTimes(idle, total);
        }

        throw new FormatException("Aggregate 'cpu' line not found in /proc/stat");
    }
}
