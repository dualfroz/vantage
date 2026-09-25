using Vantage.Core.Configuration;
using Vantage.Core.Models;

namespace Vantage.Core.Secrets;

public sealed class KeyringConfigStore : IConfigStore
{
    private const string ReferencePrefix = "keyring:";

    private readonly IConfigStore inner;
    private readonly ISecretStore secrets;
    private HashSet<string> storedReferences = new();
    private bool? keyringAvailable;

    public KeyringConfigStore(IConfigStore inner, ISecretStore secrets)
    {
        this.inner = inner;
        this.secrets = secrets;
    }

    public string Path => inner.Path;

    public bool StoresSecretsInKeyring => keyringAvailable == true;

    public static string Reference(string hostId, string kind) => $"{ReferencePrefix}{hostId}/{kind}";

    public async Task<VantageConfig> LoadAsync(CancellationToken cancellationToken = default)
    {
        var config = await inner.LoadAsync(cancellationToken).ConfigureAwait(false);
        var available = await IsAvailableAsync(cancellationToken).ConfigureAwait(false);

        var unreachable = new List<string>();
        foreach (var host in config.Hosts)
        {
            if (host.PasswordRef is null && host.PrivateKeyPassphraseRef is null)
                continue;

            if (!available)
            {
                unreachable.Add(DisplayName(host));
                continue;
            }

            host.Password ??= await ResolveAsync(host, host.PasswordRef, "password", config.LoadNotices, cancellationToken).ConfigureAwait(false);
            host.PrivateKeyPassphrase ??= await ResolveAsync(host, host.PrivateKeyPassphraseRef, "key passphrase", config.LoadNotices, cancellationToken).ConfigureAwait(false);
        }

        if (unreachable.Count > 0)
            config.LoadNotices.Add(
                $"The desktop keyring is not reachable, so the saved secrets of {string.Join(", ", unreachable)} could not be read. " +
                "Those hosts cannot log in until the keyring is available again.");

        storedReferences = config.Hosts.SelectMany(References).ToHashSet();
        return config;
    }

    public async Task SaveAsync(VantageConfig config, CancellationToken cancellationToken = default)
    {
        if (!await IsAvailableAsync(cancellationToken).ConfigureAwait(false))
        {
            await inner.SaveAsync(config, cancellationToken).ConfigureAwait(false);
            return;
        }

        var stored = new List<HostConfig>();
        foreach (var host in config.Hosts)
        {
            var copy = host.Clone();
            copy.PasswordRef = await StoreAsync(host, "password", host.Password, host.PasswordRef,
                host.AuthMethod == SshAuthMethod.Password, cancellationToken).ConfigureAwait(false);
            copy.PrivateKeyPassphraseRef = await StoreAsync(host, "passphrase", host.PrivateKeyPassphrase, host.PrivateKeyPassphraseRef,
                host.AuthMethod == SshAuthMethod.PrivateKey, cancellationToken).ConfigureAwait(false);
            copy.Password = null;
            copy.PrivateKeyPassphrase = null;
            stored.Add(copy);
        }

        await inner.SaveAsync(config.WithHosts(stored), cancellationToken).ConfigureAwait(false);

        var kept = stored.SelectMany(References).ToHashSet();
        foreach (var stale in storedReferences.Except(kept))
            await secrets.DeleteAsync(stale, cancellationToken).ConfigureAwait(false);
        storedReferences = kept;
    }

    private async Task<bool> IsAvailableAsync(CancellationToken cancellationToken) =>
        keyringAvailable ??= await secrets.IsAvailableAsync(cancellationToken).ConfigureAwait(false);

    private async Task<string?> ResolveAsync(HostConfig host, string? reference, string what, List<string> notices, CancellationToken cancellationToken)
    {
        if (reference is null)
            return null;

        try
        {
            var secret = await secrets.GetAsync(reference, cancellationToken).ConfigureAwait(false);
            if (secret is null)
                notices.Add($"The keyring has no saved {what} for {DisplayName(host)}. Enter it again in the host editor.");
            return secret;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            notices.Add($"Could not read the {what} of {DisplayName(host)} from the keyring: {ex.Message}");
            return null;
        }
    }

    private async Task<string?> StoreAsync(HostConfig host, string kind, string? secret, string? existingReference, bool inUse, CancellationToken cancellationToken)
    {
        if (secret is null)
            return inUse ? existingReference : null;

        var reference = Reference(host.Id, kind);
        await secrets.SetAsync(reference, $"Vantage {kind} for {DisplayName(host)}", secret, cancellationToken).ConfigureAwait(false);
        return reference;
    }

    private static IEnumerable<string> References(HostConfig host) =>
        new[] { host.PasswordRef, host.PrivateKeyPassphraseRef }.OfType<string>();

    private static string DisplayName(HostConfig host) =>
        !string.IsNullOrWhiteSpace(host.Name) ? host.Name : !string.IsNullOrWhiteSpace(host.Address) ? host.Address : host.Id;
}
