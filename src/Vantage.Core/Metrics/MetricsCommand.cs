namespace Vantage.Core.Metrics;

public static class MetricsCommand
{
    public const string MarkerPrefix = "@@vantage:";
    public const string LoadAvg = "loadavg";
    public const string Uptime = "uptime";
    public const string MemInfo = "meminfo";
    public const string Df = "df";
    public const string Stat1 = "stat1";
    public const string Stat2 = "stat2";

    public static string Build(string diskMount, int cpuSampleMs = 400)
    {
        var mount = Ssh.ShellArgs.Quote(diskMount);
        var sleepSeconds = (cpuSampleMs / 1000.0).ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);
        return string.Join(" ; ", new[]
        {
            Emit(LoadAvg) + "cat /proc/loadavg",
            Emit(Uptime) + "cat /proc/uptime",
            Emit(MemInfo) + "cat /proc/meminfo",
            Emit(Df) + $"df -B1 -P {mount}",
            Emit(Stat1) + "cat /proc/stat",
            $"sleep {sleepSeconds}",
            Emit(Stat2) + "cat /proc/stat"
        });
    }

    private static string Emit(string section) => $"echo '{MarkerPrefix}{section}' ; ";
}
