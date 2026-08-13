namespace Vantage.Core.Logs;

public readonly record struct LogLine(DateTimeOffset? Timestamp, string? Unit, string Message, string Raw);
