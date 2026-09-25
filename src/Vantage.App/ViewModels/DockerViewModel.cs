using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Vantage.Core.Docker;
using Vantage.Core.Models;

namespace Vantage.App.ViewModels;

public partial class DockerViewModel : ViewModelBase, IHostPanel
{
    private readonly IDockerProvider dockerProvider;

    [ObservableProperty] private bool isBusy;
    [ObservableProperty] private string? lastError;
    [ObservableProperty] private DockerContainerViewModel? selectedContainer;
    [ObservableProperty] private bool hasLoaded;

    public DockerViewModel(IDockerProvider dockerProvider)
    {
        this.dockerProvider = dockerProvider;
    }

    public ObservableCollection<DockerContainerViewModel> Containers { get; } = new();

    public HostConfig? Host { get; private set; }

    public bool RefreshOnTick => false;

    public int RunningCount => Containers.Count(c => c.IsRunning);

    public string SummaryText => HasLoaded
        ? $"{Containers.Count} containers, {RunningCount} running"
        : "";

    public bool IsEmpty => HasLoaded && Containers.Count == 0 && string.IsNullOrEmpty(LastError);

    public void SetHost(HostConfig? host)
    {
        Host = host;
        Containers.Clear();
        SelectedContainer = null;
        LastError = null;
        HasLoaded = false;
        NotifySummary();
    }

    [RelayCommand]
    public async Task RefreshAsync(CancellationToken cancellationToken)
    {
        if (Host is null)
            return;

        var host = Host;
        IsBusy = true;
        LastError = null;
        try
        {
            var containers = await dockerProvider.ListContainersAsync(host, cancellationToken);
            if (!ReferenceEquals(host, Host))
                return;

            var selectedId = SelectedContainer?.Id;
            Containers.Clear();
            foreach (var container in containers)
                Containers.Add(new DockerContainerViewModel(container));
            SelectedContainer = Containers.FirstOrDefault(c => c.Id == selectedId);
            HasLoaded = true;
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            LastError = ex.Message;
        }
        finally
        {
            IsBusy = false;
            NotifySummary();
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
            await dockerProvider.ExecuteAsync(Host, action, container.Id, CancellationToken.None);
            await RefreshAsync(CancellationToken.None);
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

    private void NotifySummary()
    {
        OnPropertyChanged(nameof(RunningCount));
        OnPropertyChanged(nameof(SummaryText));
        OnPropertyChanged(nameof(IsEmpty));
    }
}
