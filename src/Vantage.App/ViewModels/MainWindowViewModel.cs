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
    private readonly IMetricsProvider metricsProvider;
    private readonly IHealthChecker healthChecker;
    private readonly IConfigStore configStore;
    private readonly IMetricsStore? metricsStore;
    private readonly AlertEngine? alertEngine;

    private VantageConfig config = new();
    private CancellationTokenSource? monitorCts;

    [ObservableProperty] private HostViewModel? selectedHost;
    [ObservableProperty] private string statusLine = "Idle";
    [ObservableProperty] private bool isMonitoring;
    [ObservableProperty] private int firingAlertCount;

    public MainWindowViewModel(
        IMetricsProvider metricsProvider,
        IDockerProvider dockerProvider,
        IHealthChecker healthChecker,
        IConfigStore configStore,
        IMetricsStore? metricsStore = null,
        AlertEngine? alertEngine = null)
    {
        this.metricsProvider = metricsProvider;
        this.healthChecker = healthChecker;
        this.configStore = configStore;
        this.metricsStore = metricsStore;
        this.alertEngine = alertEngine;
        Docker = new DockerViewModel(dockerProvider);
        Overview = new OverviewViewModel(Hosts);
    }

    public ObservableCollection<HostViewModel> Hosts { get; } = new();
    public ObservableCollection<HealthCheckViewModel> HealthChecks { get; } = new();
    public ObservableCollection<AlertStatus> Alerts { get; } = new();
    public DockerViewModel Docker { get; }
    public OverviewViewModel Overview { get; }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        config = await configStore.LoadAsync(cancellationToken).ConfigureAwait(false);
        if (config.Hosts.Count == 0 && config.HealthChecks.Count == 0)
            config = DemoData.Build();

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

        SelectedHost = Hosts.FirstOrDefault();
        Overview.Refresh();
    }

    partial void OnSelectedHostChanged(HostViewModel? value)
    {
        Docker.SetHost(value?.Config);
    }

    [RelayCommand]
    public async Task RefreshSelectedHostAsync(CancellationToken cancellationToken)
    {
        var host = SelectedHost;
        if (host is null)
            return;

        try
        {
            var metrics = await metricsProvider.GetMetricsAsync(host.Config, cancellationToken).ConfigureAwait(false);
            host.ApplyMetrics(metrics);
            StatusLine = $"Updated {host.Name} at {metrics.CollectedAt.ToLocalTime():HH:mm:ss}";

            if (metricsStore is not null)
                await metricsStore.RecordHostMetricsAsync(host.Config.Id, metrics, cancellationToken).ConfigureAwait(false);

            if (alertEngine is not null)
            {
                await alertEngine.IngestAsync(host.Config.Id, metrics, metrics.CollectedAt, cancellationToken).ConfigureAwait(false);
                UpdateAlerts();
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            host.ApplyError(ex.Message);
            StatusLine = $"{host.Name}: {ex.Message}";
        }

        Overview.Refresh();
    }

    private void UpdateAlerts()
    {
        if (alertEngine is null)
            return;

        Alerts.Clear();
        foreach (var status in alertEngine.Statuses())
            Alerts.Add(status);

        FiringAlertCount = alertEngine.ActiveAlerts().Count;
        Overview.FiringAlerts = FiringAlertCount;
    }

    [RelayCommand]
    public async Task RunAllHealthAsync(CancellationToken cancellationToken)
    {
        var checks = HealthChecks.Select(check => check.RunAsync(cancellationToken));
        await Task.WhenAll(checks).ConfigureAwait(false);
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

        await RefreshSelectedHostAsync(cancellationToken);
        await RunAllHealthAsync(cancellationToken);

        try
        {
            while (await timer.WaitForNextTickAsync(cancellationToken))
            {
                await RefreshSelectedHostAsync(cancellationToken);
                await RunAllHealthAsync(cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
        }
    }
}
