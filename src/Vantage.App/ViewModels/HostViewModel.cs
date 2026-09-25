using CommunityToolkit.Mvvm.ComponentModel;
using Vantage.Core.Metrics;
using Vantage.Core.Models;

namespace Vantage.App.ViewModels;

public partial class HostViewModel : ViewModelBase
{
    private readonly MetricsHistory cpuHistory;
    private readonly MetricsHistory memoryHistory;

    [ObservableProperty] private double cpuUsagePercent;
    [ObservableProperty] private double memoryUsedPercent;
    [ObservableProperty] private double diskUsedPercent;
    [ObservableProperty] private string memoryText = "-";
    [ObservableProperty] private string diskText = "-";
    [ObservableProperty] private string loadText = "-";
    [ObservableProperty] private string uptimeText = "-";
    [ObservableProperty] private string statusText = "Not polled yet";
    [ObservableProperty] private bool isOnline;
    [ObservableProperty] private string? lastError;

    public HostViewModel(HostConfig config, int historyLength)
    {
        Config = config;
        cpuHistory = new MetricsHistory(historyLength);
        memoryHistory = new MetricsHistory(historyLength);
    }

    public HostConfig Config { get; private set; }

    public string Name => string.IsNullOrWhiteSpace(Config.Name) ? Config.Address : Config.Name;
    public string Endpoint => $"{Config.Username}@{Config.Address}:{Config.Port}";

    public void UpdateConfig(HostConfig config)
    {
        Config = config;
        OnPropertyChanged(nameof(Config));
        OnPropertyChanged(nameof(Name));
        OnPropertyChanged(nameof(Endpoint));
    }

    public IReadOnlyList<double> CpuPoints => cpuHistory.Snapshot();
    public IReadOnlyList<double> MemoryPoints => memoryHistory.Snapshot();

    public void ApplyMetrics(HostMetrics metrics)
    {
        CpuUsagePercent = metrics.CpuUsagePercent;
        MemoryUsedPercent = metrics.MemoryUsedPercent;
        DiskUsedPercent = metrics.DiskUsedPercent;
        MemoryText = ValueFormatting.BytesPair(metrics.MemoryUsedBytes, metrics.MemoryTotalBytes);
        DiskText = ValueFormatting.BytesPair(metrics.DiskUsedBytes, metrics.DiskTotalBytes);
        LoadText = $"{metrics.Load1:0.00} {metrics.Load5:0.00} {metrics.Load15:0.00}";
        UptimeText = ValueFormatting.Uptime(metrics.Uptime);

        cpuHistory.Add(metrics.CpuUsagePercent);
        memoryHistory.Add(metrics.MemoryUsedPercent);
        OnPropertyChanged(nameof(CpuPoints));
        OnPropertyChanged(nameof(MemoryPoints));

        IsOnline = true;
        LastError = null;
        StatusText = "Online";
    }

    public void ApplyError(string message)
    {
        IsOnline = false;
        LastError = message;
        StatusText = "Unreachable";
    }
}
