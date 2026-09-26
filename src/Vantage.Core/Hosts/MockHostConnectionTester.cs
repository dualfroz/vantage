using Vantage.Core.Models;

namespace Vantage.Core.Hosts;

public sealed class MockHostConnectionTester : IHostConnectionTester
{
    public const string SimulatedDetail =
        "Settings are valid. Mock providers are active, so no network connection was made; " +
        "start with VANTAGE_PROVIDERS=ssh to test against the real host.";

    public Task<ConnectionTestResult> TestAsync(HostConfig host, CancellationToken cancellationToken)
    {
        var errors = HostConfigValidator.Validate(host);
        if (errors.Count > 0)
            return Task.FromResult(ConnectionTestResult.Invalid(errors));

        var step = new ConnectionTestStep("Settings", true, SimulatedDetail, null);
        return Task.FromResult(new ConnectionTestResult(true, true, new[] { step }));
    }
}
