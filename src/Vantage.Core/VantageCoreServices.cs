using Microsoft.Extensions.DependencyInjection;
using Vantage.Core.Alerts;
using Vantage.Core.Collectors.Network;
using Vantage.Core.Collectors.Processes;
using Vantage.Core.Collectors.Sensors;
using Vantage.Core.Collectors.Systemd;
using Vantage.Core.Configuration;
using Vantage.Core.Desktop;
using Vantage.Core.Docker;
using Vantage.Core.Health;
using Vantage.Core.History;
using Vantage.Core.Hosts;
using Vantage.Core.Logs;
using Vantage.Core.Metrics;
using Vantage.Core.Prometheus;
using Vantage.Core.Secrets;
using Vantage.Core.Ssh;

namespace Vantage.Core;

public static class VantageCoreServices
{
    public static IServiceCollection AddVantageCore(this IServiceCollection services, bool useMockProviders) =>
        services.AddVantageCore(useMockProviders ? VantageProviderMode.Mock : VantageProviderMode.Ssh);

    public static IServiceCollection AddVantageCore(this IServiceCollection services, VantageProviderMode mode, string? configDirectory = null)
    {
        configDirectory ??= Path.GetDirectoryName(JsonConfigStore.DefaultPath())!;
        var configPath = Path.Combine(configDirectory, "config.json");
        var sessionBus = SessionBus.ForCurrentUser();

        services.AddSingleton(_ => SecretStores.ForCurrentPlatform(sessionBus));
        services.AddSingleton<IConfigStore>(sp => new KeyringConfigStore(
            new JsonConfigStore(configPath), sp.GetRequiredService<ISecretStore>()));

        services.AddSingleton(_ => new HttpClient
        {
            Timeout = Timeout.InfiniteTimeSpan
        });
        services.AddSingleton<IHealthChecker, HealthChecker>();
        services.AddSingleton<IHostKeyStore>(_ => KnownHostsStore.ForCurrentUser(configDirectory));
        services.AddSingleton<ISshCommandRunner>(sp => new SshNetCommandRunner(sp.GetRequiredService<IHostKeyStore>()));

        services.AddSingleton<IMetricsStore>(_ => SqliteMetricsStore.ForFile(Path.Combine(configDirectory, "history.db")));
        services.AddSingleton<IAlertLog>(_ => new InMemoryAlertLog());
        services.AddSingleton(_ => new BufferedDesktopNotifier(
            sessionBus is null ? null : new FreedesktopNotificationSender(sessionBus)));
        services.AddSingleton<IDesktopNotifier>(sp => sp.GetRequiredService<BufferedDesktopNotifier>());
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
            services.AddSingleton<IHostConnectionTester, MockHostConnectionTester>();
            return;
        }

        if (mode == VantageProviderMode.Prometheus)
        {
            services.AddSingleton<IMetricsProvider>(sp => new PrometheusMetricsProvider(sp.GetRequiredService<HttpClient>()));
            services.AddSingleton<IHostConnectionTester>(sp => new SshHostConnectionTester(
                sp.GetRequiredService<ISshCommandRunner>(), sp.GetRequiredService<HttpClient>()));
        }
        else
        {
            services.AddSingleton<IMetricsProvider, SshMetricsProvider>();
            services.AddSingleton<IHostConnectionTester>(sp => new SshHostConnectionTester(
                sp.GetRequiredService<ISshCommandRunner>()));
        }

        services.AddSingleton<IDockerProvider, SshDockerProvider>();
        services.AddSingleton<INetworkProvider, SshNetworkProvider>();
        services.AddSingleton<IProcessProvider, SshProcessProvider>();
        services.AddSingleton<ISystemdProvider, SshSystemdProvider>();
        services.AddSingleton<ISensorProvider, SshSensorProvider>();
        services.AddSingleton<ILogProvider, SshLogProvider>();
    }
}
