using Vantage.Core.Models;

namespace Vantage.Core.Prometheus;

public readonly record struct CpuCounters(double IdleSeconds, double TotalSeconds);

public static class PrometheusNodeMapper
{
    public static CpuCounters ExtractCpuCounters(IReadOnlyList<PrometheusSample> samples)
    {
        double idle = 0;
        double total = 0;

        foreach (var sample in samples)
        {
            if (sample.Name != "node_cpu_seconds_total")
                continue;

            total += sample.Value;
            if (sample.HasLabel("mode", "idle"))
                idle += sample.Value;
        }

        return new CpuCounters(idle, total);
    }

    public static double CpuPercent(CpuCounters previous, CpuCounters current)
    {
        var totalDelta = current.TotalSeconds - previous.TotalSeconds;
        var idleDelta = current.IdleSeconds - previous.IdleSeconds;
        if (totalDelta <= 0)
            return 0;

        var busy = (totalDelta - idleDelta) / totalDelta * 100.0;
        return Math.Clamp(Math.Round(busy, 1), 0, 100);
    }

    public static HostMetrics Map(
        IReadOnlyList<PrometheusSample> samples,
        string diskMount,
        double cpuPercent,
        DateTimeOffset now)
    {
        var memTotal = (long)First(samples, "node_memory_MemTotal_bytes");
        var memAvailable = (long)First(samples, "node_memory_MemAvailable_bytes");
        var memUsed = Math.Max(0, memTotal - memAvailable);

        var diskSize = (long)FirstForMount(samples, "node_filesystem_size_bytes", diskMount);
        var diskAvail = (long)FirstForMount(samples, "node_filesystem_avail_bytes", diskMount);
        var diskUsed = Math.Max(0, diskSize - diskAvail);

        var uptimeSeconds = First(samples, "node_time_seconds") - First(samples, "node_boot_time_seconds");
        var uptime = uptimeSeconds > 0 ? TimeSpan.FromSeconds(uptimeSeconds) : TimeSpan.Zero;

        return new HostMetrics
        {
            CpuUsagePercent = cpuPercent,
            MemoryUsedBytes = memUsed,
            MemoryTotalBytes = memTotal,
            DiskUsedBytes = diskUsed,
            DiskTotalBytes = diskSize,
            Load1 = First(samples, "node_load1"),
            Load5 = First(samples, "node_load5"),
            Load15 = First(samples, "node_load15"),
            Uptime = uptime,
            CollectedAt = now
        };
    }

    private static double First(IReadOnlyList<PrometheusSample> samples, string name)
    {
        foreach (var sample in samples)
            if (sample.Name == name)
                return sample.Value;
        return 0;
    }

    private static double FirstForMount(IReadOnlyList<PrometheusSample> samples, string name, string mount)
    {
        double? fallback = null;
        foreach (var sample in samples)
        {
            if (sample.Name != name)
                continue;
            if (sample.HasLabel("mountpoint", mount))
                return sample.Value;
            fallback ??= sample.Value;
        }

        return fallback ?? 0;
    }
}
