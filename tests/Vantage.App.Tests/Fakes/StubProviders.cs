using Vantage.App;
using Vantage.Core.Collectors.Network;
using Vantage.Core.Collectors.Processes;
using Vantage.Core.Collectors.Sensors;
using Vantage.Core.Collectors.Systemd;
using Vantage.Core.Configuration;
using Vantage.Core.Health;
using Vantage.Core.Hosts;
using Vantage.Core.Logs;
using Vantage.Core.Metrics;
using Vantage.Core.Models;
using Vantage.Core.Secrets;
using Vantage.Core.Ssh;

namespace Vantage.App.Tests.Fakes;

public sealed class ThrowingMetricsProvider : IMetricsProvider
{
    public const string Message = "host unreachable";

    public Task<HostMetrics> GetMetricsAsync(HostConfig host, CancellationToken cancellationToken) =>
        throw new InvalidOperationException(Message);
}

public sealed class FakeHealthChecker : IHealthChecker
{
    private readonly HealthCheckResult result;

    public FakeHealthChecker(HealthCheckResult result)
    {
        this.result = result;
    }

    public int Calls { get; private set; }

    public Task<HealthCheckResult> CheckAsync(HealthCheckConfig check, CancellationToken cancellationToken)
    {
        Calls++;
        return Task.FromResult(result);
    }
}

public sealed class FakeConnectionTester : IHostConnectionTester
{
    private readonly ConnectionTestResult result;

    public FakeConnectionTester(ConnectionTestResult result)
    {
        this.result = result;
    }

    public List<HostConfig> Tested { get; } = new();

    public Task<ConnectionTestResult> TestAsync(HostConfig host, CancellationToken cancellationToken)
    {
        Tested.Add(host);
        return Task.FromResult(result);
    }
}

public sealed class FakeHostKeyStore : IHostKeyStore
{
    public string TrustedKeysPath => "/tmp/vantage/known_hosts";

    public List<HostKeyCandidate> Trusted { get; } = new();

    public HostKeyCheck Check(string host, int port, byte[] keyBlob) =>
        new(HostKeyStatus.Unknown, HostKeyCandidate.From(host, port, keyBlob), Array.Empty<KnownHostMatch>());

    public IReadOnlyList<string> KnownKeyTypes(string host, int port) => Array.Empty<string>();

    public void Trust(HostKeyCandidate key) => Trusted.Add(key);
}

public sealed class SequenceConnectionTester : IHostConnectionTester
{
    private readonly Queue<ConnectionTestResult> results;

    public SequenceConnectionTester(params ConnectionTestResult[] results)
    {
        this.results = new Queue<ConnectionTestResult>(results);
    }

    public List<HostConfig> Tested { get; } = new();

    public Task<ConnectionTestResult> TestAsync(HostConfig host, CancellationToken cancellationToken)
    {
        Tested.Add(host);
        return Task.FromResult(results.Dequeue());
    }
}

public sealed class KeyringBackedConfigStore : IConfigStore
{
    private readonly IConfigStore inner;

    public KeyringBackedConfigStore(IConfigStore inner)
    {
        this.inner = inner;
    }

    public string Path => inner.Path;

    public bool StoresSecretsInKeyring => true;

    public Task<VantageConfig> LoadAsync(CancellationToken cancellationToken = default) => inner.LoadAsync(cancellationToken);

    public Task SaveAsync(VantageConfig config, CancellationToken cancellationToken = default) => inner.SaveAsync(config, cancellationToken);
}

public sealed class FakeSecretStore : ISecretStore
{
    public Dictionary<string, string> Secrets { get; } = new();

    public int AvailabilityChecks { get; private set; }

    public Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default)
    {
        AvailabilityChecks++;
        return Task.FromResult(true);
    }

    public Task<string?> GetAsync(string reference, CancellationToken cancellationToken = default) =>
        Task.FromResult(Secrets.TryGetValue(reference, out var secret) ? secret : null);

    public Task SetAsync(string reference, string label, string secret, CancellationToken cancellationToken = default)
    {
        Secrets[reference] = secret;
        return Task.CompletedTask;
    }

    public Task DeleteAsync(string reference, CancellationToken cancellationToken = default)
    {
        Secrets.Remove(reference);
        return Task.CompletedTask;
    }
}

public sealed class ManualClock
{
    public DateTimeOffset Now { get; set; } = new(2026, 3, 2, 12, 0, 0, TimeSpan.Zero);

    public void Advance(TimeSpan by) => Now += by;
}

public static class TestCollectors
{
    public static HostCollectors Mock(ManualClock? clock = null, IHostConnectionTester? tester = null, IHostKeyStore? hostKeys = null)
    {
        Func<DateTimeOffset>? now = clock is null ? null : () => clock.Now;
        return new HostCollectors(
            new MockNetworkProvider(now),
            new MockProcessProvider(),
            new MockSystemdProvider(),
            new MockSensorProvider(now),
            new MockLogProvider(now),
            tester ?? new MockHostConnectionTester(),
            hostKeys);
    }
}
