using Vantage.Core.Ssh;

namespace Vantage.Core.Logs;

public static class LogCommand
{
    public static string Build(LogSource source, LogQuery query)
    {
        var lines = Math.Clamp(query.Lines, 1, 100_000);
        var baseCommand = source.Kind switch
        {
            LogSourceKind.JournalUnit => BuildJournal(source.Target, query, lines),
            LogSourceKind.File => BuildFile(source.Target, lines),
            _ => throw new ArgumentOutOfRangeException(nameof(source))
        };

        if (string.IsNullOrWhiteSpace(query.Filter))
            return baseCommand;

        return baseCommand + " | grep -F -- " + ShellArgs.Quote(query.Filter);
    }

    private static string BuildJournal(string unit, LogQuery query, int lines)
    {
        var command = $"journalctl -u {ShellArgs.Quote(unit)} -n {lines} --no-pager -o short-iso";
        if (!string.IsNullOrWhiteSpace(query.Since))
            command += " --since " + ShellArgs.Quote(query.Since);
        return command;
    }

    private static string BuildFile(string path, int lines) =>
        $"tail -n {lines} -- {ShellArgs.Quote(path)}";
}
