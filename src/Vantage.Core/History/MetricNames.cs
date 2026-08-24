namespace Vantage.Core.History;

public static class MetricNames
{
    public const string Cpu = "cpu";
    public const string Memory = "memory";
    public const string Disk = "disk";
    public const string Load1 = "load1";
    public const string Load5 = "load5";
    public const string Load15 = "load15";
    public const string NetworkRx = "networkRx";
    public const string NetworkTx = "networkTx";

    public static readonly IReadOnlyList<string> All = new[]
    {
        Cpu, Memory, Disk, Load1, Load5, Load15, NetworkRx, NetworkTx
    };
}
