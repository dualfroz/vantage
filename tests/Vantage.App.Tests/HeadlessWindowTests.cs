using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using Microsoft.Extensions.DependencyInjection;
using Vantage.App;
using Vantage.App.ViewModels;
using Vantage.App.Views;
using Xunit;

namespace Vantage.App.Tests;

public class HeadlessWindowTests
{
    [AvaloniaFact]
    public async Task MainWindow_LoadsXamlAndBindsViewModel()
    {
        var services = new ServiceCollection();
        services.AddVantageApp(useMockProviders: true);
        var provider = services.BuildServiceProvider();

        var viewModel = provider.GetRequiredService<MainWindowViewModel>();
        await viewModel.InitializeAsync();

        var window = new MainWindow { DataContext = viewModel };
        window.Show();

        Assert.Same(viewModel, window.DataContext);
        Assert.NotEmpty(viewModel.Hosts);

        var tabControl = window.GetVisualDescendants().OfType<TabControl>().FirstOrDefault();
        Assert.NotNull(tabControl);

        var listBox = window.GetVisualDescendants().OfType<ListBox>().FirstOrDefault();
        Assert.NotNull(listBox);
    }
}
