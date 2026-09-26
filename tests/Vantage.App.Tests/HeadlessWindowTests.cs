using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Microsoft.Extensions.DependencyInjection;
using Vantage.App;
using Vantage.App.Tests.Fakes;
using Vantage.App.ViewModels;
using Vantage.App.Views;
using Vantage.Core.Configuration;
using Vantage.Core.Docker;
using Vantage.Core.Metrics;
using Vantage.Core.Models;
using Vantage.Core.Secrets;
using Xunit;

namespace Vantage.App.Tests;

public class HeadlessWindowTests
{
    private static async Task<(MainWindow Window, MainWindowViewModel ViewModel)> ShowAsync()
    {
        var store = new JsonConfigStore(Path.Combine(Path.GetTempPath(), "vantage-ui-" + Guid.NewGuid().ToString("N"), "config.json"));
        var viewModel = new MainWindowViewModel(
            new MockMetricsProvider(),
            new MockDockerProvider(),
            new FakeHealthChecker(HealthCheckResult.Up(3, "ok")),
            store,
            TestCollectors.Mock());
        await viewModel.InitializeAsync();

        var window = new MainWindow { DataContext = viewModel };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        return (window, viewModel);
    }

    [AvaloniaFact]
    public async Task MainWindow_LoadsXamlAndBindsViewModel()
    {
        var configDirectory = Path.Combine(Path.GetTempPath(), "vantage-app-" + Guid.NewGuid().ToString("N"));
        var secrets = new FakeSecretStore();
        var services = new ServiceCollection();
        services.AddVantageApp(useMockProviders: true, configDirectory);
        services.AddSingleton<ISecretStore>(secrets);

        try
        {
            await using var provider = services.BuildServiceProvider();

            var viewModel = provider.GetRequiredService<MainWindowViewModel>();
            await viewModel.InitializeAsync();

            var window = new MainWindow { DataContext = viewModel };
            window.Show();

            Assert.Same(viewModel, window.DataContext);
            Assert.NotEmpty(viewModel.Hosts);
            Assert.Equal(Path.Combine(configDirectory, "config.json"), provider.GetRequiredService<IConfigStore>().Path);
            Assert.True(File.Exists(Path.Combine(configDirectory, "history.db")));
            Assert.True(secrets.AvailabilityChecks > 0);

            var tabControl = window.GetVisualDescendants().OfType<TabControl>().FirstOrDefault();
            Assert.NotNull(tabControl);

            var listBox = window.GetVisualDescendants().OfType<ListBox>().FirstOrDefault();
            Assert.NotNull(listBox);
        }
        finally
        {
            if (Directory.Exists(configDirectory))
                Directory.Delete(configDirectory, recursive: true);
        }
    }

    [AvaloniaTheory]
    [InlineData(0, typeof(HostMetricsView))]
    [InlineData(1, typeof(DockerView))]
    [InlineData(2, typeof(NetworkView))]
    [InlineData(3, typeof(ProcessesView))]
    [InlineData(4, typeof(SystemdView))]
    [InlineData(5, typeof(SensorsView))]
    [InlineData(6, typeof(LogsView))]
    public async Task HostPanels_MaterializeInTheHostTab(int panelIndex, Type viewType)
    {
        var (window, viewModel) = await ShowAsync();

        viewModel.SelectedTabIndex = MainWindowViewModel.HostTabIndex;
        viewModel.SelectedHostPanelIndex = panelIndex;
        Dispatcher.UIThread.RunJobs();

        var view = window.GetVisualDescendants().FirstOrDefault(visual => visual.GetType() == viewType) as Control;
        Assert.NotNull(view);
        Assert.True(view!.IsEffectivelyVisible);
        Assert.NotNull(view.DataContext);
    }

    [AvaloniaFact]
    public async Task ProcessesPanel_RendersOneRowPerProcess()
    {
        var (window, viewModel) = await ShowAsync();

        viewModel.SelectedTabIndex = MainWindowViewModel.HostTabIndex;
        viewModel.SelectedHostPanelIndex = 3;
        Dispatcher.UIThread.RunJobs();

        var view = window.GetVisualDescendants().OfType<ProcessesView>().Single();
        var list = view.GetVisualDescendants().OfType<ListBox>().Single();
        Assert.Same(viewModel.Processes.Processes, list.ItemsSource);
        Assert.NotEmpty(list.GetVisualDescendants().OfType<ListBoxItem>());
    }

    [AvaloniaFact]
    public async Task HostEditorTab_ShowsFormForSelectedHost()
    {
        var (window, viewModel) = await ShowAsync();

        viewModel.OpenHostEditorCommand.Execute(null);
        Dispatcher.UIThread.RunJobs();

        var editor = window.GetVisualDescendants().OfType<HostEditorView>().Single();
        var addressBox = editor.GetVisualDescendants().OfType<TextBox>()
            .FirstOrDefault(box => box.Text == viewModel.HostEditor.SelectedDraft!.Address);
        Assert.NotNull(addressBox);
        Assert.True(addressBox!.IsEffectivelyVisible);
    }
}
