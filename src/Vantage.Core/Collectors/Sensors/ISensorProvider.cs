using Vantage.Core.Models;

namespace Vantage.Core.Collectors.Sensors;

public interface ISensorProvider
{
    Task<SensorSnapshot> GetSnapshotAsync(HostConfig host, CancellationToken cancellationToken);
}
