using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Vantage.App.Converters;
using Vantage.Core.Collectors.Sensors;
using Vantage.Core.Metrics;
using Vantage.Core.Models;

namespace Vantage.App.ViewModels;

public partial class SensorsViewModel : ViewModelBase, IHostPanel
{
    private readonly ISensorProvider sensorProvider;
    private readonly int historyLength;
    private MetricsHistory hottestHistory;

    [ObservableProperty] private bool isBusy;
    [ObservableProperty] private string? lastError;
    [ObservableProperty] private bool hasLoaded;
    [ObservableProperty] private string hottestText = "-";
    [ObservableProperty] private string hottestLabel = "";
    [ObservableProperty] private StatusTone hottestTone = StatusTone.Neutral;
    [ObservableProperty] private string sampledAtText = "";
    [ObservableProperty] private double warnCelsius = 70;
    [ObservableProperty] private double criticalCelsius = 85;

    public SensorsViewModel(ISensorProvider sensorProvider, int historyLength = 120)
    {
        this.sensorProvider = sensorProvider;
        this.historyLength = Math.Max(2, historyLength);
        hottestHistory = new MetricsHistory(this.historyLength);
    }

    public ObservableCollection<TemperatureRowViewModel> Temperatures { get; } = new();

    public ObservableCollection<GpuRowViewModel> Gpus { get; } = new();

    public HostConfig? Host { get; private set; }

    public bool RefreshOnTick => true;

    public IReadOnlyList<double> HottestPoints => hottestHistory.Snapshot();

    public double ScaleMaximum => Math.Max(100, CriticalCelsius + 15);

    public bool HasNoTemperatures => HasLoaded && Temperatures.Count == 0;

    public bool HasNoGpus => HasLoaded && Gpus.Count == 0;

    public string ThresholdText => $"Warm from {WarnCelsius:0} °C, hot from {CriticalCelsius:0} °C";

    public void SetThresholds(double warn, double critical)
    {
        WarnCelsius = warn;
        CriticalCelsius = Math.Max(warn, critical);
        OnPropertyChanged(nameof(ScaleMaximum));
        OnPropertyChanged(nameof(ThresholdText));
    }

    public void SetHost(HostConfig? host)
    {
        Host = host;
        hottestHistory = new MetricsHistory(historyLength);
        Temperatures.Clear();
        Gpus.Clear();
        LastError = null;
        HasLoaded = false;
        HottestText = "-";
        HottestLabel = "";
        HottestTone = StatusTone.Neutral;
        SampledAtText = "";
        NotifyDerived();
    }

    [RelayCommand]
    public async Task RefreshAsync(CancellationToken cancellationToken)
    {
        if (Host is null)
            return;

        var host = Host;
        IsBusy = true;
        LastError = null;
        try
        {
            var snapshot = await sensorProvider.GetSnapshotAsync(host, cancellationToken);
            if (!ReferenceEquals(host, Host))
                return;
            Apply(snapshot);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            LastError = ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    public StatusTone ToneFor(double celsius) =>
        celsius >= CriticalCelsius ? StatusTone.Down
        : celsius >= WarnCelsius ? StatusTone.Warn
        : StatusTone.Ok;

    private void Apply(SensorSnapshot snapshot)
    {
        Temperatures.Clear();
        foreach (var reading in snapshot.Temperatures.OrderByDescending(t => t.Celsius))
            Temperatures.Add(new TemperatureRowViewModel(reading.Label, reading.Celsius, ToneFor(reading.Celsius), ScaleMaximum));

        Gpus.Clear();
        foreach (var gpu in snapshot.Gpus)
            Gpus.Add(new GpuRowViewModel(gpu, ToneFor(gpu.TemperatureCelsius)));

        var hottest = snapshot.Temperatures
            .Select(t => (t.Label, t.Celsius))
            .Concat(snapshot.Gpus.Select(g => (Label: g.Name, Celsius: g.TemperatureCelsius)))
            .OrderByDescending(t => t.Celsius)
            .FirstOrDefault();

        if (hottest.Label is not null)
        {
            hottestHistory.Add(hottest.Celsius);
            HottestText = ValueFormatting.Celsius(hottest.Celsius);
            HottestLabel = hottest.Label;
            HottestTone = ToneFor(hottest.Celsius);
        }
        else
        {
            HottestText = "-";
            HottestLabel = "No sensors reported";
            HottestTone = StatusTone.Neutral;
        }

        SampledAtText = "Sampled " + DateTime.Now.ToString("HH:mm:ss");
        HasLoaded = true;
        NotifyDerived();
    }

    private void NotifyDerived()
    {
        OnPropertyChanged(nameof(HottestPoints));
        OnPropertyChanged(nameof(HasNoTemperatures));
        OnPropertyChanged(nameof(HasNoGpus));
    }
}

public sealed class TemperatureRowViewModel : ViewModelBase
{
    public TemperatureRowViewModel(string label, double celsius, StatusTone tone, double scaleMaximum)
    {
        Label = label;
        Celsius = celsius;
        Tone = tone;
        ScaleMaximum = scaleMaximum;
    }

    public string Label { get; }
    public double Celsius { get; }
    public StatusTone Tone { get; }
    public double ScaleMaximum { get; }
    public string CelsiusText => ValueFormatting.Celsius(Celsius);
}

public sealed class GpuRowViewModel : ViewModelBase
{
    public GpuRowViewModel(GpuReading reading, StatusTone temperatureTone)
    {
        Reading = reading;
        TemperatureTone = temperatureTone;
    }

    public GpuReading Reading { get; }
    public StatusTone TemperatureTone { get; }

    public string Name => Reading.Name;
    public double UtilizationPercent => Reading.UtilizationPercent;
    public string UtilizationText => ValueFormatting.Percent(Reading.UtilizationPercent);
    public string TemperatureText => ValueFormatting.Celsius(Reading.TemperatureCelsius);
    public double MemoryPercent => Reading.MemoryTotalBytes <= 0 ? 0 : 100.0 * Reading.MemoryUsedBytes / Reading.MemoryTotalBytes;
    public string MemoryText => ValueFormatting.BytesPair(Reading.MemoryUsedBytes, Reading.MemoryTotalBytes);
}
