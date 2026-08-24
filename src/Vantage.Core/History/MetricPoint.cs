namespace Vantage.Core.History;

public readonly record struct MetricPoint(DateTimeOffset Timestamp, double Value);

public readonly record struct MetricSample(string HostId, string Metric, DateTimeOffset Timestamp, double Value);
