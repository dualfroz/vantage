using Microsoft.Extensions.DependencyInjection;
using Vantage.App.ViewModels;
using Vantage.Core;
using Vantage.Core.Collectors.Network;
using Vantage.Core.Collectors.Processes;
using Vantage.Core.Collectors.Sensors;
using Vantage.Core.Collectors.Systemd;
using Vantage.Core.Hosts;
using Vantage.Core.Logs;
using Vantage.Core.Ssh;

namespace Vantage.App;

public static class VantageAppServices
{
    public static IServiceCollection AddVantageApp(this IServiceCollection services, bool useMockProviders, string? configDirectory = null) =>
        services.AddVantageApp(useMockProviders ? VantageProviderMode.Mock : VantageProviderMode.Ssh, configDirectory);

    public static IServiceCollection AddVantageApp(this IServiceCollection services, VantageProviderMode mode, string? configDirectory = null)
    {
        services.AddVantageCore(mode, configDirectory);
        services.AddSingleton(sp => new HostCollectors(
            sp.GetRequiredService<INetworkProvider>(),
            sp.GetRequiredService<IProcessProvider>(),
            sp.GetRequiredService<ISystemdProvider>(),
            sp.GetRequiredService<ISensorProvider>(),
            sp.GetRequiredService<ILogProvider>(),
            sp.GetRequiredService<IHostConnectionTester>(),
            sp.GetRequiredService<IHostKeyStore>()));
        services.AddTransient<MainWindowViewModel>();
        return services;
    }

    public static bool ResolveMockMode() => ResolveProviderMode() == VantageProviderMode.Mock;

    public static VantageProviderMode ResolveProviderMode()
    {
        var mode = Environment.GetEnvironmentVariable("VANTAGE_PROVIDERS");
        return mode?.ToLowerInvariant() switch
        {
            "ssh" => VantageProviderMode.Ssh,
            "prometheus" => VantageProviderMode.Prometheus,
            _ => VantageProviderMode.Mock
        };
    }
}
