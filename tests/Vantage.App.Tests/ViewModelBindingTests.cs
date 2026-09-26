using Vantage.App.Tests.Fakes;
using Vantage.App.ViewModels;
using Vantage.Core.Models;
using Xunit;

namespace Vantage.App.Tests;

public class ViewModelBindingTests
{
    [Fact]
    public void HostViewModel_ApplyMetricsUpdatesTextsAndHistory()
    {
        var host = new HostViewModel(new HostConfig { Name = "node", Address = "192.0.2.5", Username = "root", Port = 22 }, historyLength: 10);

        host.ApplyMetrics(new HostMetrics
        {
            CpuUsagePercent = 42.5,
            MemoryUsedBytes = 4L * 1024 * 1024 * 1024,
            MemoryTotalBytes = 8L * 1024 * 1024 * 1024,
            DiskUsedBytes = 50L * 1024 * 1024 * 1024,
            DiskTotalBytes = 100L * 1024 * 1024 * 1024,
            Load1 = 0.8,
            Load5 = 0.6,
            Load15 = 0.4,
            Uptime = TimeSpan.FromHours(50)
        });

        Assert.Equal(42.5, host.CpuUsagePercent);
        Assert.Equal(50.0, host.MemoryUsedPercent);
        Assert.Single(host.CpuPoints);
        Assert.Equal("4.0 GiB / 8.0 GiB", host.MemoryText);
        Assert.True(host.IsOnline);
        Assert.Equal("node", host.Name);
        Assert.Equal("root@192.0.2.5:22", host.Endpoint);
    }

    [Fact]
    public void HostViewModel_ApplyErrorMarksOffline()
    {
        var host = new HostViewModel(new HostConfig { Name = "node" }, historyLength: 10);

        host.ApplyError("connection refused");

        Assert.False(host.IsOnline);
        Assert.Equal("connection refused", host.LastError);
        Assert.Equal("Unreachable", host.StatusText);
    }

    [Fact]
    public async Task HealthCheckViewModel_RunAppliesResult()
    {
        var config = new HealthCheckConfig { Name = "web", Kind = HealthKind.Http, Target = "https://x" };
        var checker = new FakeHealthChecker(HealthCheckResult.Up(12.4, "HTTP 200 OK"));
        var viewModel = new HealthCheckViewModel(config, checker);

        await viewModel.RunCommand.ExecuteAsync(null);

        Assert.Equal(HealthStatus.Up, viewModel.Status);
        Assert.StartsWith("Up", viewModel.StatusText);
        Assert.Equal("12 ms", viewModel.LatencyText);
    }
}
