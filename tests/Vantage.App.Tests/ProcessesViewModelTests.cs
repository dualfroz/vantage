using Vantage.App.ViewModels;
using Vantage.Core.Collectors.Processes;
using Vantage.Core.Models;
using Xunit;

namespace Vantage.App.Tests;

public class ProcessesViewModelTests
{
    private static async Task<ProcessesViewModel> LoadedAsync(string hostId = "proc-1")
    {
        var viewModel = new ProcessesViewModel(new MockProcessProvider());
        viewModel.SetHost(new HostConfig { Id = hostId });
        await viewModel.RefreshAsync(CancellationToken.None);
        return viewModel;
    }

    [Fact]
    public async Task Refresh_LoadsProcessesSortedByCpuDescending()
    {
        var viewModel = await LoadedAsync();

        Assert.NotEmpty(viewModel.Processes);
        Assert.True(viewModel.Processes.Count <= viewModel.SelectedLimit);
        for (var i = 1; i < viewModel.Processes.Count; i++)
            Assert.True(viewModel.Processes[i - 1].CpuPercent >= viewModel.Processes[i].CpuPercent);
        Assert.EndsWith("↓", viewModel.CpuHeader);
    }

    [Fact]
    public async Task SortBy_SwitchesColumnAndTogglesDirection()
    {
        var viewModel = await LoadedAsync();

        viewModel.SortByCommand.Execute("Pid");
        var ascending = viewModel.Processes.Select(p => p.Pid).ToArray();
        Assert.Equal(ascending.OrderBy(pid => pid), ascending);
        Assert.EndsWith("↑", viewModel.PidHeader);
        Assert.Equal("CPU", viewModel.CpuHeader);

        viewModel.SortByCommand.Execute("Pid");
        var descending = viewModel.Processes.Select(p => p.Pid).ToArray();
        Assert.Equal(ascending.Reverse(), descending);
    }

    [Fact]
    public async Task SortBy_MemoryStartsDescending()
    {
        var viewModel = await LoadedAsync();

        viewModel.SortByCommand.Execute("Memory");

        Assert.True(viewModel.SortDescending);
        for (var i = 1; i < viewModel.Processes.Count; i++)
            Assert.True(viewModel.Processes[i - 1].MemoryPercent >= viewModel.Processes[i].MemoryPercent);
    }

    [Fact]
    public async Task Filter_NarrowsToMatchingRowsAndReportsCount()
    {
        var viewModel = await LoadedAsync();
        var total = viewModel.Processes.Count;

        viewModel.FilterText = "postgres";

        Assert.NotEmpty(viewModel.Processes);
        Assert.All(viewModel.Processes, p => Assert.Contains("postgres", p.Command + p.User));
        Assert.Contains($"of {total} loaded", viewModel.SummaryText);
    }

    [Fact]
    public async Task Kill_RequiresConfirmationAndRemovesProcess()
    {
        var viewModel = await LoadedAsync();
        var target = viewModel.Processes.First(p => p.Command == "rsync");
        viewModel.SelectedProcess = target;

        viewModel.RequestKillCommand.Execute(null);

        Assert.True(viewModel.IsConfirming);
        Assert.Contains("SIGKILL", viewModel.ConfirmText);
        Assert.Contains(target.Pid.ToString(), viewModel.ConfirmText);
        Assert.Contains(viewModel.Processes, p => p.Pid == target.Pid);

        await viewModel.ConfirmSignalCommand.ExecuteAsync(null);

        Assert.False(viewModel.IsConfirming);
        Assert.DoesNotContain(viewModel.Processes, p => p.Pid == target.Pid);
        Assert.Equal($"Sent SIGKILL to PID {target.Pid} (rsync).", viewModel.StatusMessage);
        Assert.Null(viewModel.LastError);
    }

    [Fact]
    public async Task CancelSignal_KeepsProcess()
    {
        var viewModel = await LoadedAsync();
        var target = viewModel.Processes.First(p => p.Pid > 1);
        viewModel.SelectedProcess = target;

        viewModel.RequestTerminateCommand.Execute(null);
        viewModel.CancelSignalCommand.Execute(null);

        Assert.False(viewModel.IsConfirming);
        await viewModel.RefreshAsync(CancellationToken.None);
        Assert.Contains(viewModel.Processes, p => p.Pid == target.Pid);
    }

    [Fact]
    public async Task InitProcess_CannotBeSignalled()
    {
        var viewModel = await LoadedAsync();
        viewModel.SelectedProcess = viewModel.Processes.Single(p => p.Pid == 1);

        viewModel.RequestKillCommand.Execute(null);

        Assert.False(viewModel.CanSignal);
        Assert.False(viewModel.IsConfirming);
    }

    [Fact]
    public async Task Refresh_KeepsSelectionAndPendingConfirmation()
    {
        var viewModel = await LoadedAsync();
        var target = viewModel.Processes.First(p => p.Command == "node");
        viewModel.SelectedProcess = target;
        viewModel.RequestTerminateCommand.Execute(null);

        await viewModel.RefreshAsync(CancellationToken.None);

        Assert.Same(target, viewModel.SelectedProcess);
        Assert.True(viewModel.IsConfirming);
    }

    [Fact]
    public async Task ChangingSelection_CancelsPendingConfirmation()
    {
        var viewModel = await LoadedAsync();
        viewModel.SelectedProcess = viewModel.Processes.First(p => p.Command == "node");
        viewModel.RequestTerminateCommand.Execute(null);

        viewModel.SelectedProcess = viewModel.Processes.First(p => p.Command == "redis-server");

        Assert.False(viewModel.IsConfirming);
    }
}
