namespace Vantage.Core.Collectors.Sensors;

public readonly record struct TemperatureReading(string Label, double Celsius);

public readonly record struct GpuReading(
    string Name,
    double TemperatureCelsius,
    double UtilizationPercent,
    long MemoryUsedBytes,
    long MemoryTotalBytes);

public sealed record SensorSnapshot(
    IReadOnlyList<TemperatureReading> Temperatures,
    IReadOnlyList<GpuReading> Gpus)
{
    public static readonly SensorSnapshot Empty =
        new(Array.Empty<TemperatureReading>(), Array.Empty<GpuReading>());

    public double? HottestCelsius => Temperatures.Count == 0 ? null : Temperatures.Max(t => t.Celsius);
}
