using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Vantage.App.ViewModels;

public partial class OverviewViewModel : ViewModelBase
{
    private readonly ObservableCollection<HostViewModel> hosts;

    [ObservableProperty] private int totalHosts;
    [ObservableProperty] private int onlineHosts;
    [ObservableProperty] private int offlineHosts;
    [ObservableProperty] private double averageCpuPercent;
    [ObservableProperty] private double peakCpuPercent;
    [ObservableProperty] private string peakCpuHost = "-";
    [ObservableProperty] private double peakMemoryPercent;
    [ObservableProperty] private string peakMemoryHost = "-";
    [ObservableProperty] private int firingAlerts;

    public OverviewViewModel(ObservableCollection<HostViewModel> hosts)
    {
        this.hosts = hosts;
    }

    public void Refresh()
    {
        TotalHosts = hosts.Count;
        OnlineHosts = hosts.Count(host => host.IsOnline);
        OfflineHosts = TotalHosts - OnlineHosts;

        var online = hosts.Where(host => host.IsOnline).ToArray();
        AverageCpuPercent = online.Length == 0 ? 0 : Math.Round(online.Average(host => host.CpuUsagePercent), 1);

        var hottestCpu = online.OrderByDescending(host => host.CpuUsagePercent).FirstOrDefault();
        PeakCpuPercent = hottestCpu?.CpuUsagePercent ?? 0;
        PeakCpuHost = hottestCpu?.Name ?? "-";

        var hottestMemory = online.OrderByDescending(host => host.MemoryUsedPercent).FirstOrDefault();
        PeakMemoryPercent = hottestMemory?.MemoryUsedPercent ?? 0;
        PeakMemoryHost = hottestMemory?.Name ?? "-";
    }
}
