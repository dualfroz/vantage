using Vantage.Core.Models;
using Vantage.Core.Ssh;

namespace Vantage.Core.Logs;

public sealed class SshLogProvider : ILogProvider
{
    private readonly ISshCommandRunner runner;

    public SshLogProvider(ISshCommandRunner runner)
    {
        this.runner = runner;
    }

    public async Task<IReadOnlyList<LogLine>> ReadAsync(
        HostConfig host,
        LogSource source,
        LogQuery query,
        CancellationToken cancellationToken)
    {
        var command = LogCommand.Build(source, query);
        var output = await runner.RunAsync(host, command, cancellationToken).ConfigureAwait(false);
        return LogLineParser.Parse(output);
    }
}
