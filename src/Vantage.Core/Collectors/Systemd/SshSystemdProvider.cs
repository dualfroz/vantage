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

    public static string BuildActionCommand(HostConfig host, SystemdAction action, string unit)
    {
        if (string.IsNullOrWhiteSpace(unit))
            throw new ArgumentException("Unit name is required.", nameof(unit));

        var verb = action switch
        {
            SystemdAction.Start => "start",
            SystemdAction.Stop => "stop",
            SystemdAction.Restart => "restart",
            _ => throw new ArgumentOutOfRangeException(nameof(action))
        };

        return ShellArgs.Privileged(host, $"systemctl {verb} -- {ShellArgs.Quote(unit)}");
    }

    public async Task<IReadOnlyList<SystemdUnit>> GetServicesAsync(HostConfig host, CancellationToken cancellationToken)
    {
        var output = await runner.RunAsync(host, Command, cancellationToken).ConfigureAwait(false);
        return SystemdUnitParser.Parse(output);
    }

    public async Task ExecuteAsync(HostConfig host, SystemdAction action, string unit, CancellationToken cancellationToken)
    {
        await runner.RunAsync(host, BuildActionCommand(host, action, unit), cancellationToken).ConfigureAwait(false);
    }
}
