using Microsoft.Data.Sqlite;
using Vantage.Core.Models;

namespace Vantage.Core.History;

public sealed class SqliteMetricsStore : IMetricsStore, IAsyncDisposable, IDisposable
{
    private readonly SqliteConnection connection;
    private readonly SemaphoreSlim gate = new(1, 1);
    private bool initialized;

    public SqliteMetricsStore(string connectionString)
    {
        connection = new SqliteConnection(connectionString);
    }

    public static SqliteMetricsStore ForFile(string path)
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);
        return new SqliteMetricsStore($"Data Source={path}");
    }

    public static SqliteMetricsStore InMemory() =>
        new($"Data Source=vantage-{Guid.NewGuid():N};Mode=Memory;Cache=Shared");

    public static string DefaultPath()
    {
        var root = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        return Path.Combine(root, "vantage", "history.db");
    }

    public async Task RecordAsync(MetricSample sample, CancellationToken cancellationToken = default)
    {
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await EnsureSchemaAsync(cancellationToken).ConfigureAwait(false);
            await InsertAsync(new[] { sample }, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task RecordManyAsync(IEnumerable<MetricSample> samples, CancellationToken cancellationToken = default)
    {
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await EnsureSchemaAsync(cancellationToken).ConfigureAwait(false);
            await InsertAsync(samples, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            gate.Release();
        }
    }

    public Task RecordHostMetricsAsync(string hostId, HostMetrics metrics, CancellationToken cancellationToken = default)
    {
        var at = metrics.CollectedAt;
        var samples = new[]
        {
            new MetricSample(hostId, MetricNames.Cpu, at, metrics.CpuUsagePercent),
            new MetricSample(hostId, MetricNames.Memory, at, metrics.MemoryUsedPercent),
            new MetricSample(hostId, MetricNames.Disk, at, metrics.DiskUsedPercent),
            new MetricSample(hostId, MetricNames.Load1, at, metrics.Load1),
            new MetricSample(hostId, MetricNames.Load5, at, metrics.Load5),
            new MetricSample(hostId, MetricNames.Load15, at, metrics.Load15)
        };
        return RecordManyAsync(samples, cancellationToken);
    }

    public async Task<IReadOnlyList<MetricPoint>> QueryAsync(
        string hostId,
        string metric,
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken cancellationToken = default)
    {
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await EnsureSchemaAsync(cancellationToken).ConfigureAwait(false);

            using var command = connection.CreateCommand();
            command.CommandText =
                "SELECT ts, value FROM metricSample " +
                "WHERE hostId = $hostId AND metric = $metric AND ts >= $from AND ts <= $to " +
                "ORDER BY ts ASC";
            command.Parameters.AddWithValue("$hostId", hostId);
            command.Parameters.AddWithValue("$metric", metric);
            command.Parameters.AddWithValue("$from", from.ToUnixTimeMilliseconds());
            command.Parameters.AddWithValue("$to", to.ToUnixTimeMilliseconds());

            var points = new List<MetricPoint>();
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                var ts = DateTimeOffset.FromUnixTimeMilliseconds(reader.GetInt64(0));
                points.Add(new MetricPoint(ts, reader.GetDouble(1)));
            }

            return points;
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task<int> PruneOlderThanAsync(DateTimeOffset cutoff, CancellationToken cancellationToken = default)
    {
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await EnsureSchemaAsync(cancellationToken).ConfigureAwait(false);

            using var command = connection.CreateCommand();
            command.CommandText = "DELETE FROM metricSample WHERE ts < $cutoff";
            command.Parameters.AddWithValue("$cutoff", cutoff.ToUnixTimeMilliseconds());
            return await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task<long> CountAsync(CancellationToken cancellationToken = default)
    {
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await EnsureSchemaAsync(cancellationToken).ConfigureAwait(false);

            using var command = connection.CreateCommand();
            command.CommandText = "SELECT COUNT(*) FROM metricSample";
            var scalar = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
            return Convert.ToInt64(scalar);
        }
        finally
        {
            gate.Release();
        }
    }

    private async Task InsertAsync(IEnumerable<MetricSample> samples, CancellationToken cancellationToken)
    {
        using var transaction = connection.BeginTransaction();
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            "INSERT INTO metricSample (hostId, metric, ts, value) VALUES ($hostId, $metric, $ts, $value)";

        var hostId = command.Parameters.Add("$hostId", SqliteType.Text);
        var metric = command.Parameters.Add("$metric", SqliteType.Text);
        var ts = command.Parameters.Add("$ts", SqliteType.Integer);
        var value = command.Parameters.Add("$value", SqliteType.Real);

        foreach (var sample in samples)
        {
            hostId.Value = sample.HostId;
            metric.Value = sample.Metric;
            ts.Value = sample.Timestamp.ToUnixTimeMilliseconds();
            value.Value = sample.Value;
            await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        transaction.Commit();
    }

    private async Task EnsureSchemaAsync(CancellationToken cancellationToken)
    {
        if (initialized)
            return;

        if (connection.State != System.Data.ConnectionState.Open)
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        using var command = connection.CreateCommand();
        command.CommandText =
            "CREATE TABLE IF NOT EXISTS metricSample (" +
            "  hostId TEXT NOT NULL," +
            "  metric TEXT NOT NULL," +
            "  ts INTEGER NOT NULL," +
            "  value REAL NOT NULL" +
            ");" +
            "CREATE INDEX IF NOT EXISTS idxMetricSampleLookup ON metricSample (hostId, metric, ts);";
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        initialized = true;
    }

    public async ValueTask DisposeAsync()
    {
        await connection.DisposeAsync().ConfigureAwait(false);
        gate.Dispose();
    }

    public void Dispose()
    {
        connection.Dispose();
        gate.Dispose();
    }
}
