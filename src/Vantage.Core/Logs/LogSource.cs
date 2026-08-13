namespace Vantage.Core.Logs;

public enum LogSourceKind
{
    JournalUnit,
    File
}

public sealed class LogSource
{
    public LogSourceKind Kind { get; set; } = LogSourceKind.JournalUnit;
    public string Target { get; set; } = "";
    public string Label { get; set; } = "";

    public static LogSource Journal(string unit, string? label = null) =>
        new() { Kind = LogSourceKind.JournalUnit, Target = unit, Label = label ?? unit };

    public static LogSource File(string path, string? label = null) =>
        new() { Kind = LogSourceKind.File, Target = path, Label = label ?? path };
}

public sealed class LogQuery
{
    public int Lines { get; set; } = 200;
    public string? Filter { get; set; }
    public string? Since { get; set; }
}
