namespace Vantage.Core.Collectors.Systemd;

public static class SystemdUnitParser
{
    public static IReadOnlyList<SystemdUnit> Parse(string listUnitsOutput)
    {
        var units = new List<SystemdUnit>();

        foreach (var rawLine in listUnitsOutput.Replace("\r\n", "\n").Split('\n'))
        {
            var line = StripLeadingMarker(rawLine).TrimEnd();
            if (line.Length == 0)
                continue;
            if (!line.Contains(".service", StringComparison.Ordinal))
                continue;

            var fields = line.Split(' ', 5, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (fields.Length < 4)
                continue;

            units.Add(new SystemdUnit(
                Unit: fields[0],
                Load: fields[1],
                Active: fields[2],
                Sub: fields[3],
                Description: fields.Length >= 5 ? fields[4] : ""));
        }

        return units;
    }

    private static string StripLeadingMarker(string line)
    {
        var index = 0;
        while (index < line.Length && !IsUnitStart(line[index]))
            index++;
        return line[index..];
    }

    private static bool IsUnitStart(char c) => char.IsLetterOrDigit(c) && c < 128;
}
