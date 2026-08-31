using System.Net;
using System.Net.Sockets;
using Vantage.Core.Health;
using Vantage.Core.Models;
using Vantage.Core.Tests.Fakes;
using Xunit;

namespace Vantage.Core.Tests.Health;

public class HealthCheckerTests
{
    private static HealthCheckConfig HttpCheck() =>
        new() { Kind = HealthKind.Http, Target = "https://service.local/health", TimeoutMs = 2000 };

    [Fact]
    public async Task Http_ExpectedStatusIsUp()
    {
        var checker = new HealthChecker(new HttpClient(StubHttpMessageHandler.WithStatus(HttpStatusCode.OK)));
        var result = await checker.CheckAsync(HttpCheck(), CancellationToken.None);

        Assert.Equal(HealthStatus.Up, result.Status);
        Assert.NotNull(result.LatencyMs);
    }

    [Fact]
    public async Task Http_UnexpectedStatusIsDegraded()
    {
        var checker = new HealthChecker(new HttpClient(StubHttpMessageHandler.WithStatus(HttpStatusCode.InternalServerError)));
        var result = await checker.CheckAsync(HttpCheck(), CancellationToken.None);

        Assert.Equal(HealthStatus.Degraded, result.Status);
    }

    [Fact]
    public async Task Http_ConnectionErrorIsDown()
    {
        var checker = new HealthChecker(new HttpClient(StubHttpMessageHandler.Throwing(new HttpRequestException("refused"))));
        var result = await checker.CheckAsync(HttpCheck(), CancellationToken.None);

        Assert.Equal(HealthStatus.Down, result.Status);
        Assert.Contains("refused", result.Message);
    }

    [Fact]
    public async Task Http_TimeoutIsDown()
    {
        var checker = new HealthChecker(new HttpClient(StubHttpMessageHandler.Delayed(TimeSpan.FromSeconds(10), HttpStatusCode.OK)));
        var check = HttpCheck();
        check.TimeoutMs = 100;

        var result = await checker.CheckAsync(check, CancellationToken.None);

        Assert.Equal(HealthStatus.Down, result.Status);
        Assert.Contains("Timed out", result.Message);
    }

    [Fact]
    public async Task Tcp_OpenPortIsUp()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        try
        {
            var checker = new HealthChecker(new HttpClient());
            var check = new HealthCheckConfig { Kind = HealthKind.Tcp, Target = $"127.0.0.1:{port}", TimeoutMs = 2000 };

            var result = await checker.CheckAsync(check, CancellationToken.None);

            Assert.Equal(HealthStatus.Up, result.Status);
            Assert.NotNull(result.LatencyMs);
        }
        finally
        {
            listener.Stop();
        }
    }

    [Fact]
    public async Task Tcp_ClosedPortIsDown()
    {
        var probe = new TcpListener(IPAddress.Loopback, 0);
        probe.Start();
        var closedPort = ((IPEndPoint)probe.LocalEndpoint).Port;
        probe.Stop();

        var checker = new HealthChecker(new HttpClient());
        var check = new HealthCheckConfig { Kind = HealthKind.Tcp, Target = $"127.0.0.1:{closedPort}", TimeoutMs = 2000 };

        var result = await checker.CheckAsync(check, CancellationToken.None);

        Assert.Equal(HealthStatus.Down, result.Status);
    }
}
