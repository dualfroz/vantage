using System.Net.Http.Json;

namespace Vantage.Core.Alerts;

public sealed class WebhookAlertSink : IAlertSink
{
    private readonly HttpClient httpClient;
    private readonly Uri endpoint;
    private readonly TimeSpan timeout;

    public WebhookAlertSink(HttpClient httpClient, Uri endpoint, TimeSpan? timeout = null)
    {
        this.httpClient = httpClient;
        this.endpoint = endpoint;
        this.timeout = timeout ?? TimeSpan.FromSeconds(10);
    }

    public async Task NotifyAsync(AlertNotification notification, CancellationToken cancellationToken = default)
    {
        var payload = new WebhookPayload(
            Status: notification.Kind == AlertTransitionKind.Fired ? "firing" : "resolved",
            Severity: notification.Severity.ToString().ToLowerInvariant(),
            Rule: notification.RuleName,
            Host: notification.HostId,
            Metric: notification.Metric,
            Value: notification.Value,
            Threshold: notification.Threshold,
            Comparison: notification.ComparisonSymbol,
            Summary: notification.Summary(),
            Timestamp: notification.Timestamp.ToUnixTimeMilliseconds());

        using var timeoutCts = new CancellationTokenSource(timeout);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

        using var response = await httpClient
            .PostAsJsonAsync(endpoint, payload, linked.Token)
            .ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
    }

    private sealed record WebhookPayload(
        string Status,
        string Severity,
        string Rule,
        string Host,
        string Metric,
        double Value,
        double Threshold,
        string Comparison,
        string Summary,
        long Timestamp);
}
