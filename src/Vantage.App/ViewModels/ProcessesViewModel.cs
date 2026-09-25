using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Vantage.Core.Collectors.Processes;
using Vantage.Core.Models;

namespace Vantage.App.ViewModels;

public enum ProcessSortColumn
{
    Cpu,
    Memory,
    Pid,
    User,
    Command
}

public partial class ProcessesViewModel : ViewModelBase, IHostPanel
{
    private readonly IProcessProvider processProvider;
    private readonly Dictionary<int, ProcessRowViewModel> rowsByPid = new();
    private List<ProcessRowViewModel> loaded = new();
    private bool rebuilding;

    [ObservableProperty] private bool isBusy;
    [ObservableProperty] private string? lastError;
    [ObservableProperty] private string? statusMessage;
    [ObservableProperty] private ProcessRowViewModel? selectedProcess;
    [ObservableProperty] private ProcessSortColumn sortColumn = ProcessSortColumn.Cpu;
    [ObservableProperty] private bool sortDescending = true;
    [ObservableProperty] private string filterText = "";
    [ObservableProperty] private int selectedLimit = 50;
    [ObservableProperty] private ProcessSignal? pendingSignal;
    [ObservableProperty] private bool hasLoaded;

    public ProcessesViewModel(IProcessProvider processProvider)
    {
        this.processProvider = processProvider;
    }

    public ObservableCollection<ProcessRowViewModel> Processes { get; } = new();

    public IReadOnlyList<int> Limits { get; } = new[] { 25, 50, 100, 250 };

    public HostConfig? Host { get; private set; }

    public bool RefreshOnTick => true;

    public string PidHeader => Header("PID", ProcessSortColumn.Pid);
    public string UserHeader => Header("User", ProcessSortColumn.User);
    public string CpuHeader => Header("CPU", ProcessSortColumn.Cpu);
    public string MemoryHeader => Header("Memory", ProcessSortColumn.Memory);
    public string CommandHeader => Header("Command", ProcessSortColumn.Command);

    public string SummaryText => !HasLoaded
        ? ""
        : Processes.Count == loaded.Count
            ? $"{loaded.Count} processes, the top {SelectedLimit} by CPU on the host"
            : $"{Processes.Count} of {loaded.Count} loaded processes match the filter";

    public bool IsConfirming => PendingSignal is not null && SelectedProcess is not null;

    public string ConfirmText => SelectedProcess is null || PendingSignal is null
        ? ""
        : $"Send {SignalName(PendingSignal.Value)} to PID {SelectedProcess.Pid} ({SelectedProcess.Command})?";

    public bool CanSignal => SelectedProcess is not null && SelectedProcess.Pid > 1 && !IsBusy;

    public void SetHost(HostConfig? host)
    {
        Host = host;
        rowsByPid.Clear();
        loaded = new List<ProcessRowViewModel>();
        PendingSignal = null;
        StatusMessage = null;
        LastError = null;
        HasLoaded = false;
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
            var processes = await processProvider.GetTopProcessesAsync(host, SelectedLimit, cancellationToken);
            if (!ReferenceEquals(host, Host))
                return;
            Apply(processes);
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
    private void SortBy(string column)
    {
        if (!Enum.TryParse<ProcessSortColumn>(column, out var target))
            return;

        if (SortColumn == target)
        {
            SortDescending = !SortDescending;
        }
        else
        {
            SortColumn = target;
            SortDescending = target is ProcessSortColumn.Cpu or ProcessSortColumn.Memory;
        }
    }

    [RelayCommand]
    private void RequestTerminate() => RequestSignal(ProcessSignal.Terminate);

    [RelayCommand]
    private void RequestKill() => RequestSignal(ProcessSignal.Kill);

    [RelayCommand]
    private void CancelSignal() => PendingSignal = null;

    [RelayCommand]
    private async Task ConfirmSignalAsync()
    {
        if (Host is null || SelectedProcess is null || PendingSignal is null)
            return;

        var target = SelectedProcess;
        var signal = PendingSignal.Value;
        PendingSignal = null;
        IsBusy = true;
        LastError = null;
        StatusMessage = null;
        try
        {
            await processProvider.SignalAsync(Host, target.Pid, signal, CancellationToken.None);
            StatusMessage = $"Sent {SignalName(signal)} to PID {target.Pid} ({target.Command}).";
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

    partial void OnSortColumnChanged(ProcessSortColumn value) => Rebuild();
    partial void OnSortDescendingChanged(bool value) => Rebuild();
    partial void OnFilterTextChanged(string value) => Rebuild();
    partial void OnIsBusyChanged(bool value) => OnPropertyChanged(nameof(CanSignal));
    partial void OnHasLoadedChanged(bool value) => OnPropertyChanged(nameof(SummaryText));

    partial void OnSelectedLimitChanged(int value)
    {
        if (Host is not null)
            _ = RefreshAsync(CancellationToken.None);
    }

    partial void OnSelectedProcessChanged(ProcessRowViewModel? value)
    {
        if (!rebuilding && PendingSignal is not null)
            PendingSignal = null;
        NotifySelection();
    }

    partial void OnPendingSignalChanged(ProcessSignal? value) => NotifySelection();

    private void RequestSignal(ProcessSignal signal)
    {
        if (!CanSignal)
            return;
        StatusMessage = null;
        PendingSignal = signal;
    }

    private void Apply(IReadOnlyList<ProcessInfo> processes)
    {
        var seen = new HashSet<int>();
        var next = new List<ProcessRowViewModel>(processes.Count);
        foreach (var process in processes)
        {
            if (!seen.Add(process.Pid))
                continue;

            if (!rowsByPid.TryGetValue(process.Pid, out var row))
            {
                row = new ProcessRowViewModel(process.Pid);
                rowsByPid[process.Pid] = row;
            }

            row.Apply(process);
            next.Add(row);
        }

        foreach (var pid in rowsByPid.Keys.Where(pid => !seen.Contains(pid)).ToArray())
            rowsByPid.Remove(pid);

        loaded = next;
        HasLoaded = true;
        Rebuild();
    }

    private void Rebuild()
    {
        var selectedPid = SelectedProcess?.Pid;
        var pending = PendingSignal;

        IEnumerable<ProcessRowViewModel> query = loaded;
        var filter = FilterText?.Trim();
        if (!string.IsNullOrEmpty(filter))
            query = query.Where(row =>
                row.Command.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
                row.User.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
                row.PidText == filter);

        query = (SortColumn, SortDescending) switch
        {
            (ProcessSortColumn.Cpu, true) => query.OrderByDescending(r => r.CpuPercent).ThenBy(r => r.Pid),
            (ProcessSortColumn.Cpu, false) => query.OrderBy(r => r.CpuPercent).ThenBy(r => r.Pid),
            (ProcessSortColumn.Memory, true) => query.OrderByDescending(r => r.MemoryPercent).ThenBy(r => r.Pid),
            (ProcessSortColumn.Memory, false) => query.OrderBy(r => r.MemoryPercent).ThenBy(r => r.Pid),
            (ProcessSortColumn.Pid, true) => query.OrderByDescending(r => r.Pid),
            (ProcessSortColumn.Pid, false) => query.OrderBy(r => r.Pid),
            (ProcessSortColumn.User, true) => query.OrderByDescending(r => r.User, StringComparer.OrdinalIgnoreCase).ThenBy(r => r.Pid),
            (ProcessSortColumn.User, false) => query.OrderBy(r => r.User, StringComparer.OrdinalIgnoreCase).ThenBy(r => r.Pid),
            (ProcessSortColumn.Command, true) => query.OrderByDescending(r => r.Command, StringComparer.OrdinalIgnoreCase).ThenBy(r => r.Pid),
            _ => query.OrderBy(r => r.Command, StringComparer.OrdinalIgnoreCase).ThenBy(r => r.Pid)
        };

        var target = query.ToList();
        var keep = new HashSet<ProcessRowViewModel>(target);

        rebuilding = true;
        try
        {
            for (var i = Processes.Count - 1; i >= 0; i--)
                if (!keep.Contains(Processes[i]))
                    Processes.RemoveAt(i);

            for (var i = 0; i < target.Count; i++)
            {
                if (i < Processes.Count && ReferenceEquals(Processes[i], target[i]))
                    continue;

                var current = Processes.IndexOf(target[i]);
                if (current >= 0)
                    Processes.Move(current, i);
                else
                    Processes.Insert(i, target[i]);
            }

            SelectedProcess = selectedPid is { } pid ? Processes.FirstOrDefault(r => r.Pid == pid) : null;
            PendingSignal = SelectedProcess is null ? null : pending;
        }
        finally
        {
            rebuilding = false;
        }

        OnPropertyChanged(nameof(PidHeader));
        OnPropertyChanged(nameof(UserHeader));
        OnPropertyChanged(nameof(CpuHeader));
        OnPropertyChanged(nameof(MemoryHeader));
        OnPropertyChanged(nameof(CommandHeader));
        OnPropertyChanged(nameof(SummaryText));
    }

    private void NotifySelection()
    {
        OnPropertyChanged(nameof(IsConfirming));
        OnPropertyChanged(nameof(ConfirmText));
        OnPropertyChanged(nameof(CanSignal));
    }

    private string Header(string label, ProcessSortColumn column) =>
        SortColumn != column ? label : label + (SortDescending ? " ↓" : " ↑");

    private static string SignalName(ProcessSignal signal) =>
        signal == ProcessSignal.Kill ? "SIGKILL" : "SIGTERM";
}

public partial class ProcessRowViewModel : ViewModelBase
{
    [ObservableProperty] private string user = "";
    [ObservableProperty] private string command = "";
    [ObservableProperty] private double cpuPercent;
    [ObservableProperty] private double memoryPercent;

    public ProcessRowViewModel(int pid)
    {
        Pid = pid;
    }

    public int Pid { get; }

    public string PidText => Pid.ToString(CultureInfo.InvariantCulture);

    public string CpuText => CpuPercent.ToString("0.0", CultureInfo.InvariantCulture);

    public string MemoryText => MemoryPercent.ToString("0.0", CultureInfo.InvariantCulture);

    public void Apply(ProcessInfo process)
    {
        User = process.User;
        Command = process.Command;
        CpuPercent = process.CpuPercent;
        MemoryPercent = process.MemoryPercent;
    }

    partial void OnCpuPercentChanged(double value) => OnPropertyChanged(nameof(CpuText));
    partial void OnMemoryPercentChanged(double value) => OnPropertyChanged(nameof(MemoryText));
}
