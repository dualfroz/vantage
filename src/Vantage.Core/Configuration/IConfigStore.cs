using Vantage.Core.Models;

namespace Vantage.Core.Configuration;

public interface IConfigStore
{
    string Path { get; }

    bool StoresSecretsInKeyring => false;

    Task<VantageConfig> LoadAsync(CancellationToken cancellationToken = default);

    Task SaveAsync(VantageConfig config, CancellationToken cancellationToken = default);
}
