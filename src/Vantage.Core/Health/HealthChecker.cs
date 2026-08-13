using System.Diagnostics;
using System.Net.Sockets;
using Vantage.Core.Models;

namespace Vantage.Core.Health;

public sealed class HealthChecker : IHealthChecker
{
    private readonly HttpClient httpClient;

    public HealthChecker(HttpClient httpClient)
    {
        this.httpClient = httpClient;
    }

    public Task<HealthCheckResult> CheckAsync(HealthCheckConfig check, CancellationToken cancellationToken) =>
        check.Kind switch
        {
            HealthKind.Http => CheckHttpAsync(check, cancellationToken),
            HealthKind.Tcp => CheckTcpAsync(check, cancellationToken),
            _ => throw new ArgumentOutOfRangeException(nameof(check))
        };

    private async Task<HealthCheckResult> CheckHttpAsync(HealthCheckConfig check, CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(check.TimeoutMs);

        var stopwatch = Stopwatch.StartNew();
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, check.Target);
            using var response = await httpClient
                .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token)
                .ConfigureAwait(false);
            stopwatch.Stop();

            var code = (int)response.StatusCode;
            var latency = stopwatch.Elapsed.TotalMilliseconds;
            var label = $"HTTP {code} {response.ReasonPhrase}".TrimEnd();

            if (code >= check.ExpectedStatusMin && code <= check.ExpectedStatusMax)
                return HealthCheckResult.Up(latency, label);

            return HealthCheckResult.Degraded(latency, $"{label} outside {check.ExpectedStatusMin}-{check.ExpectedStatusMax}");
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return HealthCheckResult.Down($"Timed out after {check.TimeoutMs} ms");
        }
        catch (HttpRequestException ex)
        {
            return HealthCheckResult.Down(ex.Message);
        }
    }

    private static async Task<HealthCheckResult> CheckTcpAsync(HealthCheckConfig check, CancellationToken cancellationToken)
    {
        var endpoint = TcpEndpoint.Parse(check.Target);

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(check.TimeoutMs);

        var stopwatch = Stopwatch.StartNew();
        try
        {
            using var client = new TcpClient();
            await client.ConnectAsync(endpoint.Host, endpoint.Port, timeout.Token).ConfigureAwait(false);
            stopwatch.Stop();
            return HealthCheckResult.Up(stopwatch.Elapsed.TotalMilliseconds, $"Connected to {endpoint.Host}:{endpoint.Port}");
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return HealthCheckResult.Down($"Timed out after {check.TimeoutMs} ms");
        }
        catch (SocketException ex)
        {
            return HealthCheckResult.Down(ex.Message);
        }
    }
}
