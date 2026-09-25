using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Vantage.App.Converters;
using Vantage.Core.Collectors.Systemd;
using Vantage.Core.Models;

namespace Vantage.App.ViewModels;

public partial class SystemdViewModel : ViewModelBase, IHostPanel
{
    public const string AllUnits = "All units";
    public const string RunningUnits = "Running";
    public const string FailedUnits = "Failed";
    public const string InactiveUnits = "Inactive";

    private readonly ISystemdProvider systemdProvider;
    private List<SystemdUnitViewModel> loaded = new();
    private bool rebuilding;

    [ObservableProperty] private bool isBusy;
    [ObservableProperty] private string? lastError;
    [ObservableProperty] private string? statusMessage;
    [ObservableProperty] private SystemdUnitViewModel? selectedUnit;
    [ObservableProperty] private string filterText = "";
    [ObservableProperty] private string selectedStateFilter = AllUnits;
    [ObservableProperty] private bool isConfirmingStop;
    [ObservableProperty] private bool hasLoaded;
    [ObservableProperty] private string loadedAtText = "";

    public SystemdViewModel(ISystemdProvider systemdProvider)
    {
        this.systemdProvider = systemdProvider;
    }

    public ObservableCollection<SystemdUnitViewModel> Units { get; } = new();

    public IReadOnlyList<string> StateFilters { get; } = new[] { AllUnits, RunningUnits, FailedUnits, InactiveUnits };

    public HostConfig? Host { get; private set; }

    public bool RefreshOnTick => false;

    public int TotalCount => loaded.Count;
    public int RunningCount => loaded.Count(unit => unit.IsRunning);
    public int FailedCount => loaded.Count(unit => unit.IsFailed);
    public int InactiveCount => loaded.Count(unit => unit.IsInactive);

    public string SummaryText => !HasLoaded
        ? ""
        : Units.Count == loaded.Count
            ? $"{loaded.Count} service units"
            : $"{Units.Count} of {loaded.Count} service units match";

    public bool HasSelection => SelectedUnit is not null && !IsBusy;

    public string ConfirmStopText => SelectedUnit is null ? "" : $"Stop {SelectedUnit.Unit}?";

    public void SetHost(HostConfig? host)
    {
        Host = host;
        loaded = new List<SystemdUnitViewModel>();
        IsConfirmingStop = false;
        StatusMessage = null;
        LastError = null;
        HasLoaded = false;
        LoadedAtText = "";
        Rebuild();
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
            var units = await systemdProvider.GetServicesAsync(host, cancellationToken);
            if (!ReferenceEquals(host, Host))
                return;

            loaded = units
                .OrderBy(unit => unit.IsFailed ? 0 : 1)
                .ThenBy(unit => unit.Unit, StringComparer.OrdinalIgnoreCase)
                .Select(unit => new SystemdUnitViewModel(unit))
                .ToList();
            HasLoaded = true;
            LoadedAtText = "Loaded " + DateTime.Now.ToString("HH:mm:ss");
            Rebuild();
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

    [RelayCommand]
    private Task StartAsync() => ExecuteAsync(SystemdAction.Start);

    [RelayCommand]
    private Task RestartAsync() => ExecuteAsync(SystemdAction.Restart);

    [RelayCommand]
    private void RequestStop()
    {
        if (!HasSelection)
            return;
        StatusMessage = null;
        IsConfirmingStop = true;
    }

    [RelayCommand]
    private void CancelStop() => IsConfirmingStop = false;

    [RelayCommand]
    private Task ConfirmStopAsync()
    {
        IsConfirmingStop = false;
        return ExecuteAsync(SystemdAction.Stop);
    }

    partial void OnFilterTextChanged(string value) => Rebuild();
    partial void OnSelectedStateFilterChanged(string value) => Rebuild();
    partial void OnIsBusyChanged(bool value) => OnPropertyChanged(nameof(HasSelection));
    partial void OnHasLoadedChanged(bool value) => OnPropertyChanged(nameof(SummaryText));

    partial void OnSelectedUnitChanged(SystemdUnitViewModel? value)
    {
        if (!rebuilding)
            IsConfirmingStop = false;
        OnPropertyChanged(nameof(HasSelection));
        OnPropertyChanged(nameof(ConfirmStopText));
    }

    private async Task ExecuteAsync(SystemdAction action)
    {
        if (Host is null || SelectedUnit is null)
            return;

        var unit = SelectedUnit.Unit;
        IsBusy = true;
        LastError = null;
        StatusMessage = null;
        try
        {
            await systemdProvider.ExecuteAsync(Host, action, unit, CancellationToken.None);
            StatusMessage = action switch
            {
                SystemdAction.Start => $"Started {unit}.",
                SystemdAction.Stop => $"Stopped {unit}.",
                _ => $"Restarted {unit}."
            };
        }
        catch (Exception ex)
        {
            LastError = ex.Message;
        }
        finally
        {
            IsBusy = false;
        }

        await RefreshAsync(CancellationToken.None);
    }

    private void Rebuild()
    {
        var selectedName = SelectedUnit?.Unit;
        var confirming = IsConfirmingStop;

        IEnumerable<SystemdUnitViewModel> query = SelectedStateFilter switch
        {
            RunningUnits => loaded.Where(unit => unit.IsRunning),
            FailedUnits => loaded.Where(unit => unit.IsFailed),
            InactiveUnits => loaded.Where(unit => unit.IsInactive),
            _ => loaded
        };

        var filter = FilterText?.Trim();
        if (!string.IsNullOrEmpty(filter))
            query = query.Where(unit =>
                unit.Unit.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
                unit.Description.Contains(filter, StringComparison.OrdinalIgnoreCase));

        rebuilding = true;
        try
        {
            Units.Clear();
            foreach (var unit in query)
                Units.Add(unit);

            SelectedUnit = selectedName is null ? null : Units.FirstOrDefault(u => u.Unit == selectedName);
            IsConfirmingStop = SelectedUnit is not null && confirming;
        }
        finally
        {
            rebuilding = false;
        }

        OnPropertyChanged(nameof(TotalCount));
        OnPropertyChanged(nameof(RunningCount));
        OnPropertyChanged(nameof(FailedCount));
        OnPropertyChanged(nameof(InactiveCount));
        OnPropertyChanged(nameof(SummaryText));
    }
}

public sealed class SystemdUnitViewModel : ViewModelBase
{
    public SystemdUnitViewModel(SystemdUnit unit)
    {
        Model = unit;
    }

    public SystemdUnit Model { get; }

    public string Unit => Model.Unit;
    public string Description => Model.Description;
    public string Load => Model.Load;
    public string Active => Model.Active;
    public string Sub => Model.Sub;
    public string StateText => $"{Model.Active} ({Model.Sub})";

    public bool IsRunning => Model.IsRunning;
    public bool IsFailed => Model.IsFailed;
    public bool IsInactive => Model.Active == "inactive";

    public StatusTone Tone => Model switch
    {
        { IsFailed: true } => StatusTone.Down,
        { IsRunning: true } => StatusTone.Ok,
        { Active: "activating" or "deactivating" or "reloading" } => StatusTone.Warn,
        _ => StatusTone.Neutral
    };
}
