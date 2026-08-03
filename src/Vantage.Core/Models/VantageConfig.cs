using Vantage.Core.Alerts;
using Vantage.Core.Logs;

namespace Vantage.Core.Models;

public sealed class VantageConfig
{
    public List<HostConfig> Hosts { get; set; } = new();
    public List<HealthCheckConfig> HealthChecks { get; set; } = new();
    public List<AlertRule> AlertRules { get; set; } = new();
    public List<LogSource> LogSources { get; set; } = new();
    public int RefreshIntervalSeconds { get; set; } = 5;
    public int HistoryLength { get; set; } = 120;
    public int HistoryRetentionHours { get; set; } = 168;
    public string? AlertWebhookUrl { get; set; }
}
