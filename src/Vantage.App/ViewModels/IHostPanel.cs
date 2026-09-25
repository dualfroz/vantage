using Vantage.Core.Models;

namespace Vantage.App.ViewModels;

public interface IHostPanel
{
    HostConfig? Host { get; }

    bool IsBusy { get; }

    bool RefreshOnTick { get; }

    void SetHost(HostConfig? host);

    Task RefreshAsync(CancellationToken cancellationToken);
}
