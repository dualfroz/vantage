using Vantage.Core.Models;

namespace Vantage.Core.Logs;

public sealed class MockLogProvider : ILogProvider
{
    private static readonly string[] Templates =
    {
        "Started session {0} of user foksiu.",
        "Accepted publickey for foksiu from 10.20.0.4 port {0}",
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
        var now = clock();
        var count = Math.Clamp(query.Lines, 1, 200);

        var lines = new List<LogLine>();
        for (var i = count; i > 0; i--)
        {
            var timestamp = now.AddSeconds(-i * 3);
            var message = string.Format(Templates[random.Next(Templates.Length)], random.Next(1, 4000));
            var raw = $"{timestamp:yyyy-MM-ddTHH:mm:sszzz} {host.Name} {unit}[{1000 + i}]: {message}";
            lines.Add(new LogLine(timestamp, unit, message, raw));
        }

        if (!string.IsNullOrWhiteSpace(query.Filter))
            lines = lines.Where(l => l.Raw.Contains(query.Filter!, StringComparison.OrdinalIgnoreCase)).ToList();

        return Task.FromResult<IReadOnlyList<LogLine>>(lines);
    }
}
