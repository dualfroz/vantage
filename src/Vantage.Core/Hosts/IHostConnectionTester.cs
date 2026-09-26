using Vantage.Core.Models;
using Vantage.Core.Ssh;

namespace Vantage.Core.Hosts;

public interface IHostConnectionTester
{
    Task<ConnectionTestResult> TestAsync(HostConfig host, CancellationToken cancellationToken);
}

public sealed record ConnectionTestStep(string Name, bool Succeeded, string Detail, double? LatencyMs);

public sealed record ConnectionTestResult(
    bool Succeeded,
    bool Simulated,
    IReadOnlyList<ConnectionTestStep> Steps,
    HostKeyCandidate? UnknownHostKey = null)
{
    public static ConnectionTestResult Invalid(IReadOnlyList<string> errors) =>
        new(false, false, errors.Select(error => new ConnectionTestStep("Settings", false, error, null)).ToArray());
}
