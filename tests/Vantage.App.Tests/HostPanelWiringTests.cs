using Vantage.App.Tests.Fakes;
using Vantage.App.ViewModels;
using Vantage.Core.Configuration;
using Vantage.Core.Docker;
using Vantage.Core.Health;
using Vantage.Core.Metrics;
using Vantage.Core.Models;
using Xunit;

namespace Vantage.App.Tests;

public class HostPanelWiringTests
{
    private static JsonConfigStore TempStore() =>
        new(Path.Combine(Path.GetTempPath(), "vantage-panels-" + Guid.NewGuid().ToString("N"), "config.json"));

    private static async Task<MainWindowViewModel> InitializedAsync(IConfigStore? store = null)
    {
        var viewModel = new MainWindowViewModel(
            new MockMetricsProvider(),
            new MockDockerProvider(),
            new FakeHealthChecker(HealthCheckResult.Up(1, "ok")),
            store ?? TempStore(),
            TestCollectors.Mock());
        await viewModel.InitializeAsync();
        return viewModel;
    }

    [Fact]
    public async Task Initialize_HandsConfigToEveryPanel()
    {
        var viewModel = await InitializedAsync();

        Assert.Same(viewModel.SelectedHost!.Config, viewModel.Network.Host);
        Assert.Same(viewModel.SelectedHost.Config, viewModel.Processes.Host);
        Assert.Same(viewModel.SelectedHost.Config, viewModel.Services.Host);
        Assert.Same(viewModel.SelectedHost.Config, viewModel.Sensors.Host);
        Assert.Same(viewModel.SelectedHost.Config, viewModel.Logs.Host);
        Assert.Equal(viewModel.Config.LogSources.Count, viewModel.Logs.Sources.Count);
        Assert.Equal(viewModel.Hosts.Count, viewModel.HostEditor.Drafts.Count);
    }

    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    public async Task OpeningAHostPanel_LoadsItInMockMode(int panelIndex)
    {
        var viewModel = await InitializedAsync();

        viewModel.SelectedTabIndex = MainWindowViewModel.HostTabIndex;
        viewModel.SelectedHostPanelIndex = panelIndex;

        switch (viewModel.ActiveHostPanel)
        {
            case NetworkViewModel network:
                Assert.NotEmpty(network.Interfaces);
                break;
            case ProcessesViewModel processes:
                Assert.NotEmpty(processes.Processes);
                break;
            case SystemdViewModel services:
                Assert.NotEmpty(services.Units);
                break;
            case SensorsViewModel sensors:
                Assert.NotEmpty(sensors.Temperatures);
                break;
            case LogsViewModel logs:
                Assert.NotEmpty(logs.Lines);
                break;
            default:
                Assert.Fail($"Unexpected panel at index {panelIndex}");
                break;
        }
    }

    [Fact]
    public async Task DockerPanel_LoadsWhenOpened()
    {
        var viewModel = await InitializedAsync();

        viewModel.SelectedHostPanelIndex = 1;

        Assert.Same(viewModel.Docker, viewModel.ActiveHostPanel);
        Assert.NotEmpty(viewModel.Docker.Containers);
    }

    [Fact]
    public async Task SwitchingHost_ReloadsActivePanelForNewHost()
    {
        var viewModel = await InitializedAsync();
        viewModel.SelectedHostPanelIndex = 3;
        var first = viewModel.Processes.Processes.First(p => p.Pid > 1);
        viewModel.Processes.SelectedProcess = first;
        viewModel.Processes.RequestKillCommand.Execute(null);
        await viewModel.Processes.ConfirmSignalCommand.ExecuteAsync(null);

        viewModel.SelectedHost = viewModel.Hosts[1];

        Assert.Same(viewModel.Hosts[1].Config, viewModel.Processes.Host);
        Assert.Contains(viewModel.Processes.Processes, p => p.Command == first.Command);
        Assert.Null(viewModel.Processes.StatusMessage);
    }

    [Fact]
    public async Task RefreshAllHosts_PollsEveryHost()
    {
        var viewModel = await InitializedAsync();

        await viewModel.RefreshAllHostsAsync(CancellationToken.None);

        Assert.All(viewModel.Hosts, host => Assert.True(host.IsOnline));
        Assert.Equal(viewModel.Hosts.Count, viewModel.Overview.OnlineHosts);
        Assert.StartsWith($"Updated {viewModel.Hosts.Count} hosts", viewModel.StatusLine);
    }

    [Fact]
    public async Task SavingHostInEditor_AddsItToSidebarAndPersists()
    {
        var store = TempStore();
        var viewModel = await InitializedAsync(store);
        var before = viewModel.Hosts.Count;

        viewModel.HostEditor.AddHostCommand.Execute(null);
        var draft = viewModel.HostEditor.SelectedDraft!;
        draft.Name = "lab-03";
        draft.Address = "203.0.113.30";
        draft.Username = "demo";
        await viewModel.HostEditor.SaveCommand.ExecuteAsync(null);

        Assert.Equal(before + 1, viewModel.Hosts.Count);
        Assert.Contains(viewModel.Hosts, host => host.Name == "lab-03");
        var persisted = await store.LoadAsync();
        Assert.Contains(persisted.Hosts, host => host.Name == "lab-03" && host.Address == "203.0.113.30");
    }

    [Fact]
    public async Task EditingSelectedHost_UpdatesSidebarEntryInPlace()
    {
        var store = TempStore();
        var viewModel = await InitializedAsync(store);
        var selected = viewModel.SelectedHost!;
        await viewModel.RefreshSelectedHostAsync(CancellationToken.None);

        var draft = viewModel.HostEditor.Drafts.Single(d => d.Id == selected.Config.Id);
        viewModel.HostEditor.SelectedDraft = draft;
        draft.Name = "edge-renamed";
        await viewModel.HostEditor.SaveCommand.ExecuteAsync(null);

        Assert.Same(selected, viewModel.SelectedHost);
        Assert.Equal("edge-renamed", selected.Name);
        Assert.NotEmpty(selected.CpuPoints);
        Assert.Same(selected.Config, viewModel.Docker.Host);
        Assert.Equal("edge-renamed", (await store.LoadAsync()).Hosts.Single(h => h.Id == selected.Config.Id).Name);
    }

    [Fact]
    public async Task RemovingSelectedHost_SelectsNextAndPersists()
    {
        var store = TempStore();
        var viewModel = await InitializedAsync(store);
        var removed = viewModel.SelectedHost!;
        viewModel.HostEditor.SelectedDraft = viewModel.HostEditor.Drafts.Single(d => d.Id == removed.Config.Id);

        viewModel.HostEditor.RequestRemoveCommand.Execute(null);
        await viewModel.HostEditor.ConfirmRemoveCommand.ExecuteAsync(null);

        Assert.DoesNotContain(removed, viewModel.Hosts);
        Assert.NotNull(viewModel.SelectedHost);
        Assert.NotSame(removed, viewModel.SelectedHost);
        Assert.DoesNotContain((await store.LoadAsync()).Hosts, h => h.Id == removed.Config.Id);
    }

    [Fact]
    public async Task OpenHostEditorCommand_SelectsHostsTab()
    {
        var viewModel = await InitializedAsync();

        viewModel.OpenHostEditorCommand.Execute(null);

        Assert.Equal(MainWindowViewModel.HostEditorTabIndex, viewModel.SelectedTabIndex);
    }
}
