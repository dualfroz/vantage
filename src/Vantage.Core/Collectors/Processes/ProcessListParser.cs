using System.Globalization;

namespace Vantage.Core.Collectors.Processes;

public static class ProcessListParser
{
    public static IReadOnlyList<ProcessInfo> Parse(string psOutput)
    {
        var processes = new List<ProcessInfo>();

        foreach (var rawLine in psOutput.Replace("\r\n", "\n").Split('\n'))
        {
            var line = rawLine.Trim();
            if (line.Length == 0)
                continue;

            var fields = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (fields.Length < 5)
                continue;

            if (!int.TryParse(fields[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var pid))
                continue;

            if (!TryParsePercent(fields[2], out var cpu) || !TryParsePercent(fields[3], out var mem))
                continue;

            var command = string.Join(' ', fields[4..]);
            processes.Add(new ProcessInfo(pid, fields[1], cpu, mem, command));
        }

        return processes;
    }

    public static IReadOnlyList<ProcessInfo> TopByCpu(string psOutput, int count) =>
        Parse(psOutput).OrderByDescending(p => p.CpuPercent).Take(count).ToArray();

    private static bool TryParsePercent(string value, out double parsed) =>
        double.TryParse(value.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out parsed);
}
