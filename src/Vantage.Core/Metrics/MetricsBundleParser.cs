using Vantage.Core.Metrics.Parsing;
using Vantage.Core.Models;

namespace Vantage.Core.Metrics;

public static class MetricsBundleParser
{
    public static HostMetrics Parse(string bundle)
    {
        var sections = SplitSections(bundle);

        var load = LoadAvgParser.Parse(Require(sections, MetricsCommand.LoadAvg));
        var uptime = UptimeParser.Parse(Require(sections, MetricsCommand.Uptime));
        var memory = MemInfoParser.Parse(Require(sections, MetricsCommand.MemInfo));
        var disk = DfParser.Parse(Require(sections, MetricsCommand.Df));

        var statBefore = ProcStatParser.Parse(Require(sections, MetricsCommand.Stat1));
        var statAfter = ProcStatParser.Parse(Require(sections, MetricsCommand.Stat2));
        var cpu = CpuUsage.Compute(statBefore, statAfter);

        return new HostMetrics
        {
            CpuUsagePercent = cpu,
            MemoryUsedBytes = memory.UsedBytes,
            MemoryTotalBytes = memory.TotalBytes,
            DiskUsedBytes = disk.UsedBytes,
            DiskTotalBytes = disk.TotalBytes,
            Load1 = load.Load1,
            Load5 = load.Load5,
            Load15 = load.Load15,
            Uptime = uptime,
            CollectedAt = DateTimeOffset.UtcNow
        };
    }

    private static Dictionary<string, string> SplitSections(string bundle)
    {
        var sections = new Dictionary<string, string>(StringComparer.Ordinal);
        string? current = null;
        var buffer = new List<string>();

        void Flush()
        {
            if (current is not null)
                sections[current] = string.Join('\n', buffer);
            buffer.Clear();
        }

        foreach (var rawLine in bundle.Replace("\r\n", "\n").Split('\n'))
        {
            if (rawLine.StartsWith(MetricsCommand.MarkerPrefix, StringComparison.Ordinal))
            {
                Flush();
                current = rawLine[MetricsCommand.MarkerPrefix.Length..].Trim();
            }
            else
            {
                buffer.Add(rawLine);
            }
        }

        Flush();
        return sections;
    }

    private static string Require(IReadOnlyDictionary<string, string> sections, string name) =>
        sections.TryGetValue(name, out var value)
            ? value
            : throw new FormatException($"Metrics bundle is missing section '{name}'");
}
