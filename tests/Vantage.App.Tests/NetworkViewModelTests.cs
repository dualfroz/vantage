using Vantage.App.Tests.Fakes;
using Vantage.App.ViewModels;
using Vantage.Core.Collectors.Network;
using Vantage.Core.Models;
using Xunit;

namespace Vantage.App.Tests;

public class NetworkViewModelTests
{
    private static (NetworkViewModel ViewModel, ManualClock Clock) Build()
    {
        var clock = new ManualClock();
        var viewModel = new NetworkViewModel(new MockNetworkProvider(() => clock.Now));
        viewModel.SetHost(new HostConfig { Id = "net-1" });
        return (viewModel, clock);
    }

    [Fact]
    public async Task FirstSample_ListsInterfacesWithoutRates()
    {
        var (viewModel, _) = Build();

        await viewModel.RefreshAsync(CancellationToken.None);

        Assert.NotEmpty(viewModel.Interfaces);
        Assert.DoesNotContain(viewModel.Interfaces, row => row.IsLoopback);
        Assert.True(viewModel.IsWaitingForSecondSample);
        Assert.False(viewModel.HasRates);
        Assert.All(viewModel.Interfaces, row => Assert.Equal("-", row.RxRateText));
        Assert.Empty(viewModel.RxPoints);
    }

    [Fact]
    public async Task SecondSample_ComputesThroughputAndHistory()
    {
        var (viewModel, clock) = Build();
        await viewModel.RefreshAsync(CancellationToken.None);

        clock.Advance(TimeSpan.FromSeconds(5));
        await viewModel.RefreshAsync(CancellationToken.None);

        Assert.True(viewModel.HasRates);
        Assert.False(viewModel.IsWaitingForSecondSample);
        Assert.Single(viewModel.RxPoints);
        Assert.Single(viewModel.TxPoints);
        Assert.True(viewModel.RxPoints[0] > 0);
        Assert.EndsWith("/s", viewModel.TotalRxRateText);
        var eth0 = viewModel.Interfaces.Single(row => row.Name == "eth0");
        Assert.EndsWith("/s", eth0.RxRateText);
        Assert.True(viewModel.RateMaximum >= viewModel.RxPoints[0]);
    }

    [Fact]
    public async Task RowsAreUpdatedInPlaceBetweenSamples()
    {
        var (viewModel, clock) = Build();
        await viewModel.RefreshAsync(CancellationToken.None);
        var eth0 = viewModel.Interfaces.Single(row => row.Name == "eth0");

        clock.Advance(TimeSpan.FromSeconds(3));
        await viewModel.RefreshAsync(CancellationToken.None);

        Assert.Same(eth0, viewModel.Interfaces.Single(row => row.Name == "eth0"));
    }

    [Fact]
    public async Task ShowLoopback_TogglesLoopbackRow()
    {
        var (viewModel, _) = Build();
        await viewModel.RefreshAsync(CancellationToken.None);

        viewModel.ShowLoopback = true;
        Assert.Contains(viewModel.Interfaces, row => row.Name == "lo");

        viewModel.ShowLoopback = false;
        Assert.DoesNotContain(viewModel.Interfaces, row => row.Name == "lo");
    }

    [Fact]
    public async Task SetHost_ResetsSamplesAndHistory()
    {
        var (viewModel, clock) = Build();
        await viewModel.RefreshAsync(CancellationToken.None);
        clock.Advance(TimeSpan.FromSeconds(3));
        await viewModel.RefreshAsync(CancellationToken.None);

        viewModel.SetHost(new HostConfig { Id = "net-2" });

        Assert.Empty(viewModel.Interfaces);
        Assert.Empty(viewModel.RxPoints);
        Assert.False(viewModel.HasRates);
        Assert.Equal("-", viewModel.TotalRxRateText);
    }

    [Fact]
    public async Task ProviderFailure_SurfacesError()
    {
        var viewModel = new NetworkViewModel(new FailingNetworkProvider());
        viewModel.SetHost(new HostConfig());

        await viewModel.RefreshAsync(CancellationToken.None);

        Assert.Equal("cat: /proc/net/dev: Permission denied", viewModel.LastError);
        Assert.False(viewModel.IsBusy);
    }

    private sealed class FailingNetworkProvider : INetworkProvider
    {
        public Task<NetworkSnapshot> GetSnapshotAsync(HostConfig host, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("cat: /proc/net/dev: Permission denied");
    }
}
