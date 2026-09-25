using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Vantage.Core.Health;
using Vantage.Core.Models;

namespace Vantage.App.ViewModels;

public partial class HealthCheckViewModel : ViewModelBase
{
    private readonly IHealthChecker checker;

    [ObservableProperty] private HealthStatus status = HealthStatus.Unknown;
    [ObservableProperty] private string statusText = "Not checked";
    [ObservableProperty] private string latencyText = "-";
    [ObservableProperty] private string lastCheckedText = "never";

    public HealthCheckViewModel(HealthCheckConfig config, IHealthChecker checker)
    {
        Config = config;
        this.checker = checker;
    }

    public HealthCheckConfig Config { get; }

    public string Name => string.IsNullOrWhiteSpace(Config.Name) ? Config.Target : Config.Name;
    public string Target => Config.Target;
    public string KindText => Config.Kind.ToString().ToUpperInvariant();

    [RelayCommand]
    public async Task RunAsync(CancellationToken cancellationToken)
    {
        var checkResult = await checker.CheckAsync(Config, cancellationToken);
        Status = checkResult.Status;
        StatusText = checkResult.Status + (string.IsNullOrEmpty(checkResult.Message) ? "" : $" - {checkResult.Message}");
        LatencyText = checkResult.LatencyMs is { } ms
            ? ms.ToString("0", CultureInfo.InvariantCulture) + " ms"
            : "-";
        LastCheckedText = checkResult.CheckedAt.ToLocalTime().ToString("HH:mm:ss", CultureInfo.InvariantCulture);
    }
}
