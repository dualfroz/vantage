using Vantage.Core.Models;

namespace Vantage.Core.Collectors.Network;

public interface INetworkProvider
{
    Task<NetworkSnapshot> GetSnapshotAsync(HostConfig host, CancellationToken cancellationToken);
}
