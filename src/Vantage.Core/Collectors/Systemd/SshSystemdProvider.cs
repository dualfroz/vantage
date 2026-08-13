using Vantage.Core.Models;
using Vantage.Core.Ssh;

namespace Vantage.Core.Collectors.Systemd;

public sealed class SshSystemdProvider : ISystemdProvider
{
    public const string Command =
        "systemctl list-units --type=service --all --no-legend --no-pager --plain";

    private readonly ISshCommandRunner runner;

    public SshSystemdProvider(ISshCommandRunner runner)
    {
        this.runner = runner;
    }

    public async Task<IReadOnlyList<SystemdUnit>> GetServicesAsync(HostConfig host, CancellationToken cancellationToken)
    {
        var output = await runner.RunAsync(host, Command, cancellationToken).ConfigureAwait(false);
        return SystemdUnitParser.Parse(output);
    }
}
