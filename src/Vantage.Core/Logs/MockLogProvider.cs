using Vantage.Core.Models;

namespace Vantage.Core.Logs;

public sealed class MockLogProvider : ILogProvider
{
    private static readonly string[] SshTemplates =
    {
        "Accepted publickey for demo from 192.0.2.4 port {0} ssh2",
        "pam_unix(sshd:session): session opened for user demo(uid=1000) by (uid=0)",
        "Received disconnect from 198.51.100.23 port {0}:11: disconnected by user",
        "Invalid user admin from 203.0.113.50 port {0}",
        "Connection closed by authenticating user root 203.0.113.50 port {0} [preauth]",
        "pam_unix(sshd:session): session closed for user demo"
    };

    private static readonly string[] DockerTemplates =
    {
        "Container health status changed: healthy (latency {0}ms)",
        "ignoring event module=libcontainerd namespace=moby topic=/tasks/delete",
        "Pulling image layer sha256:{0}",
        "Loading containers: done. {0} containers restored",
        "API listen on /run/docker.sock",
        "shim disconnected id={0} namespace=moby"
    };

    private static readonly string[] GenericTemplates =
    {
        "Started session {0} of user demo.",
        "GET /api/status 200 {0}ms",
        "connection pool exhausted, retrying in {0}ms",
        "flushed {0} pending writes to disk",
        "renewed certificate, valid for {0} days",
        "healthcheck ok, latency {0}ms"
    };

    private readonly Func<DateTimeOffset> clock;

    public MockLogProvider(Func<DateTimeOffset>? clock = null)
    {
        this.clock = clock ?? (() => DateTimeOffset.UtcNow);
    }

    public Task<IReadOnlyList<LogLine>> ReadAsync(
        HostConfig host,
        LogSource source,
        LogQuery query,
        CancellationToken cancellationToken)
    {
        var random = new Random(source.Target.Length * 31 + query.Lines);
        var unit = source.Kind == LogSourceKind.JournalUnit ? source.Target : "file";
        var templates = TemplatesFor(source);
        var now = clock();
        var count = Math.Clamp(query.Lines, 1, 1000);

        var lines = new List<LogLine>();
        for (var i = count; i > 0; i--)
        {
            var timestamp = now.AddSeconds(-i * 3);
            var message = string.Format(templates[random.Next(templates.Length)], random.Next(1, 60000));
            var raw = $"{timestamp:yyyy-MM-ddTHH:mm:sszzz} {host.Name} {unit}[{1000 + i}]: {message}";
            lines.Add(new LogLine(timestamp, unit, message, raw));
        }

        if (source.Kind == LogSourceKind.JournalUnit && LogSince.TryResolve(query.Since, now, out var cutoff))
            lines = lines.Where(l => l.Timestamp >= cutoff).ToList();

        if (!string.IsNullOrWhiteSpace(query.Filter))
            lines = lines.Where(l => l.Raw.Contains(query.Filter!, StringComparison.Ordinal)).ToList();

        return Task.FromResult<IReadOnlyList<LogLine>>(lines);
    }

    private static string[] TemplatesFor(LogSource source)
    {
        if (source.Target.Contains("ssh", StringComparison.OrdinalIgnoreCase))
            return SshTemplates;
        if (source.Target.Contains("docker", StringComparison.OrdinalIgnoreCase))
            return DockerTemplates;
        return GenericTemplates;
    }
}
