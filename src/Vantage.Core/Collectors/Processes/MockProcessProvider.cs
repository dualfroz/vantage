using System.Collections.Concurrent;
using Vantage.Core.Models;

namespace Vantage.Core.Collectors.Processes;

public sealed class MockProcessProvider : IProcessProvider
{
    private static readonly (string User, string Command, double Cpu, double Mem)[] Catalog =
    {
        ("root", "systemd", 0.3, 0.4),
        ("postgres", "postgres", 3.1, 8.7),
        ("postgres", "postgres: walwriter", 0.6, 1.1),
        ("root", "dockerd", 5.4, 2.1),
        ("root", "containerd", 2.8, 1.5),
        ("root", "containerd-shim", 0.4, 0.3),
        ("www-data", "nginx", 1.2, 0.9),
        ("www-data", "nginx: worker", 0.8, 0.7),
        ("node", "node", 12.6, 6.3),
        ("root", "sshd", 0.1, 0.2),
        ("demo", "sshd: demo@pts/0", 0.1, 0.1),
        ("demo", "bash", 0.0, 0.1),
        ("prometheus", "prometheus", 4.7, 3.9),
        ("nobody", "node_exporter", 0.9, 0.5),
        ("grafana", "grafana", 1.9, 4.2),
        ("root", "systemd-journald", 0.2, 0.3),
        ("systemd-resolve", "systemd-resolved", 0.1, 0.2),
        ("root", "cron", 0.0, 0.1),
        ("redis", "redis-server", 1.4, 1.8),
        ("root", "fail2ban-server", 0.3, 0.6),
        ("root", "rsync", 7.9, 0.4),
        ("root", "kworker/u8:2", 0.5, 0.0)
    };

    private sealed class HostState
    {
        public readonly List<(int Pid, string User, string Command, double Cpu, double Mem)> Processes = new();
        public Random Jitter = new();
    }

    private readonly ConcurrentDictionary<string, HostState> hosts = new();
    private readonly int seed;

    public MockProcessProvider(int seed = 1337)
    {
        this.seed = seed;
    }

    public Task<IReadOnlyList<ProcessInfo>> GetTopProcessesAsync(HostConfig host, int count, CancellationToken cancellationToken)
    {
        var state = hosts.GetOrAdd(host.Id, Seed);
        lock (state)
        {
            var processes = state.Processes
                .Select(entry => new ProcessInfo(
                    entry.Pid,
                    entry.User,
                    Math.Round(entry.Cpu + state.Jitter.NextDouble() * 4, 1),
                    Math.Round(entry.Mem + state.Jitter.NextDouble() * 2, 1),
                    entry.Command))
                .OrderByDescending(p => p.CpuPercent)
                .Take(Math.Max(1, count))
                .ToArray();

            return Task.FromResult<IReadOnlyList<ProcessInfo>>(processes);
        }
    }

    public Task SignalAsync(HostConfig host, int pid, ProcessSignal signal, CancellationToken cancellationToken)
    {
        if (pid <= 1)
            throw new ArgumentOutOfRangeException(nameof(pid), "Refusing to signal PID 0 or 1.");

        var state = hosts.GetOrAdd(host.Id, Seed);
        lock (state)
        {
            var removed = state.Processes.RemoveAll(entry => entry.Pid == pid);
            if (removed == 0)
                throw new InvalidOperationException($"kill: ({pid}) - No such process");
        }

        return Task.CompletedTask;
    }

    private HostState Seed(string hostId)
    {
        var hash = seed;
        foreach (var c in hostId)
            hash = unchecked(hash * 31 + c);

        var random = new Random(hash);
        var state = new HostState { Jitter = new Random(hash ^ 0x5f3759df) };
        var pid = 1;
        foreach (var entry in Catalog)
        {
            state.Processes.Add((pid, entry.User, entry.Command, entry.Cpu, entry.Mem));
            pid += random.Next(40, 900);
        }

        return state;
    }
}
