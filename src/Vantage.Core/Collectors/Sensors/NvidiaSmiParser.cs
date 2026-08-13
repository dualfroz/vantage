using System.Globalization;

namespace Vantage.Core.Collectors.Sensors;

public static class NvidiaSmiParser
{
    public const string QueryArgs =
        "--query-gpu=name,temperature.gpu,utilization.gpu,memory.used,memory.total " +
        "--format=csv,noheader,nounits";

    public static IReadOnlyList<GpuReading> Parse(string csvOutput)
    {
        var gpus = new List<GpuReading>();

        foreach (var rawLine in csvOutput.Replace("\r\n", "\n").Split('\n'))
        {
            var line = rawLine.Trim();
            if (line.Length == 0)
                continue;

            var fields = line.Split(',', StringSplitOptions.TrimEntries);
            if (fields.Length < 5)
                continue;

            gpus.Add(new GpuReading(
                Name: fields[0],
                TemperatureCelsius: ParseDouble(fields[1]),
                UtilizationPercent: ParseDouble(fields[2]),
                MemoryUsedBytes: (long)(ParseDouble(fields[3]) * 1024 * 1024),
                MemoryTotalBytes: (long)(ParseDouble(fields[4]) * 1024 * 1024)));
        }

        return gpus;
    }

    private static double ParseDouble(string value) =>
        double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed) ? parsed : 0;
}
