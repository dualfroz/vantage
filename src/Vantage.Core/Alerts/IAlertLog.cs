namespace Vantage.Core.Alerts;

public sealed record AlertLogEntry(DateTimeOffset Timestamp, AlertSeverity Severity, string Line);

public interface IAlertLog
{
    void Append(AlertLogEntry entry);

    IReadOnlyList<AlertLogEntry> Recent(int max);
}
