using Vantage.Core.Alerts;
using Vantage.Core.Logs;
using Vantage.Core.Models;

namespace Vantage.App;

public static class DemoData
{
    public static VantageConfig Build() => new()
    {
        RefreshIntervalSeconds = 3,
        HistoryLength = 120,
        AlertRules =
        {
            new AlertRule { Name = "CPU saturated", Metric = "cpu", Comparison = AlertComparison.GreaterOrEqual, Threshold = 80, For = TimeSpan.FromSeconds(30), Severity = AlertSeverity.Warning },
            new AlertRule { Name = "Memory pressure", Metric = "memory", Comparison = AlertComparison.GreaterOrEqual, Threshold = 90, For = TimeSpan.FromMinutes(2), Severity = AlertSeverity.Critical },
            new AlertRule { Name = "Disk almost full", Metric = "disk", Comparison = AlertComparison.GreaterOrEqual, Threshold = 90, For = TimeSpan.Zero, Severity = AlertSeverity.Critical }
        },
        LogSources =
        {
            LogSource.Journal("docker", "Docker"),
            LogSource.Journal("ssh", "SSH"),
            LogSource.Journal("nginx", "nginx"),
            LogSource.File("/var/log/syslog", "syslog")
        },
        Hosts =
        {
            new HostConfig { Name = "edge-01", Address = "192.0.2.11", Username = "root", AuthMethod = SshAuthMethod.PrivateKey, PrivateKeyPath = "~/.ssh/id_ed25519" },
            new HostConfig { Name = "storage-01", Address = "198.51.100.21", Username = "ops", AuthMethod = SshAuthMethod.PrivateKey, PrivateKeyPath = "~/.ssh/id_ed25519" },
            new HostConfig { Name = "vps-main", Address = "vps.example.net", Username = "deploy", AuthMethod = SshAuthMethod.PrivateKey, PrivateKeyPath = "~/.ssh/id_ed25519" }
        },
        HealthChecks =
        {
            new HealthCheckConfig { Name = "Gateway HTTP", Kind = HealthKind.Http, Target = "https://example.com" },
            new HealthCheckConfig { Name = "Local SSH", Kind = HealthKind.Tcp, Target = "127.0.0.1:22" },
            new HealthCheckConfig { Name = "DNS TCP", Kind = HealthKind.Tcp, Target = "1.1.1.1:53" }
        }
    };
}
