namespace Vantage.Core.Models;

public enum HealthKind
{
    Http,
    Tcp
}

public enum HealthStatus
{
    Unknown,
    Up,
    Degraded,
    Down
}

public sealed class HealthCheckConfig
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "";
    public HealthKind Kind { get; set; } = HealthKind.Http;
    public string Target { get; set; } = "";
    public int TimeoutMs { get; set; } = 5000;
    public int ExpectedStatusMin { get; set; } = 200;
    public int ExpectedStatusMax { get; set; } = 399;
}

public sealed record HealthCheckResult
{
    public HealthStatus Status { get; init; } = HealthStatus.Unknown;
    public double? LatencyMs { get; init; }
    public string Message { get; init; } = "";
    public DateTimeOffset CheckedAt { get; init; } = DateTimeOffset.UtcNow;

    public static HealthCheckResult Up(double latencyMs, string message) =>
        new() { Status = HealthStatus.Up, LatencyMs = latencyMs, Message = message };

    public static HealthCheckResult Degraded(double latencyMs, string message) =>
        new() { Status = HealthStatus.Degraded, LatencyMs = latencyMs, Message = message };

    public static HealthCheckResult Down(string message) =>
        new() { Status = HealthStatus.Down, Message = message };
}
