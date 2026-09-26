using Vantage.App.Converters;
using Vantage.App.Tests.Fakes;
using Vantage.App.ViewModels;
using Vantage.Core.Collectors.Sensors;
using Vantage.Core.Models;
using Xunit;

namespace Vantage.App.Tests;

public class SensorsViewModelTests
{
    private sealed class FixedSensorProvider : ISensorProvider
    {
        private readonly SensorSnapshot snapshot;

        public FixedSensorProvider(SensorSnapshot snapshot)
        {
            this.snapshot = snapshot;
        }

        public Task<SensorSnapshot> GetSnapshotAsync(HostConfig host, CancellationToken cancellationToken) =>
            Task.FromResult(snapshot);
    }

    private static readonly SensorSnapshot Sample = new(
        new[]
        {
            new TemperatureReading("acpitz", 41),
            new TemperatureReading("x86_pkg_temp", 88),
            new TemperatureReading("nvme Composite", 72)
        },
        new[] { new GpuReading("NVIDIA RTX A2000", 64, 35, 2L * 1024 * 1024 * 1024, 8L * 1024 * 1024 * 1024) });

    [Fact]
    public async Task Refresh_SortsTemperaturesAndAppliesThresholds()
    {
        var viewModel = new SensorsViewModel(new FixedSensorProvider(Sample));
        viewModel.SetThresholds(70, 85);
        viewModel.SetHost(new HostConfig());

        await viewModel.RefreshAsync(CancellationToken.None);

        Assert.Equal(new[] { "x86_pkg_temp", "nvme Composite", "acpitz" }, viewModel.Temperatures.Select(t => t.Label));
        Assert.Equal(StatusTone.Down, viewModel.Temperatures[0].Tone);
        Assert.Equal(StatusTone.Warn, viewModel.Temperatures[1].Tone);
        Assert.Equal(StatusTone.Ok, viewModel.Temperatures[2].Tone);
        Assert.Equal("88.0 °C", viewModel.HottestText);
        Assert.Equal("x86_pkg_temp", viewModel.HottestLabel);
        Assert.Equal(StatusTone.Down, viewModel.HottestTone);
    }

    [Fact]
    public async Task Refresh_FormatsGpuReadings()
    {
        var viewModel = new SensorsViewModel(new FixedSensorProvider(Sample));
        viewModel.SetHost(new HostConfig());

        await viewModel.RefreshAsync(CancellationToken.None);

        var gpu = Assert.Single(viewModel.Gpus);
        Assert.Equal("35.0 %", gpu.UtilizationText);
        Assert.Equal(25, gpu.MemoryPercent, 1);
        Assert.Equal("2.0 GiB / 8.0 GiB", gpu.MemoryText);
        Assert.False(viewModel.HasNoGpus);
    }

    [Fact]
    public async Task EmptySnapshot_ShowsEmptyStates()
    {
        var viewModel = new SensorsViewModel(new FixedSensorProvider(SensorSnapshot.Empty));
        viewModel.SetHost(new HostConfig());

        await viewModel.RefreshAsync(CancellationToken.None);

        Assert.True(viewModel.HasNoTemperatures);
        Assert.True(viewModel.HasNoGpus);
        Assert.Equal("-", viewModel.HottestText);
        Assert.Empty(viewModel.HottestPoints);
    }

    [Fact]
    public async Task EachRefresh_AddsHottestReadingToHistory()
    {
        var clock = new ManualClock();
        var viewModel = new SensorsViewModel(new MockSensorProvider(() => clock.Now));
        viewModel.SetHost(new HostConfig { Id = "sensor-host" });

        await viewModel.RefreshAsync(CancellationToken.None);
        clock.Advance(TimeSpan.FromMinutes(1));
        await viewModel.RefreshAsync(CancellationToken.None);

        Assert.Equal(2, viewModel.HottestPoints.Count);
        Assert.NotEmpty(viewModel.Temperatures);
    }

    [Fact]
    public void SetThresholds_KeepsCriticalAboveWarn()
    {
        var viewModel = new SensorsViewModel(new FixedSensorProvider(Sample));

        viewModel.SetThresholds(80, 60);

        Assert.Equal(80, viewModel.CriticalCelsius);
        Assert.Equal(StatusTone.Down, viewModel.ToneFor(80));
        Assert.Equal(StatusTone.Ok, viewModel.ToneFor(79.9));
    }
}
