using Vantage.Core.Models;
using Vantage.Core.Ssh;

namespace Vantage.Core.Collectors.Network;

public sealed class SshNetworkProvider : INetworkProvider
{
    public const string Command = "cat /proc/net/dev";

    private readonly ISshCommandRunner runner;
    private readonly Func<DateTimeOffset> clock;

    public SshNetworkProvider(ISshCommandRunner runner, Func<DateTimeOffset>? clock = null)
    {
        this.runner = runner;
        this.clock = clock ?? (() => DateTimeOffset.UtcNow);
    }

    public async Task<NetworkSnapshot> GetSnapshotAsync(HostConfig host, CancellationToken cancellationToken)
    {
        var output = await runner.RunAsync(host, Command, cancellationToken).ConfigureAwait(false);
        return new NetworkSnapshot(clock(), NetDevParser.Parse(output));
    }
}
