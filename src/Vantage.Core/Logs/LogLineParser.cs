using System.Globalization;

namespace Vantage.Core.Logs;

public static class LogLineParser
{
    public static IReadOnlyList<LogLine> Parse(string logOutput)
    {
        var result = new List<LogLine>();

        foreach (var rawLine in logOutput.Replace("\r\n", "\n").Split('\n'))
        {
            if (rawLine.Length == 0)
                continue;
            result.Add(ParseLine(rawLine));
        }

        return result;
    }

    public static LogLine ParseLine(string line)
    {
        var firstSpace = line.IndexOf(' ');
        if (firstSpace <= 0 || !TryParseTimestamp(line[..firstSpace], out var timestamp))
            return new LogLine(null, null, line, line);

        var rest = line[(firstSpace + 1)..];
        var secondSpace = rest.IndexOf(' ');
        if (secondSpace <= 0)
            return new LogLine(timestamp, null, rest, line);

        var afterHost = rest[(secondSpace + 1)..];
        var colon = afterHost.IndexOf(':');
        if (colon <= 0)
            return new LogLine(timestamp, null, afterHost, line);

        var unit = StripPid(afterHost[..colon].Trim());
        var message = afterHost[(colon + 1)..].TrimStart();
        return new LogLine(timestamp, unit, message, line);
    }

    private static string StripPid(string unit)
    {
        var bracket = unit.IndexOf('[');
        return bracket > 0 ? unit[..bracket] : unit;
    }

    private static bool TryParseTimestamp(string token, out DateTimeOffset timestamp)
    {
        if (DateTimeOffset.TryParse(token, CultureInfo.InvariantCulture, DateTimeStyles.None, out timestamp))
            return true;

        var withColon = InsertOffsetColon(token);
        if (withColon is not null &&
            DateTimeOffset.TryParse(withColon, CultureInfo.InvariantCulture, DateTimeStyles.None, out timestamp))
            return true;

        timestamp = default;
        return false;
    }

    private static string? InsertOffsetColon(string token)
    {
        if (token.Length < 5)
            return null;

        var sign = token[^5];
        if (sign != '+' && sign != '-')
            return null;

        var offset = token[^4..];
        if (!offset.All(char.IsDigit))
            return null;

        return token[..^2] + ":" + token[^2..];
    }
}
