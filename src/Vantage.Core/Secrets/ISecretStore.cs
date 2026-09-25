using Vantage.Core.Desktop;

namespace Vantage.Core.Secrets;

public interface ISecretStore
{
    Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default);

    Task<string?> GetAsync(string reference, CancellationToken cancellationToken = default);

    Task SetAsync(string reference, string label, string secret, CancellationToken cancellationToken = default);

    Task DeleteAsync(string reference, CancellationToken cancellationToken = default);
}

public sealed class UnavailableSecretStore : ISecretStore
{
    public Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default) => Task.FromResult(false);

    public Task<string?> GetAsync(string reference, CancellationToken cancellationToken = default) =>
        throw new InvalidOperationException("No desktop keyring is available.");

    public Task SetAsync(string reference, string label, string secret, CancellationToken cancellationToken = default) =>
        throw new InvalidOperationException("No desktop keyring is available.");

    public Task DeleteAsync(string reference, CancellationToken cancellationToken = default) =>
        throw new InvalidOperationException("No desktop keyring is available.");
}

public static class SecretStores
{
    public static ISecretStore ForCurrentPlatform(SessionBus? sessionBus) =>
        sessionBus is null ? new UnavailableSecretStore() : new SecretServiceStore(sessionBus);
}
