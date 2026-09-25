using System.Globalization;

namespace Vantage.Core.Logs;

public sealed record LogSincePreset(string Label, string? Value);

public static class LogSince
{
    public static readonly IReadOnlyList<LogSincePreset> Presets = new[]
    {
        new LogSincePreset("Any time", null),
        new LogSincePreset("Last 15 minutes", "15 min ago"),
        new LogSincePreset("Last hour", "1 hour ago"),
        new LogSincePreset("Last 6 hours", "6 hours ago"),
        new LogSincePreset("Today", "today"),
        new LogSincePreset("Since yesterday", "yesterday")
    };

    public static bool TryResolve(string? since, DateTimeOffset now, out DateTimeOffset cutoff)
    {
        cutoff = default;
        if (string.IsNullOrWhiteSpace(since))
            return false;

        var value = since.Trim().ToLowerInvariant();
        if (value == "today")
        {
            cutoff = new DateTimeOffset(now.Date, now.Offset);
            return true;
        }

        if (value == "yesterday")
        {
            cutoff = new DateTimeOffset(now.Date.AddDays(-1), now.Offset);
            return true;
        }

        var parts = value.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 3 || parts[2] != "ago" ||
            !int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var amount) || amount < 0)
            return false;

        TimeSpan? span = parts[1] switch
        {
            "min" or "mins" or "minute" or "minutes" => TimeSpan.FromMinutes(amount),
            "hour" or "hours" or "h" => TimeSpan.FromHours(amount),
            "day" or "days" or "d" => TimeSpan.FromDays(amount),
            _ => null
        };

        if (span is null)
            return false;

        cutoff = now - span.Value;
        return true;
    }
}
