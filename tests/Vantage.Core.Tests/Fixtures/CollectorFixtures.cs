namespace Vantage.Core.Tests.Fixtures;

public static class CollectorFixtures
{
    public const string ProcNetDev =
        "Inter-|   Receive                                                |  Transmit\n" +
        " face |bytes    packets errs drop fifo frame compressed multicast|bytes    packets errs drop fifo colls carrier compressed\n" +
        "    lo: 1234567    8901    0    0    0     0          0         0  1234567    8901    0    0    0     0       0          0\n" +
        "  eth0: 9000000000 1200000    0    0    0     0          0         0  2000000000  980000    0    0    0     0       0          0\n" +
        "  wlan0:      0          0    0    0    0     0          0         0        0          0    0    0    0     0       0          0\n";

    public const string ProcNetDevLater =
        "Inter-|   Receive                                                |  Transmit\n" +
        " face |bytes    packets errs drop fifo frame compressed multicast|bytes    packets errs drop fifo colls carrier compressed\n" +
        "    lo: 1234567    8901    0    0    0     0          0         0  1234567    8901    0    0    0     0       0          0\n" +
        "  eth0: 9000010000 1200100    0    0    0     0          0         0  2000004000  980040    0    0    0     0       0          0\n";

    public const string PsTopOutput =
        "  842 node     12.6  6.3 node server.js\n" +
        "  311 root      5.4  2.1 dockerd\n" +
        " 1290 postgres  3.1  8.7 postgres\n" +
        "    1 root      0.3  0.4 systemd\n" +
        "  777 www-data  1.2  0.9 nginx\n";

    public const string SystemctlOutput =
        "docker.service loaded active running Docker Application Container Engine\n" +
        "nginx.service loaded active running A high performance web server\n" +
        "\u25cf backup.service loaded failed failed Nightly backup job\n" +
        "systemd-journald.service loaded active running Journal Service\n" +
        "not-a-service.timer loaded active waiting Some timer\n";

    public const string ThermalZones =
        "x86_pkg_temp:52000\n" +
        "coretemp Core 0:48000\n" +
        "nvme Composite:38900\n" +
        "acpitz:41000\n";

    public const string NvidiaSmiCsv =
        "NVIDIA GeForce RTX 3060, 52, 34, 3200, 12288\n";

    public static string SensorBundle() =>
        ThermalZones + SshSensorGpuMarker + "\n" + NvidiaSmiCsv;

    public const string SshSensorGpuMarker = "@@vantage:gpu";

    public const string JournalOutput =
        "2026-08-26T14:22:01+0200 vps01 nginx[1234]: GET /api/status 200 12ms\n" +
        "2026-08-26T14:22:03+0200 vps01 nginx[1234]: GET /health 200 3ms\n" +
        "2026-08-26T14:22:05+0200 vps01 sshd[9911]: Accepted publickey for foksiu\n";
}
