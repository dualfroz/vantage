using Microsoft.Extensions.DependencyInjection;
using Vantage.App.ViewModels;
using Vantage.Core;

namespace Vantage.App;

public static class VantageAppServices
{
    public static IServiceCollection AddVantageApp(this IServiceCollection services, bool useMockProviders) =>
        services.AddVantageApp(useMockProviders ? VantageProviderMode.Mock : VantageProviderMode.Ssh);

    public static IServiceCollection AddVantageApp(this IServiceCollection services, VantageProviderMode mode)
    {
        services.AddVantageCore(mode);
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
