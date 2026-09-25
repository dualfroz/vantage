using System.Text.Json.Serialization;
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
    public bool DesktopNotifications { get; set; } = true;
    public int DesktopNotificationsPerMinute { get; set; } = BufferedDesktopNotifier.DefaultMaxPerWindow;
    public double TemperatureWarnCelsius { get; set; } = 70;
    public double TemperatureCriticalCelsius { get; set; } = 85;

    [JsonIgnore]
    public List<string> LoadNotices { get; private set; } = new();

    [JsonIgnore]
    public bool MigratedOnLoad { get; set; }

    public VantageConfig WithHosts(List<HostConfig> hosts)
    {
        var copy = (VantageConfig)MemberwiseClone();
        copy.Hosts = hosts;
        copy.LoadNotices = new List<string>();
        return copy;
    }
}
