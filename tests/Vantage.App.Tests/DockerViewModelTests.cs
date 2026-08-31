using Vantage.App.ViewModels;
using Vantage.Core.Docker;
using Vantage.Core.Models;
using Xunit;

namespace Vantage.App.Tests;

public class DockerViewModelTests
{
    [Fact]
    public async Task RefreshAsync_PopulatesContainersForHost()
    {
        var viewModel = new DockerViewModel(new MockDockerProvider());
        viewModel.SetHost(new HostConfig { Id = "h1" });

        await viewModel.RefreshAsync(CancellationToken.None);

        Assert.NotEmpty(viewModel.Containers);
    }

    [Fact]
    public async Task RefreshAsync_WithoutHostDoesNothing()
    {
        var viewModel = new DockerViewModel(new MockDockerProvider());

        await viewModel.RefreshAsync(CancellationToken.None);

        Assert.Empty(viewModel.Containers);
    }

    [Fact]
    public async Task StopCommand_TransitionsSelectedContainerToExited()
    {
        var viewModel = new DockerViewModel(new MockDockerProvider());
        viewModel.SetHost(new HostConfig { Id = "h2" });
        await viewModel.RefreshAsync(CancellationToken.None);

        var running = viewModel.Containers.First(c => c.IsRunning);
        viewModel.SelectedContainer = running;

        await viewModel.StopCommand.ExecuteAsync(running);

        var reloaded = viewModel.Containers.First(c => c.Id == running.Id);
        Assert.Equal(ContainerState.Exited, reloaded.State);
        Assert.False(reloaded.IsRunning);
    }
}
