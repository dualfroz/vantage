using Vantage.Core.Models;

namespace Vantage.Core.Collectors.Systemd;

public sealed class MockSystemdProvider : ISystemdProvider
{
    private static readonly SystemdUnit[] Units =
    {
        new("docker.service", "loaded", "active", "running", "Docker Application Container Engine"),
        new("nginx.service", "loaded", "active", "running", "A high performance web server"),
        new("postgresql.service", "loaded", "active", "running", "PostgreSQL RDBMS"),
        new("ssh.service", "loaded", "active", "running", "OpenBSD Secure Shell server"),
        new("prometheus.service", "loaded", "active", "running", "Prometheus monitoring"),
        new("fail2ban.service", "loaded", "active", "running", "Fail2Ban Service"),
        new("systemd-journald.service", "loaded", "active", "running", "Journal Service"),
        new("backup.service", "loaded", "failed", "failed", "Nightly backup job"),
        new("certbot-renew.service", "loaded", "inactive", "dead", "Certbot renewal")
    };

    public Task<IReadOnlyList<SystemdUnit>> GetServicesAsync(HostConfig host, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<SystemdUnit>>(Units);
}
