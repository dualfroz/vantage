using CommunityToolkit.Mvvm.ComponentModel;
using Vantage.Core.Models;

namespace Vantage.App.ViewModels;

public partial class HostDraftViewModel : ViewModelBase
{
    private HostConfig saved;

    [ObservableProperty] private string name = "";
    [ObservableProperty] private string address = "";
    [ObservableProperty] private decimal? port = 22;
    [ObservableProperty] private string username = "";
    [ObservableProperty] private int authMethodIndex;
    [ObservableProperty] private string privateKeyPath = "";
    [ObservableProperty] private string privateKeyPassphrase = "";
    [ObservableProperty] private string password = "";
    [ObservableProperty] private bool useSudo;
    [ObservableProperty] private string diskMount = "/";
    [ObservableProperty] private string metricsEndpoint = "";
    [ObservableProperty] private bool isNew;

    public HostDraftViewModel(HostConfig host, bool isNew)
    {
        saved = host.Clone();
        this.isNew = isNew;
        Load(saved);
    }

    public string Id => saved.Id;

    public IReadOnlyList<string> AuthMethods { get; } = new[] { "Private key", "Password" };

    public SshAuthMethod AuthMethod => AuthMethodIndex == 1 ? SshAuthMethod.Password : SshAuthMethod.PrivateKey;

    public bool UsesPrivateKey => AuthMethod == SshAuthMethod.PrivateKey;

    public bool UsesPassword => AuthMethod == SshAuthMethod.Password;

    public string DisplayName => !string.IsNullOrWhiteSpace(Name)
        ? Name.Trim()
        : !string.IsNullOrWhiteSpace(Address) ? Address.Trim() : "New host";

    public string EndpointText => string.IsNullOrWhiteSpace(Address)
        ? "No address yet"
        : $"{Username.Trim()}@{Address.Trim()}:{Port ?? 0:0}";

    public string MetricsEndpointHint => string.IsNullOrWhiteSpace(Address)
        ? "http://<address>:9100/metrics"
        : $"http://{Address.Trim()}:9100/metrics";

    public bool IsDirty => IsNew || !Same(ToConfig(), saved);

    public HostConfig ToConfig() => new()
    {
        Id = saved.Id,
        Name = Name.Trim(),
        Address = Address.Trim(),
        Port = Port is { } value && value >= int.MinValue && value <= int.MaxValue ? (int)value : 0,
        Username = Username.Trim(),
        AuthMethod = AuthMethod,
        PrivateKeyPath = UsesPrivateKey && !string.IsNullOrWhiteSpace(PrivateKeyPath) ? PrivateKeyPath.Trim() : null,
        PrivateKeyPassphrase = UsesPrivateKey && !string.IsNullOrEmpty(PrivateKeyPassphrase) ? PrivateKeyPassphrase : null,
        Password = UsesPassword && !string.IsNullOrEmpty(Password) ? Password : null,
        UseSudo = UseSudo,
        DiskMount = DiskMount.Trim(),
        MetricsEndpoint = string.IsNullOrWhiteSpace(MetricsEndpoint) ? null : MetricsEndpoint.Trim()
    };

    public void MarkSaved(HostConfig host)
    {
        saved = host.Clone();
        IsNew = false;
        OnPropertyChanged(nameof(IsDirty));
    }

    public void Revert()
    {
        Load(saved);
        OnPropertyChanged(nameof(IsDirty));
    }

    protected override void OnPropertyChanged(System.ComponentModel.PropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        switch (e.PropertyName)
        {
            case nameof(Name):
            case nameof(Address):
            case nameof(Port):
            case nameof(Username):
            case nameof(PrivateKeyPath):
            case nameof(PrivateKeyPassphrase):
            case nameof(Password):
            case nameof(UseSudo):
            case nameof(DiskMount):
            case nameof(MetricsEndpoint):
            case nameof(IsNew):
                base.OnPropertyChanged(new System.ComponentModel.PropertyChangedEventArgs(nameof(IsDirty)));
                base.OnPropertyChanged(new System.ComponentModel.PropertyChangedEventArgs(nameof(DisplayName)));
                base.OnPropertyChanged(new System.ComponentModel.PropertyChangedEventArgs(nameof(EndpointText)));
                base.OnPropertyChanged(new System.ComponentModel.PropertyChangedEventArgs(nameof(MetricsEndpointHint)));
                break;
            case nameof(AuthMethodIndex):
                base.OnPropertyChanged(new System.ComponentModel.PropertyChangedEventArgs(nameof(AuthMethod)));
                base.OnPropertyChanged(new System.ComponentModel.PropertyChangedEventArgs(nameof(UsesPrivateKey)));
                base.OnPropertyChanged(new System.ComponentModel.PropertyChangedEventArgs(nameof(UsesPassword)));
                base.OnPropertyChanged(new System.ComponentModel.PropertyChangedEventArgs(nameof(IsDirty)));
                break;
        }
    }

    private void Load(HostConfig host)
    {
        Name = host.Name;
        Address = host.Address;
        Port = host.Port;
        Username = host.Username;
        AuthMethodIndex = host.AuthMethod == SshAuthMethod.Password ? 1 : 0;
        PrivateKeyPath = host.PrivateKeyPath ?? "";
        PrivateKeyPassphrase = host.PrivateKeyPassphrase ?? "";
        Password = host.Password ?? "";
        UseSudo = host.UseSudo;
        DiskMount = host.DiskMount;
        MetricsEndpoint = host.MetricsEndpoint ?? "";
    }

    private static bool Same(HostConfig a, HostConfig b) =>
        a.Name == b.Name &&
        a.Address == b.Address &&
        a.Port == b.Port &&
        a.Username == b.Username &&
        a.AuthMethod == b.AuthMethod &&
        (a.PrivateKeyPath ?? "") == (b.AuthMethod == SshAuthMethod.PrivateKey ? b.PrivateKeyPath ?? "" : "") &&
        (a.PrivateKeyPassphrase ?? "") == (b.AuthMethod == SshAuthMethod.PrivateKey ? b.PrivateKeyPassphrase ?? "" : "") &&
        (a.Password ?? "") == (b.AuthMethod == SshAuthMethod.Password ? b.Password ?? "" : "") &&
        a.UseSudo == b.UseSudo &&
        a.DiskMount == b.DiskMount &&
        (a.MetricsEndpoint ?? "") == (b.MetricsEndpoint ?? "");
}
