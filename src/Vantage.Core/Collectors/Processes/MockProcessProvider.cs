using Vantage.Core.Models;

namespace Vantage.Core.Collectors.Processes;

public sealed class MockProcessProvider : IProcessProvider
{
    private static readonly (string User, string Command, double Cpu, double Mem)[] Catalog =
    {
        ("root", "/usr/lib/systemd/systemd", 0.3, 0.4),
        ("postgres", "postgres: writer", 3.1, 8.7),
        ("root", "dockerd", 5.4, 2.1),
        ("www-data", "nginx: worker", 1.2, 0.9),
        ("root", "containerd", 2.8, 1.5),
        ("node", "node server.js", 12.6, 6.3),
        ("root", "sshd: foksiu", 0.1, 0.2),
        ("prometheus", "prometheus", 4.7, 3.9),
        ("grafana", "grafana-server", 1.9, 4.2),
        ("root", "journald", 0.2, 0.3)
    };

    private readonly Random random;

    public MockProcessProvider(int seed = 1337)
    {
        random = new Random(seed);
    }

    public Task<IReadOnlyList<ProcessInfo>> GetTopProcessesAsync(HostConfig host, int count, CancellationToken cancellationToken)
    {
        var pid = 100;
        var processes = Catalog
            .Select(entry => new ProcessInfo(
                pid += random.Next(1, 900),
                entry.User,
                Math.Round(entry.Cpu + random.NextDouble() * 4, 1),
                Math.Round(entry.Mem + random.NextDouble() * 2, 1),
                entry.Command))
            .OrderByDescending(p => p.CpuPercent)
            .Take(count)
            .ToArray();

        return Task.FromResult<IReadOnlyList<ProcessInfo>>(processes);
    }
}
