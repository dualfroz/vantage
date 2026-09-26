using Vantage.App.Tests.Fakes;
using Vantage.App.ViewModels;
using Vantage.Core.Configuration;
using Vantage.Core.Docker;
using Vantage.Core.Health;
using Vantage.Core.Metrics;
using Xunit;

namespace Vantage.App.Tests;

public class MainWindowViewModelTests
{
    private static IConfigStore EmptyStore() =>
        new JsonConfigStore(Path.Combine(Path.GetTempPath(), "vantage-vm-" + Guid.NewGuid().ToString("N"), "config.json"));

    private static MainWindowViewModel Build(IMetricsProvider metrics, IHealthChecker? health = null) =>
        new(metrics, new MockDockerProvider(), health ?? new HealthChecker(new HttpClient()), EmptyStore(), TestCollectors.Mock());

    [Fact]
    public async Task InitializeAsync_FallsBackToDemoDataWhenConfigEmpty()
    {
        var viewModel = Build(new MockMetricsProvider());

        await viewModel.InitializeAsync();

        Assert.NotEmpty(viewModel.Hosts);
        Assert.NotEmpty(viewModel.HealthChecks);
        Assert.NotNull(viewModel.SelectedHost);
        Assert.Same(viewModel.Hosts[0].Config, viewModel.Docker.Host);
    }

    [Fact]
    public async Task RefreshSelectedHostAsync_PopulatesMetricsAndHistory()
    {
        var viewModel = Build(new MockMetricsProvider());
        await viewModel.InitializeAsync();

        await viewModel.RefreshSelectedHostAsync(CancellationToken.None);

        Assert.True(viewModel.SelectedHost!.IsOnline);
        Assert.NotEmpty(viewModel.SelectedHost.CpuPoints);
        Assert.StartsWith("Updated", viewModel.StatusLine);
    }

    [Fact]
    public async Task RefreshSelectedHostAsync_MarksHostUnreachableOnFailure()
    {
        var viewModel = Build(new ThrowingMetricsProvider());
        await viewModel.InitializeAsync();

        await viewModel.RefreshSelectedHostAsync(CancellationToken.None);

        Assert.False(viewModel.SelectedHost!.IsOnline);
        Assert.Equal(ThrowingMetricsProvider.Message, viewModel.SelectedHost.LastError);
        Assert.Contains(ThrowingMetricsProvider.Message, viewModel.StatusLine);
    }

    [Fact]
    public async Task RunAllHealthAsync_RunsEveryConfiguredCheck()
    {
        var health = new FakeHealthChecker(Vantage.Core.Models.HealthCheckResult.Up(5, "ok"));
        var viewModel = Build(new MockMetricsProvider(), health);
        await viewModel.InitializeAsync();

        await viewModel.RunAllHealthAsync(CancellationToken.None);

        Assert.Equal(viewModel.HealthChecks.Count, health.Calls);
        Assert.All(viewModel.HealthChecks, check => Assert.StartsWith("Up", check.StatusText));
    }
}
