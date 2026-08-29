using Vantage.Core.Models;
using Vantage.Core.Ssh;

namespace Vantage.Core.Tests.Fakes;

public sealed class FakeSshCommandRunner : ISshCommandRunner
{
    private readonly Func<string, string> respond;

    public FakeSshCommandRunner(string fixedResponse)
        : this(_ => fixedResponse)
    {
    }

    public FakeSshCommandRunner(Func<string, string> respond)
    {
        this.respond = respond;
    }

    public List<string> Commands { get; } = new();

    public Task<string> RunAsync(HostConfig host, string command, CancellationToken cancellationToken)
    {
        Commands.Add(command);
        return Task.FromResult(respond(command));
    }
}
