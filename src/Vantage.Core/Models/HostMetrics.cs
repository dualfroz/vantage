namespace Vantage.Core.Models;

public sealed record HostMetrics
{
    public double CpuUsagePercent { get; init; }
    public long MemoryUsedBytes { get; init; }
    public long MemoryTotalBytes { get; init; }
    public long DiskUsedBytes { get; init; }
    public long DiskTotalBytes { get; init; }
    public double Load1 { get; init; }
    public double Load5 { get; init; }
    public double Load15 { get; init; }
    public TimeSpan Uptime { get; init; }
    public DateTimeOffset CollectedAt { get; init; } = DateTimeOffset.UtcNow;

    public double MemoryUsedPercent =>
        MemoryTotalBytes <= 0 ? 0 : Math.Round(100.0 * MemoryUsedBytes / MemoryTotalBytes, 1);

    public double DiskUsedPercent =>
        DiskTotalBytes <= 0 ? 0 : Math.Round(100.0 * DiskUsedBytes / DiskTotalBytes, 1);
}
