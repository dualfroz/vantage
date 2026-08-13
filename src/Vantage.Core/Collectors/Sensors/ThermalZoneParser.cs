using System.Globalization;

namespace Vantage.Core.Collectors.Sensors;

public static class ThermalZoneParser
{
    public static IReadOnlyList<TemperatureReading> Parse(string zoneOutput)
    {
        var readings = new List<TemperatureReading>();

        foreach (var rawLine in zoneOutput.Replace("\r\n", "\n").Split('\n'))
        {
            var line = rawLine.Trim();
            if (line.Length == 0)
                continue;

            var separator = line.LastIndexOf(':');
            if (separator <= 0 || separator == line.Length - 1)
                continue;

            var label = line[..separator].Trim();
            var rawValue = line[(separator + 1)..].Trim();
            if (!long.TryParse(rawValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out var milliCelsius))
                continue;

            readings.Add(new TemperatureReading(label, Math.Round(milliCelsius / 1000.0, 1)));
        }

        return readings;
    }
}
