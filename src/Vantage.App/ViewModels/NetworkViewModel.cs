using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Vantage.Core.Collectors.Network;
using Vantage.Core.Metrics;
using Vantage.Core.Models;

namespace Vantage.App.ViewModels;

public partial class NetworkViewModel : ViewModelBase, IHostPanel
{
    private readonly INetworkProvider networkProvider;
    private readonly int historyLength;
    private MetricsHistory rxHistory;
    private MetricsHistory txHistory;
    private NetworkSnapshot? previous;
    private NetworkSnapshot? latest;

    [ObservableProperty] private bool isBusy;
    [ObservableProperty] private string? lastError;
    [ObservableProperty] private bool showLoopback;
    [ObservableProperty] private bool hasRates;
    [ObservableProperty] private string totalRxRateText = "-";
    [ObservableProperty] private string totalTxRateText = "-";
    [ObservableProperty] private string totalRxText = "-";
    [ObservableProperty] private string totalTxText = "-";
    [ObservableProperty] private string sampledAtText = "";

    public NetworkViewModel(INetworkProvider networkProvider, int historyLength = 60)
    {
        this.networkProvider = networkProvider;
        this.historyLength = Math.Max(2, historyLength);
        rxHistory = new MetricsHistory(this.historyLength);
        txHistory = new MetricsHistory(this.historyLength);
    }

    public ObservableCollection<NetworkInterfaceViewModel> Interfaces { get; } = new();

    public HostConfig? Host { get; private set; }

    public bool RefreshOnTick => true;

    public IReadOnlyList<double> RxPoints => rxHistory.Snapshot();
    public IReadOnlyList<double> TxPoints => txHistory.Snapshot();

    public double RateMaximum
    {
        get
        {
            var peak = 0.0;
            foreach (var value in rxHistory.Snapshot().Concat(txHistory.Snapshot()))
                peak = Math.Max(peak, value);
            return peak <= 0 ? 1 : peak * 1.15;
        }
    }

    public string PeakRateText => ValueFormatting.Rate(RateMaximum <= 1 ? 0 : RateMaximum / 1.15);

    public bool IsWaitingForSecondSample => latest is not null && !HasRates;

    public void SetHost(HostConfig? host)
    {
        Host = host;
        previous = null;
        latest = null;
        rxHistory = new MetricsHistory(historyLength);
        txHistory = new MetricsHistory(historyLength);
        Interfaces.Clear();
        LastError = null;
        HasRates = false;
        TotalRxRateText = "-";
        TotalTxRateText = "-";
        TotalRxText = "-";
        TotalTxText = "-";
        SampledAtText = "";
        NotifyHistory();
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
            var snapshot = await networkProvider.GetSnapshotAsync(host, cancellationToken);
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

    partial void OnShowLoopbackChanged(bool value)
    {
        if (latest is not null)
            Rebuild(latest, LastRates());
    }

    private IReadOnlyList<InterfaceThroughput> LastRates() =>
        previous is not null && latest is not null
            ? NetworkThroughput.Between(previous, latest)
            : Array.Empty<InterfaceThroughput>();

    private void Apply(NetworkSnapshot snapshot)
    {
        previous = latest;
        latest = snapshot;

        var rates = LastRates();
        HasRates = previous is not null && rates.Count > 0;

        if (HasRates)
        {
            var external = rates.Where(rate => rate.Name != "lo").ToArray();
            var rx = external.Sum(rate => rate.RxBytesPerSecond);
            var tx = external.Sum(rate => rate.TxBytesPerSecond);
            rxHistory.Add(rx);
            txHistory.Add(tx);
            TotalRxRateText = ValueFormatting.Rate(rx);
            TotalTxRateText = ValueFormatting.Rate(tx);
        }

        TotalRxText = ValueFormatting.Bytes(snapshot.TotalRxBytes);
        TotalTxText = ValueFormatting.Bytes(snapshot.TotalTxBytes);
        SampledAtText = "Sampled " + snapshot.CollectedAt.ToLocalTime().ToString("HH:mm:ss");

        Rebuild(snapshot, rates);
        NotifyHistory();
    }

    private void Rebuild(NetworkSnapshot snapshot, IReadOnlyList<InterfaceThroughput> rates)
    {
        var visible = snapshot.Interfaces
            .Where(counters => ShowLoopback || !counters.IsLoopback)
            .ToArray();

        for (var i = Interfaces.Count - 1; i >= 0; i--)
            if (visible.All(counters => counters.Name != Interfaces[i].Name))
                Interfaces.RemoveAt(i);

        for (var index = 0; index < visible.Length; index++)
        {
            var counters = visible[index];
            InterfaceThroughput? rate = rates.Any(r => r.Name == counters.Name)
                ? rates.First(r => r.Name == counters.Name)
                : null;

            var existing = Interfaces.FirstOrDefault(row => row.Name == counters.Name);
            if (existing is null)
            {
                existing = new NetworkInterfaceViewModel(counters.Name);
                Interfaces.Insert(Math.Min(index, Interfaces.Count), existing);
            }

            existing.Apply(counters, rate);
        }
    }

    private void NotifyHistory()
    {
        OnPropertyChanged(nameof(RxPoints));
        OnPropertyChanged(nameof(TxPoints));
        OnPropertyChanged(nameof(RateMaximum));
        OnPropertyChanged(nameof(PeakRateText));
        OnPropertyChanged(nameof(IsWaitingForSecondSample));
    }
}

public partial class NetworkInterfaceViewModel : ViewModelBase
{
    [ObservableProperty] private string rxRateText = "-";
    [ObservableProperty] private string txRateText = "-";
    [ObservableProperty] private string rxTotalText = "-";
    [ObservableProperty] private string txTotalText = "-";
    [ObservableProperty] private string packetsText = "-";

    public NetworkInterfaceViewModel(string name)
    {
        Name = name;
    }

    public string Name { get; }

    public bool IsLoopback => Name == "lo";

    public void Apply(InterfaceCounters counters, InterfaceThroughput? rate)
    {
        RxTotalText = ValueFormatting.Bytes(counters.RxBytes);
        TxTotalText = ValueFormatting.Bytes(counters.TxBytes);
        PacketsText = $"{ValueFormatting.Count(counters.RxPackets)} / {ValueFormatting.Count(counters.TxPackets)}";
        RxRateText = rate is { } value ? ValueFormatting.Rate(value.RxBytesPerSecond) : "-";
        TxRateText = rate is { } same ? ValueFormatting.Rate(same.TxBytesPerSecond) : "-";
    }
}
