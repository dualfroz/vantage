using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.DependencyInjection;
using Vantage.App.ViewModels;
using Vantage.App.Views;

namespace Vantage.App;

public partial class App : Application
{
    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        var services = new ServiceCollection();
        services.AddVantageApp(VantageAppServices.ResolveProviderMode());
        var provider = services.BuildServiceProvider();

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var viewModel = provider.GetRequiredService<MainWindowViewModel>();
            desktop.MainWindow = new MainWindow { DataContext = viewModel };
            desktop.ShutdownRequested += (_, _) => viewModel.StopMonitoring();
            _ = StartAsync(viewModel);
        }

        base.OnFrameworkInitializationCompleted();
    }

    private static async Task StartAsync(MainWindowViewModel viewModel)
    {
        await viewModel.InitializeAsync();
        viewModel.StartMonitoring();
    }
}
