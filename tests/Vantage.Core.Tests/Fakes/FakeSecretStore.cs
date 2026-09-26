using Vantage.Core.Secrets;

namespace Vantage.Core.Tests.Fakes;

public sealed class FakeSecretStore : ISecretStore
{
    public FakeSecretStore(bool available = true)
    {
        Available = available;
    }

    public bool Available { get; set; }

    public bool FailWrites { get; set; }

    public Dictionary<string, string> Secrets { get; } = new();

    public Dictionary<string, string> Labels { get; } = new();

    public List<string> Deleted { get; } = new();

    public Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default) => Task.FromResult(Available);

    public Task<string?> GetAsync(string reference, CancellationToken cancellationToken = default) =>
        Task.FromResult(Secrets.TryGetValue(reference, out var secret) ? secret : null);

    public Task SetAsync(string reference, string label, string secret, CancellationToken cancellationToken = default)
    {
        if (FailWrites)
            throw new InvalidOperationException("The keyring prompt was dismissed.");
        Secrets[reference] = secret;
        Labels[reference] = label;
        return Task.CompletedTask;
    }

    public Task DeleteAsync(string reference, CancellationToken cancellationToken = default)
    {
        Secrets.Remove(reference);
        Deleted.Add(reference);
        return Task.CompletedTask;
    }
}
