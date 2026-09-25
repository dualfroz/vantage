using System.Collections.Concurrent;
using Vantage.Core.Models;

namespace Vantage.Core.Collectors.Systemd;

public sealed class MockSystemdProvider : ISystemdProvider
{
    private static readonly SystemdUnit[] Units =
    {
        new("docker.service", "loaded", "active", "running", "Docker Application Container Engine"),
        new("containerd.service", "loaded", "active", "running", "containerd container runtime"),
        new("nginx.service", "loaded", "active", "running", "A high performance web server"),
        new("postgresql.service", "loaded", "active", "running", "PostgreSQL RDBMS"),
        new("redis-server.service", "loaded", "active", "running", "Advanced key-value store"),
        new("ssh.service", "loaded", "active", "running", "OpenBSD Secure Shell server"),
        new("prometheus.service", "loaded", "active", "running", "Prometheus monitoring"),
        new("prometheus-node-exporter.service", "loaded", "active", "running", "Prometheus exporter for machine metrics"),
        new("grafana-server.service", "loaded", "active", "running", "Grafana instance"),
        new("fail2ban.service", "loaded", "active", "running", "Fail2Ban Service"),
        new("cron.service", "loaded", "active", "running", "Regular background program processing daemon"),
        new("systemd-journald.service", "loaded", "active", "running", "Journal Service"),
        new("systemd-resolved.service", "loaded", "active", "running", "Network Name Resolution"),
        new("systemd-timesyncd.service", "loaded", "active", "running", "Network Time Synchronization"),
        new("backup.service", "loaded", "failed", "failed", "Nightly backup job"),
        new("certbot-renew.service", "loaded", "inactive", "dead", "Certbot renewal"),
        new("apt-daily-upgrade.service", "loaded", "inactive", "dead", "Daily apt upgrade and clean activities"),
        new("systemd-tmpfiles-clean.service", "loaded", "inactive", "dead", "Cleanup of Temporary Directories")
    };

    private readonly ConcurrentDictionary<string, List<SystemdUnit>> byHost = new();

    public Task<IReadOnlyList<SystemdUnit>> GetServicesAsync(HostConfig host, CancellationToken cancellationToken)
    {
        var units = byHost.GetOrAdd(host.Id, _ => Units.ToList());
        lock (units)
            return Task.FromResult<IReadOnlyList<SystemdUnit>>(units.ToArray());
    }

    public Task ExecuteAsync(HostConfig host, SystemdAction action, string unit, CancellationToken cancellationToken)
    {
        var units = byHost.GetOrAdd(host.Id, _ => Units.ToList());
        lock (units)
        {
            var index = units.FindIndex(u => u.Unit == unit);
            if (index < 0)
                throw new InvalidOperationException($"Unit {unit} not found.");

            units[index] = action switch
            {
                SystemdAction.Stop => units[index] with { Active = "inactive", Sub = "dead" },
                SystemdAction.Start or SystemdAction.Restart => units[index] with { Active = "active", Sub = "running" },
                _ => units[index]
            };
        }

        return Task.CompletedTask;
    }
}
