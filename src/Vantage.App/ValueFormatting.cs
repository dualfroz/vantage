using System.Globalization;

namespace Vantage.App;

public static class ValueFormatting
{
    private static readonly string[] ByteUnits = { "B", "KiB", "MiB", "GiB", "TiB", "PiB" };

    public static string Bytes(long bytes)
    {
        if (bytes <= 0)
            return "0 B";

        double value = bytes;
        var unit = 0;
        while (value >= 1024 && unit < ByteUnits.Length - 1)
        {
            value /= 1024;
            unit++;
        }

        var format = value >= 100 || unit == 0 ? "0" : "0.0";
        return value.ToString(format, CultureInfo.InvariantCulture) + " " + ByteUnits[unit];
    }

    public static string BytesPair(long used, long total) => $"{Bytes(used)} / {Bytes(total)}";

    public static string Rate(double bytesPerSecond) =>
        Bytes((long)Math.Round(Math.Max(0, bytesPerSecond))) + "/s";

    public static string Count(long value) => value.ToString("N0", CultureInfo.InvariantCulture);

    public static string Celsius(double value) => value.ToString("0.0", CultureInfo.InvariantCulture) + " °C";

    public static string Percent(double value) => value.ToString("0.0", CultureInfo.InvariantCulture) + " %";

    public static string Uptime(TimeSpan uptime)
    {
        if (uptime.TotalDays >= 1)
            return $"{(int)uptime.TotalDays}d {uptime.Hours}h {uptime.Minutes}m";
        if (uptime.TotalHours >= 1)
            return $"{uptime.Hours}h {uptime.Minutes}m";
        return $"{uptime.Minutes}m {uptime.Seconds}s";
    }
}
