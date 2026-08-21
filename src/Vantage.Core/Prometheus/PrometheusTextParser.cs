using System.Globalization;
using System.Text;

namespace Vantage.Core.Prometheus;

public static class PrometheusTextParser
{
    private static readonly IReadOnlyDictionary<string, string> NoLabels =
        new Dictionary<string, string>();

    public static IReadOnlyList<PrometheusSample> Parse(string exposition)
    {
        var samples = new List<PrometheusSample>();

        foreach (var rawLine in exposition.Replace("\r\n", "\n").Split('\n'))
        {
            var line = rawLine.Trim();
            if (line.Length == 0 || line[0] == '#')
                continue;

            var sample = ParseLine(line);
            if (sample is not null)
                samples.Add(sample);
        }

        return samples;
    }

    private static PrometheusSample? ParseLine(string line)
    {
        var cursor = 0;
        var name = ReadName(line, ref cursor);
        if (name.Length == 0)
            return null;

        var labels = NoLabels;
        SkipSpaces(line, ref cursor);
        if (cursor < line.Length && line[cursor] == '{')
        {
            labels = ReadLabels(line, ref cursor);
            SkipSpaces(line, ref cursor);
        }

        var remainder = line[cursor..].Trim();
        if (remainder.Length == 0)
            return null;

        var parts = remainder.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (!TryParseValue(parts[0], out var value))
            return null;

        long? timestamp = null;
        if (parts.Length >= 2 && long.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var ts))
            timestamp = ts;

        return new PrometheusSample(name, labels, value, timestamp);
    }

    private static string ReadName(string line, ref int cursor)
    {
        var start = cursor;
        while (cursor < line.Length)
        {
            var c = line[cursor];
            if (c == '{' || c == ' ' || c == '\t')
                break;
            cursor++;
        }

        return line[start..cursor];
    }

    private static IReadOnlyDictionary<string, string> ReadLabels(string line, ref int cursor)
    {
        var labels = new Dictionary<string, string>(StringComparer.Ordinal);
        cursor++;

        while (cursor < line.Length && line[cursor] != '}')
        {
            SkipSpaces(line, ref cursor);
            var keyStart = cursor;
            while (cursor < line.Length && line[cursor] != '=' && line[cursor] != '}')
                cursor++;

            if (cursor >= line.Length || line[cursor] != '=')
                break;

            var key = line[keyStart..cursor].Trim();
            cursor++;
            SkipSpaces(line, ref cursor);

            if (cursor >= line.Length || line[cursor] != '"')
                break;

            var value = ReadQuoted(line, ref cursor);
            if (key.Length > 0)
                labels[key] = value;

            SkipSpaces(line, ref cursor);
            if (cursor < line.Length && line[cursor] == ',')
                cursor++;
        }

        if (cursor < line.Length && line[cursor] == '}')
            cursor++;

        return labels;
    }

    private static string ReadQuoted(string line, ref int cursor)
    {
        var builder = new StringBuilder();
        cursor++;

        while (cursor < line.Length)
        {
            var c = line[cursor];
            if (c == '\\' && cursor + 1 < line.Length)
            {
                var next = line[cursor + 1];
                builder.Append(next switch
                {
                    'n' => '\n',
                    't' => '\t',
                    '"' => '"',
                    '\\' => '\\',
                    _ => next
                });
                cursor += 2;
                continue;
            }

            if (c == '"')
            {
                cursor++;
                break;
            }

            builder.Append(c);
            cursor++;
        }

        return builder.ToString();
    }

    private static bool TryParseValue(string token, out double value)
    {
        switch (token)
        {
            case "+Inf":
                value = double.PositiveInfinity;
                return true;
            case "-Inf":
                value = double.NegativeInfinity;
                return true;
            case "NaN":
                value = double.NaN;
                return true;
            default:
                return double.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
        }
    }

    private static void SkipSpaces(string line, ref int cursor)
    {
        while (cursor < line.Length && (line[cursor] == ' ' || line[cursor] == '\t'))
            cursor++;
    }
}
