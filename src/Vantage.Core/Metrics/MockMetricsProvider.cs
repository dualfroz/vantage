using System.Collections.Concurrent;
using Vantage.Core.Models;

namespace Vantage.Core.Metrics;

public sealed class MockMetricsProvider : IMetricsProvider
{
    private sealed record HostProfile(
        long MemoryTotalBytes,
        long DiskTotalBytes,
        double CpuPhase,
        double MemoryPhase,
        DateTimeOffset BootedAt,
        Random Jitter);

    private readonly ConcurrentDictionary<string, HostProfile> profiles = new();
    private readonly Func<DateTimeOffset> clock;

    public MockMetricsProvider(Func<DateTimeOffset>? clock = null)
    {
        this.clock = clock ?? (() => DateTimeOffset.UtcNow);
    }

    public Task<HostMetrics> GetMetricsAsync(HostConfig host, CancellationToken cancellationToken)
    {
        var now = clock();
        var profile = profiles.GetOrAdd(host.Id, id => CreateProfile(id, now));
        var elapsed = (now - profile.BootedAt).TotalSeconds;

        var cpu = Oscillate(elapsed, period: 37, phase: profile.CpuPhase, min: 6, max: 82)
            + profile.Jitter.NextDouble() * 6 - 3;
        cpu = Math.Clamp(cpu, 0, 100);

        var memoryFraction = Oscillate(elapsed, period: 91, phase: profile.MemoryPhase, min: 0.28, max: 0.74);
        var memoryUsed = (long)(profile.MemoryTotalBytes * memoryFraction);

        var diskFraction = Math.Clamp(0.41 + elapsed / 1_000_000.0, 0.41, 0.95);
        var diskUsed = (long)(profile.DiskTotalBytes * diskFraction);

        var load1 = Math.Round(cpu / 100.0 * ProcessorCountFor(profile), 2);

        return Task.FromResult(new HostMetrics
        {
            CpuUsagePercent = Math.Round(cpu, 1),
            MemoryUsedBytes = memoryUsed,
            MemoryTotalBytes = profile.MemoryTotalBytes,
            DiskUsedBytes = diskUsed,
            DiskTotalBytes = profile.DiskTotalBytes,
            Load1 = load1,
            Load5 = Math.Round(load1 * 0.85, 2),
            Load15 = Math.Round(load1 * 0.7, 2),
            Uptime = now - profile.BootedAt,
            CollectedAt = now
        });
    }

    private static double ProcessorCountFor(HostProfile profile) =>
        4 + profile.MemoryTotalBytes / (8L * 1024 * 1024 * 1024);

    private static HostProfile CreateProfile(string hostId, DateTimeOffset now)
    {
        var seed = StableSeed(hostId);
        var random = new Random(seed);
        var memGb = new[] { 4L, 8L, 16L, 32L }[random.Next(4)];
        var diskGb = new[] { 128L, 256L, 512L, 1024L }[random.Next(4)];
        var bootDaysAgo = 1 + random.Next(0, 60);

        return new HostProfile(
            MemoryTotalBytes: memGb * 1024 * 1024 * 1024,
            DiskTotalBytes: diskGb * 1024 * 1024 * 1024,
            CpuPhase: random.NextDouble() * Math.PI * 2,
            MemoryPhase: random.NextDouble() * Math.PI * 2,
            BootedAt: now.AddDays(-bootDaysAgo).AddMinutes(-random.Next(0, 1440)),
            Jitter: random);
    }

    private static double Oscillate(double elapsedSeconds, double period, double phase, double min, double max)
    {
        var swing = (Math.Sin(elapsedSeconds / period * Math.PI * 2 + phase) + 1) / 2.0;
        return min + swing * (max - min);
    }

    private static int StableSeed(string value)
    {
        unchecked
        {
            var hash = 17;
            foreach (var c in value)
                hash = hash * 31 + c;
            return hash;
        }
    }
}
