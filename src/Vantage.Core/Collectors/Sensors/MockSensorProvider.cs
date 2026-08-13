using Vantage.Core.Models;

namespace Vantage.Core.Collectors.Sensors;

public sealed class MockSensorProvider : ISensorProvider
{
    private readonly ConcurrentProfiles profiles = new();
    private readonly Func<DateTimeOffset> clock;

    public MockSensorProvider(Func<DateTimeOffset>? clock = null)
    {
        this.clock = clock ?? (() => DateTimeOffset.UtcNow);
    }

    public Task<SensorSnapshot> GetSnapshotAsync(HostConfig host, CancellationToken cancellationToken)
    {
        var seconds = clock().ToUnixTimeSeconds();
        var wave = Math.Sin(seconds / 120.0) * 6;

        var temperatures = new[]
        {
            new TemperatureReading("x86_pkg_temp", Math.Round(48 + wave, 1)),
            new TemperatureReading("coretemp Core 0", Math.Round(46 + wave * 0.8, 1)),
            new TemperatureReading("nvme Composite", Math.Round(38 + wave * 0.4, 1)),
            new TemperatureReading("acpitz", Math.Round(41 + wave * 0.5, 1))
        };

        var gpus = profiles.HasGpu(host.Id)
            ? new[]
            {
                new GpuReading(
                    "NVIDIA GeForce RTX 3060",
                    Math.Round(52 + wave, 1),
                    Math.Round(Math.Clamp(30 + wave * 4, 0, 100), 1),
                    3_200L * 1024 * 1024,
                    12_288L * 1024 * 1024)
            }
            : Array.Empty<GpuReading>();

        return Task.FromResult(new SensorSnapshot(temperatures, gpus));
    }

    private sealed class ConcurrentProfiles
    {
        public bool HasGpu(string hostId)
        {
            var hash = 0;
            foreach (var c in hostId)
                hash = hash * 31 + c;
            return (hash & 1) == 0;
        }
    }
}
