using Vantage.App.Converters;
using Vantage.App.ViewModels;
using Vantage.Core.Collectors.Systemd;
using Vantage.Core.Models;
using Xunit;

namespace Vantage.App.Tests;

public class SystemdViewModelTests
{
    private static async Task<SystemdViewModel> LoadedAsync()
    {
        var viewModel = new SystemdViewModel(new MockSystemdProvider());
        viewModel.SetHost(new HostConfig { Id = "svc-1" });
        await viewModel.RefreshAsync(CancellationToken.None);
        return viewModel;
    }

    [Fact]
    public async Task Refresh_CountsUnitsByStateAndListsFailedFirst()
    {
        var viewModel = await LoadedAsync();

        Assert.Equal(viewModel.Units.Count, viewModel.TotalCount);
        Assert.Equal(1, viewModel.FailedCount);
        Assert.True(viewModel.RunningCount > 0);
        Assert.True(viewModel.InactiveCount > 0);
        Assert.Equal("backup.service", viewModel.Units[0].Unit);
        Assert.Equal(StatusTone.Down, viewModel.Units[0].Tone);
    }

    [Fact]
    public async Task StateFilter_ShowsOnlyMatchingUnits()
    {
        var viewModel = await LoadedAsync();

        viewModel.SelectedStateFilter = SystemdViewModel.FailedUnits;
        Assert.Single(viewModel.Units);

        viewModel.SelectedStateFilter = SystemdViewModel.InactiveUnits;
        Assert.All(viewModel.Units, unit => Assert.Equal("inactive", unit.Active));

        viewModel.SelectedStateFilter = SystemdViewModel.AllUnits;
        Assert.Equal(viewModel.TotalCount, viewModel.Units.Count);
    }

    [Fact]
    public async Task TextFilter_MatchesUnitAndDescription()
    {
        var viewModel = await LoadedAsync();

        viewModel.FilterText = "secure shell";

        var unit = Assert.Single(viewModel.Units);
        Assert.Equal("ssh.service", unit.Unit);
        Assert.Contains("1 of", viewModel.SummaryText);
    }

    [Fact]
    public async Task Restart_BringsFailedUnitBackAndReportsIt()
    {
        var viewModel = await LoadedAsync();
        viewModel.SelectedUnit = viewModel.Units.Single(u => u.Unit == "backup.service");

        await viewModel.RestartCommand.ExecuteAsync(null);

        Assert.Equal(0, viewModel.FailedCount);
        Assert.True(viewModel.Units.Single(u => u.Unit == "backup.service").IsRunning);
        Assert.Equal("Restarted backup.service.", viewModel.StatusMessage);
        Assert.Equal("backup.service", viewModel.SelectedUnit?.Unit);
    }

    [Fact]
    public async Task Stop_AsksForConfirmationFirst()
    {
        var viewModel = await LoadedAsync();
        viewModel.SelectedUnit = viewModel.Units.Single(u => u.Unit == "nginx.service");

        viewModel.RequestStopCommand.Execute(null);

        Assert.True(viewModel.IsConfirmingStop);
        Assert.Equal("Stop nginx.service?", viewModel.ConfirmStopText);
        Assert.True(viewModel.Units.Single(u => u.Unit == "nginx.service").IsRunning);

        await viewModel.ConfirmStopCommand.ExecuteAsync(null);

        Assert.False(viewModel.IsConfirmingStop);
        Assert.Equal("inactive", viewModel.Units.Single(u => u.Unit == "nginx.service").Active);
    }

    [Fact]
    public async Task CancelStop_LeavesUnitRunning()
    {
        var viewModel = await LoadedAsync();
        viewModel.SelectedUnit = viewModel.Units.Single(u => u.Unit == "nginx.service");

        viewModel.RequestStopCommand.Execute(null);
        viewModel.CancelStopCommand.Execute(null);
        await viewModel.RefreshAsync(CancellationToken.None);

        Assert.False(viewModel.IsConfirmingStop);
        Assert.True(viewModel.Units.Single(u => u.Unit == "nginx.service").IsRunning);
    }

    [Fact]
    public async Task ActionsWithoutSelection_DoNothing()
    {
        var viewModel = await LoadedAsync();

        viewModel.RequestStopCommand.Execute(null);
        await viewModel.StartCommand.ExecuteAsync(null);

        Assert.False(viewModel.HasSelection);
        Assert.False(viewModel.IsConfirmingStop);
        Assert.Null(viewModel.StatusMessage);
    }
}
