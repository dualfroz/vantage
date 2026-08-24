using Microsoft.Extensions.DependencyInjection;
using Vantage.Core.Alerts;
using Vantage.Core.Collectors.Network;
using Vantage.Core.Collectors.Processes;
using Vantage.Core.Collectors.Sensors;
using Vantage.Core.Collectors.Systemd;
using Vantage.Core.Configuration;
using Vantage.Core.Docker;
using Vantage.Core.Health;
using Vantage.Core.History;
using Vantage.Core.Logs;
using Vantage.Core.Metrics;
using Vantage.Core.Prometheus;
using Vantage.Core.Ssh;

namespace Vantage.Core;

public static class VantageCoreServices
{
    public static IServiceCollection AddVantageCore(this IServiceCollection services, bool useMockProviders) =>
        services.AddVantageCore(useMockProviders ? VantageProviderMode.Mock : VantageProviderMode.Ssh);

    public static IServiceCollection AddVantageCore(this IServiceCollection services, VantageProviderMode mode)
    {
        services.AddSingleton<IConfigStore>(_ => new JsonConfigStore(JsonConfigStore.DefaultPath()));

        services.AddSingleton(_ => new HttpClient
        {
            Timeout = Timeout.InfiniteTimeSpan
        });
        services.AddSingleton<IHealthChecker, HealthChecker>();
        services.AddSingleton<ISshCommandRunner, SshNetCommandRunner>();

        services.AddSingleton<IMetricsStore>(_ => SqliteMetricsStore.ForFile(SqliteMetricsStore.DefaultPath()));
        services.AddSingleton<IAlertLog>(_ => new InMemoryAlertLog());
        services.AddSingleton<IDesktopNotifier, BufferedDesktopNotifier>();
        services.AddSingleton<IAlertSink>(sp => new LogAlertSink(sp.GetRequiredService<IAlertLog>()));
        services.AddSingleton<IAlertSink>(sp => new DesktopAlertSink(sp.GetRequiredService<IDesktopNotifier>()));
        services.AddSingleton(sp => new AlertEngine(Array.Empty<AlertRule>(), sp.GetServices<IAlertSink>()));

        RegisterProviders(services, mode);
        return services;
    }

    private static void RegisterProviders(IServiceCollection services, VantageProviderMode mode)
    {
        if (mode == VantageProviderMode.Mock)
        {
            services.AddSingleton<IMetricsProvider, MockMetricsProvider>();
            services.AddSingleton<IDockerProvider, MockDockerProvider>();
            services.AddSingleton<INetworkProvider, MockNetworkProvider>();
            services.AddSingleton<IProcessProvider, MockProcessProvider>();
            services.AddSingleton<ISystemdProvider, MockSystemdProvider>();
            services.AddSingleton<ISensorProvider, MockSensorProvider>();
            services.AddSingleton<ILogProvider, MockLogProvider>();
            return;
        }

        if (mode == VantageProviderMode.Prometheus)
            services.AddSingleton<IMetricsProvider>(sp => new PrometheusMetricsProvider(sp.GetRequiredService<HttpClient>()));
        else
            services.AddSingleton<IMetricsProvider, SshMetricsProvider>();

        services.AddSingleton<IDockerProvider, SshDockerProvider>();
        services.AddSingleton<INetworkProvider, SshNetworkProvider>();
        services.AddSingleton<IProcessProvider, SshProcessProvider>();
        services.AddSingleton<ISystemdProvider, SshSystemdProvider>();
        services.AddSingleton<ISensorProvider, SshSensorProvider>();
        services.AddSingleton<ILogProvider, SshLogProvider>();
    }
}
