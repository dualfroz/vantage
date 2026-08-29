using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Vantage.Core.Docker;
using Vantage.Core.Models;

namespace Vantage.App.ViewModels;

public partial class DockerViewModel : ViewModelBase
{
    private readonly IDockerProvider dockerProvider;

    [ObservableProperty] private bool isBusy;
    [ObservableProperty] private string? lastError;
    [ObservableProperty] private DockerContainerViewModel? selectedContainer;

    public DockerViewModel(IDockerProvider dockerProvider)
    {
        this.dockerProvider = dockerProvider;
    }

    public ObservableCollection<DockerContainerViewModel> Containers { get; } = new();

    public HostConfig? Host { get; private set; }

    public void SetHost(HostConfig? host)
    {
        Host = host;
        Containers.Clear();
    }

    [RelayCommand]
    public async Task RefreshAsync(CancellationToken cancellationToken)
    {
        if (Host is null)
            return;

        IsBusy = true;
        LastError = null;
        try
        {
            var containers = await dockerProvider.ListContainersAsync(Host, cancellationToken).ConfigureAwait(false);
            Containers.Clear();
            foreach (var container in containers)
                Containers.Add(new DockerContainerViewModel(container));
        }
        catch (Exception ex)
        {
            LastError = ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private Task StartAsync(DockerContainerViewModel? container) => ExecuteAsync(DockerAction.Start, container);

    [RelayCommand]
    private Task StopAsync(DockerContainerViewModel? container) => ExecuteAsync(DockerAction.Stop, container);

    [RelayCommand]
    private Task RestartAsync(DockerContainerViewModel? container) => ExecuteAsync(DockerAction.Restart, container);

    private async Task ExecuteAsync(DockerAction action, DockerContainerViewModel? container)
    {
        if (Host is null || container is null)
            return;

        IsBusy = true;
        LastError = null;
        try
        {
            await dockerProvider.ExecuteAsync(Host, action, container.Id, CancellationToken.None).ConfigureAwait(false);
            await RefreshAsync(CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            LastError = ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }
}
