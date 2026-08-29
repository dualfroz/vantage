using CommunityToolkit.Mvvm.ComponentModel;
using Vantage.Core.Models;

namespace Vantage.App.ViewModels;

public partial class DockerContainerViewModel : ViewModelBase
{
    [ObservableProperty] private DockerContainer container;

    public DockerContainerViewModel(DockerContainer container)
    {
        this.container = container;
    }

    public string Id => Container.Id;
    public string Name => Container.Name;
    public string Image => Container.Image;
    public string StatusText => Container.StatusText;
    public bool IsRunning => Container.IsRunning;
    public ContainerState State => Container.State;

    partial void OnContainerChanged(DockerContainer value)
    {
        OnPropertyChanged(nameof(Name));
        OnPropertyChanged(nameof(Image));
        OnPropertyChanged(nameof(StatusText));
        OnPropertyChanged(nameof(IsRunning));
        OnPropertyChanged(nameof(State));
    }
}
