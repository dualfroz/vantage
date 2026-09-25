using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Vantage.Core.Alerts;
using Vantage.Core.Configuration;
using Vantage.Core.Docker;
using Vantage.Core.Health;
using Vantage.Core.History;
using Vantage.Core.Metrics;
using Vantage.Core.Models;

namespace Vantage.App.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    public const int HostTabIndex = 1;
    public const int HostEditorTabIndex = 4;

    private const int MaxTransitions = 100;
    private static readonly TimeSpan PruneInterval = TimeSpan.FromHours(1);

    private readonly AlertTransitionRecorder transitionRecorder = new();

    private readonly IMetricsProvider metricsProvider;
    private readonly IHealthChecker healthChecker;
    private readonly IConfigStore configStore;
    private readonly IMetricsStore? metricsStore;
    private readonly AlertEngine? alertEngine;
    private readonly BufferedDesktopNotifier? desktopNotifier;
    private readonly IHostPanel?[] hostPanels;

    private VantageConfig config = new();
    private CancellationTokenSource? monitorCts;
    private DateTimeOffset lastPrune = DateTimeOffset.MinValue;

    [ObservableProperty] private HostViewModel? selectedHost;
    [ObservableProperty] private string statusLine = "Idle";
    [ObservableProperty] private bool isMonitoring;
    [ObservableProperty] private int firingAlertCount;
    [ObservableProperty] private int selectedTabIndex;
    [ObservableProperty] private int selectedHostPanelIndex;

    public MainWindowViewModel(
        IMetricsProvider metricsProvider,
        IDockerProvider dockerProvider,
        IHealthChecker healthChecker,
        IConfigStore configStore,
        HostCollectors collectors,
        IMetricsStore? metricsStore = null,
        AlertEngine? alertEngine = null,
        BufferedDesktopNotifier? desktopNotifier = null)
    {
        this.metricsProvider = metricsProvider;
        this.healthChecker = healthChecker;
        this.configStore = configStore;
        this.metricsStore = metricsStore;
        this.alertEngine = alertEngine;
        this.desktopNotifier = desktopNotifier;
        alertEngine?.AddSink(transitionRecorder);

        Docker = new DockerViewModel(dockerProvider);
        Network = new NetworkViewModel(collectors.Network);
        Processes = new ProcessesViewModel(collectors.Processes);
        Services = new SystemdViewModel(collectors.Systemd);
        Sensors = new SensorsViewModel(collectors.Sensors);
        Logs = new LogsViewModel(collectors.Logs);
        HostEditor = new HostEditorViewModel(collectors.ConnectionTester, SaveHostAsync, RemoveHostAsync, collectors.HostKeys);
        Overview = new OverviewViewModel(Hosts);

        hostPanels = new IHostPanel?[] { null, Docker, Network, Processes, Services, Sensors, Logs };
    }

    public ObservableCollection<HostViewModel> Hosts { get; } = new();
    public ObservableCollection<HealthCheckViewModel> HealthChecks { get; } = new();
    public ObservableCollection<AlertRowViewModel> Alerts { get; } = new();
    public ObservableCollection<AlertTransitionViewModel> RecentTransitions { get; } = new();
    public ObservableCollection<string> Notices { get; } = new();
    public DockerViewModel Docker { get; }
    public NetworkViewModel Network { get; }
    public ProcessesViewModel Processes { get; }
    public SystemdViewModel Services { get; }
    public SensorsViewModel Sensors { get; }
    public LogsViewModel Logs { get; }
    public HostEditorViewModel HostEditor { get; }
    public OverviewViewModel Overview { get; }

    public VantageConfig Config => config;

    public bool HasHosts => Hosts.Count > 0;

    public bool HasNoHosts => Hosts.Count == 0;

    public bool HasNoAlerts => Alerts.Count == 0;

    public bool HasNoTransitions => RecentTransitions.Count == 0;

    public bool HasNotices => Notices.Count > 0;

    public IHostPanel? ActiveHostPanel =>
        SelectedHostPanelIndex >= 0 && SelectedHostPanelIndex < hostPanels.Length ? hostPanels[SelectedHostPanelIndex] : null;

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        config = await configStore.LoadAsync(cancellationToken);
        Notices.Clear();
        foreach (var notice in config.LoadNotices)
            Notices.Add(notice);
        if (config.MigratedOnLoad)
            await SaveMigratedConfigAsync(cancellationToken);
        OnPropertyChanged(nameof(HasNotices));

        if (config.Hosts.Count == 0 && config.HealthChecks.Count == 0)
            config = DemoData.Build();

        if (desktopNotifier is not null)
        {
            desktopNotifier.SendToDesktop = config.DesktopNotifications;
            desktopNotifier.MaxPerWindow = Math.Max(1, config.DesktopNotificationsPerMinute);
        }
        HostEditor.SecretsInKeyring = configStore.StoresSecretsInKeyring;

        Hosts.Clear();
        foreach (var host in config.Hosts)
            Hosts.Add(new HostViewModel(host, config.HistoryLength));

        HealthChecks.Clear();
        foreach (var check in config.HealthChecks)
            HealthChecks.Add(new HealthCheckViewModel(check, healthChecker));

        if (alertEngine is not null)
        {
            alertEngine.SetRules(config.AlertRules);
            if (!string.IsNullOrWhiteSpace(config.AlertWebhookUrl))
                alertEngine.AddSink(new WebhookAlertSink(new HttpClient(), new Uri(config.AlertWebhookUrl)));
        }

        Sensors.SetThresholds(config.TemperatureWarnCelsius, config.TemperatureCriticalCelsius);
        Logs.SetSources(config.LogSources);
        HostEditor.Load(config.Hosts);

        SelectedHost = Hosts.FirstOrDefault();
        NotifyHostsChanged();
        Overview.Refresh();
        await PruneHistoryAsync(cancellationToken);
    }

    partial void OnSelectedHostChanged(HostViewModel? value)
    {
        foreach (var panel in hostPanels)
            panel?.SetHost(value?.Config);
        _ = RefreshActivePanelAsync(CancellationToken.None, onlyLive: false);
    }

    partial void OnSelectedHostPanelIndexChanged(int value)
    {
        _ = RefreshActivePanelAsync(CancellationToken.None, onlyLive: false);
    }

    partial void OnSelectedTabIndexChanged(int value)
    {
        if (value == HostTabIndex)
            _ = RefreshActivePanelAsync(CancellationToken.None, onlyLive: false);
    }

    [RelayCommand]
    private void OpenHostEditor() => SelectedTabIndex = HostEditorTabIndex;

    [RelayCommand]
    private void DismissNotices()
    {
        Notices.Clear();
        OnPropertyChanged(nameof(HasNotices));
    }

    private async Task SaveMigratedConfigAsync(CancellationToken cancellationToken)
    {
        try
        {
            await configStore.SaveAsync(config, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Notices.Add("The updated settings could not be written to config.json: " + ex.Message);
        }
    }

    [RelayCommand]
    public async Task RefreshSelectedHostAsync(CancellationToken cancellationToken)
    {
        var host = SelectedHost;
        if (host is null)
            return;

        var error = await PollHostAsync(host, cancellationToken);
        StatusLine = error is null
            ? $"Updated {host.Name} at {DateTimeOffset.Now:HH:mm:ss}"
            : $"{host.Name}: {error}";

        UpdateAlerts();
        Overview.Refresh();
        await RefreshActivePanelAsync(cancellationToken, onlyLive: false);
    }

    public async Task RefreshAllHostsAsync(CancellationToken cancellationToken)
    {
        var hosts = Hosts.ToArray();
        if (hosts.Length == 0)
        {
            StatusLine = "No hosts configured";
            return;
        }

        var errors = await Task.WhenAll(hosts.Select(host => PollHostAsync(host, cancellationToken)));
        var failed = errors.Count(error => error is not null);
        var time = DateTimeOffset.Now.ToString("HH:mm:ss");
        StatusLine = failed == 0
            ? $"Updated {hosts.Length} hosts at {time}"
            : $"Updated {hosts.Length - failed} of {hosts.Length} hosts at {time}, {failed} unreachable";

        UpdateAlerts();
        Overview.Refresh();
    }

    private async Task<string?> PollHostAsync(HostViewModel host, CancellationToken cancellationToken)
    {
        try
        {
            var metrics = await metricsProvider.GetMetricsAsync(host.Config, cancellationToken);
            host.ApplyMetrics(metrics);

            if (metricsStore is not null)
                await metricsStore.RecordHostMetricsAsync(host.Config.Id, metrics, cancellationToken);

            if (alertEngine is not null)
                await alertEngine.IngestAsync(host.Config.Id, metrics, metrics.CollectedAt, cancellationToken);

            return null;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            host.ApplyError(ex.Message);
            return ex.Message;
        }
    }

    private async Task RefreshActivePanelAsync(CancellationToken cancellationToken, bool onlyLive)
    {
        var panel = ActiveHostPanel;
        if (panel?.Host is null || panel.IsBusy)
            return;
        if (onlyLive && (!panel.RefreshOnTick || SelectedTabIndex != HostTabIndex))
            return;

        await panel.RefreshAsync(cancellationToken);
    }

    private void UpdateAlerts()
    {
        if (alertEngine is null)
            return;

        var names = Hosts.ToDictionary(host => host.Config.Id, host => host.Name);
        Alerts.Clear();
        foreach (var status in alertEngine.Statuses().Where(status => names.ContainsKey(status.HostId)))
            Alerts.Add(new AlertRowViewModel(status, names[status.HostId]));

        FiringAlertCount = Alerts.Count(alert => alert.Status.State == AlertState.Firing);
        Overview.FiringAlerts = FiringAlertCount;
        OnPropertyChanged(nameof(HasNoAlerts));

        foreach (var notification in transitionRecorder.Drain())
        {
            var hostName = names.TryGetValue(notification.HostId, out var name) ? name : notification.HostId;
            RecentTransitions.Insert(0, new AlertTransitionViewModel(notification, hostName));
        }

        while (RecentTransitions.Count > MaxTransitions)
            RecentTransitions.RemoveAt(RecentTransitions.Count - 1);
        OnPropertyChanged(nameof(HasNoTransitions));
    }

    [RelayCommand]
    public async Task RunAllHealthAsync(CancellationToken cancellationToken)
    {
        var checks = HealthChecks.Select(check => check.RunAsync(cancellationToken));
        await Task.WhenAll(checks);
    }

    public void StartMonitoring()
    {
        if (IsMonitoring)
            return;

        monitorCts = new CancellationTokenSource();
        IsMonitoring = true;
        _ = MonitorLoopAsync(monitorCts.Token);
    }

    public void StopMonitoring()
    {
        monitorCts?.Cancel();
        monitorCts?.Dispose();
        monitorCts = null;
        IsMonitoring = false;
    }

    private async Task MonitorLoopAsync(CancellationToken cancellationToken)
    {
        var interval = TimeSpan.FromSeconds(Math.Max(1, config.RefreshIntervalSeconds));
        using var timer = new PeriodicTimer(interval);

        try
        {
            await TickAsync(cancellationToken);
            while (await timer.WaitForNextTickAsync(cancellationToken))
                await TickAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
        }
    }

    private async Task TickAsync(CancellationToken cancellationToken)
    {
        await RefreshAllHostsAsync(cancellationToken);
        await RunAllHealthAsync(cancellationToken);
        await RefreshActivePanelAsync(cancellationToken, onlyLive: true);
        await PruneHistoryAsync(cancellationToken);
    }

    private async Task PruneHistoryAsync(CancellationToken cancellationToken)
    {
        if (metricsStore is null || config.HistoryRetentionHours <= 0)
            return;

        var now = DateTimeOffset.UtcNow;
        if (now - lastPrune < PruneInterval)
            return;

        lastPrune = now;
        try
        {
            await metricsStore.PruneOlderThanAsync(now.AddHours(-config.HistoryRetentionHours), cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            StatusLine = "History cleanup failed: " + ex.Message;
        }
    }

    private async Task SaveHostAsync(HostConfig host)
    {
        var index = config.Hosts.FindIndex(existing => existing.Id == host.Id);
        if (index >= 0)
            config.Hosts[index] = host;
        else
            config.Hosts.Add(host);

        await configStore.SaveAsync(config);

        var viewModel = Hosts.FirstOrDefault(existing => existing.Config.Id == host.Id);
        if (viewModel is null)
        {
            viewModel = new HostViewModel(host, config.HistoryLength);
            Hosts.Add(viewModel);
            SelectedHost ??= viewModel;
        }
        else
        {
            viewModel.UpdateConfig(host);
            if (ReferenceEquals(viewModel, SelectedHost))
                OnSelectedHostChanged(viewModel);
        }

        NotifyHostsChanged();
        UpdateAlerts();
        Overview.Refresh();
    }

    private async Task RemoveHostAsync(string hostId)
    {
        config.Hosts.RemoveAll(existing => existing.Id == hostId);
        await configStore.SaveAsync(config);

        var viewModel = Hosts.FirstOrDefault(existing => existing.Config.Id == hostId);
        if (viewModel is not null)
        {
            var wasSelected = ReferenceEquals(viewModel, SelectedHost);
            Hosts.Remove(viewModel);
            if (wasSelected)
                SelectedHost = Hosts.FirstOrDefault();
        }

        NotifyHostsChanged();
        UpdateAlerts();
        Overview.Refresh();
    }

    private void NotifyHostsChanged()
    {
        OnPropertyChanged(nameof(HasHosts));
        OnPropertyChanged(nameof(HasNoHosts));
    }
}
