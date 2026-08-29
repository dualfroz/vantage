using Vantage.Core.Collectors.Sensors;
using Vantage.Core.Models;
using Vantage.Core.Tests.Fakes;
using Vantage.Core.Tests.Fixtures;
using Xunit;

namespace Vantage.Core.Tests.Collectors;

public class SensorsTests
{
    [Fact]
    public void ThermalParser_ConvertsMilliDegreesToCelsius()
    {
        var readings = ThermalZoneParser.Parse(CollectorFixtures.ThermalZones);

        Assert.Equal(4, readings.Count);
        Assert.Equal(52.0, readings.Single(r => r.Label == "x86_pkg_temp").Celsius, 3);
        Assert.Equal(48.0, readings.Single(r => r.Label == "coretemp Core 0").Celsius, 3);
    }

    [Fact]
    public void NvidiaSmiParser_ParsesGpuRow()
    {
        var gpus = NvidiaSmiParser.Parse(CollectorFixtures.NvidiaSmiCsv);

        var gpu = Assert.Single(gpus);
        Assert.Equal("NVIDIA GeForce RTX 3060", gpu.Name);
        Assert.Equal(52, gpu.TemperatureCelsius, 3);
        Assert.Equal(34, gpu.UtilizationPercent, 3);
        Assert.Equal(3200L * 1024 * 1024, gpu.MemoryUsedBytes);
        Assert.Equal(12288L * 1024 * 1024, gpu.MemoryTotalBytes);
    }

    [Fact]
    public void ParseBundle_SplitsThermalFromGpu()
    {
        var snapshot = SshSensorProvider.ParseBundle(CollectorFixtures.SensorBundle());

        Assert.Equal(4, snapshot.Temperatures.Count);
        Assert.Single(snapshot.Gpus);
        Assert.Equal(52.0, snapshot.HottestCelsius);
    }

    [Fact]
    public void ParseBundle_WithoutGpu_ReturnsEmptyGpuList()
    {
        var snapshot = SshSensorProvider.ParseBundle(CollectorFixtures.ThermalZones + "@@vantage:gpu\n");

        Assert.Empty(snapshot.Gpus);
        Assert.Equal(4, snapshot.Temperatures.Count);
    }

    [Fact]
    public async Task SshSensorProvider_BuildsThermalAndGpuCommand()
    {
        var runner = new FakeSshCommandRunner(CollectorFixtures.SensorBundle());
        var provider = new SshSensorProvider(runner);

        var snapshot = await provider.GetSnapshotAsync(new HostConfig(), CancellationToken.None);

        Assert.Contains("thermal_zone", runner.Commands.Single());
        Assert.Contains("nvidia-smi", runner.Commands.Single());
        Assert.Single(snapshot.Gpus);
    }
}
