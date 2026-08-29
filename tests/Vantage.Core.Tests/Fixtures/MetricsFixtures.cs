namespace Vantage.Core.Tests.Fixtures;

public static class MetricsFixtures
{
    public const string Loadavg = "0.50 0.40 0.30 1/234 5678";

    public const string Uptime = "123456.78 234567.89";

    public const string Meminfo =
        "MemTotal:        8000000 kB\n" +
        "MemFree:         1000000 kB\n" +
        "MemAvailable:    2000000 kB\n" +
        "Buffers:          100000 kB\n" +
        "Cached:           500000 kB\n";

    public const string Df =
        "Filesystem     1B-blocks         Used   Available Use% Mounted on\n" +
        "/dev/sda1    100000000000  40000000000 60000000000  40% /\n";

    public const string Stat1 =
        "cpu  100 0 100 800 0 0 0 0 0 0\n" +
        "cpu0 50 0 50 400 0 0 0 0 0 0\n" +
        "intr 12345\n";

    public const string Stat2 =
        "cpu  400 0 200 900 0 0 0 0 0 0\n" +
        "cpu0 200 0 100 450 0 0 0 0 0 0\n" +
        "intr 23456\n";

    public static string FullBundle() =>
        "@@vantage:loadavg\n" + Loadavg + "\n" +
        "@@vantage:uptime\n" + Uptime + "\n" +
        "@@vantage:meminfo\n" + Meminfo +
        "@@vantage:df\n" + Df +
        "@@vantage:stat1\n" + Stat1 +
        "@@vantage:stat2\n" + Stat2;
}
