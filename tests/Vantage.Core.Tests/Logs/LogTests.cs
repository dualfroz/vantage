using Vantage.Core.Logs;
using Vantage.Core.Models;
using Vantage.Core.Tests.Fakes;
using Vantage.Core.Tests.Fixtures;
using Xunit;

namespace Vantage.Core.Tests.Logs;

public class LogTests
{
    [Fact]
    public void Command_ForJournalUnit_UsesJournalctl()
    {
        var command = LogCommand.Build(LogSource.Journal("nginx"), new LogQuery { Lines = 50 });

        Assert.Contains("journalctl -u 'nginx' -n 50", command);
        Assert.Contains("-o short-iso", command);
    }

    [Fact]
    public void Command_ForFile_UsesTail()
    {
        var command = LogCommand.Build(LogSource.File("/var/log/syslog"), new LogQuery { Lines = 120 });
        Assert.Contains("tail -n 120 -- '/var/log/syslog'", command);
    }

    [Fact]
    public void Command_WithFilter_AppendsGrep()
    {
        var command = LogCommand.Build(LogSource.Journal("sshd"), new LogQuery { Lines = 10, Filter = "Accepted" });
        Assert.Contains("| grep -F -- 'Accepted'", command);
    }

    [Fact]
    public void Command_QuotesFilterToPreventInjection()
    {
        var command = LogCommand.Build(LogSource.Journal("sshd"), new LogQuery { Filter = "a'; rm -rf /" });
        Assert.Contains("grep -F -- 'a'\\''; rm -rf /'", command);
    }

    [Fact]
    public void Parser_ExtractsTimestampUnitAndMessage()
    {
        var lines = LogLineParser.Parse(CollectorFixtures.JournalOutput);

        Assert.Equal(3, lines.Count);
        Assert.Equal("nginx", lines[0].Unit);
        Assert.Equal("GET /api/status 200 12ms", lines[0].Message);
        Assert.NotNull(lines[0].Timestamp);
        Assert.Equal(2026, lines[0].Timestamp!.Value.Year);
    }

    [Fact]
    public void Parser_PlainLineWithoutTimestamp_KeepsRawAsMessage()
    {
        var lines = LogLineParser.Parse("just a plain log line without iso stamp\n");
        var line = Assert.Single(lines);
        Assert.Null(line.Timestamp);
        Assert.Equal("just a plain log line without iso stamp", line.Message);
    }

    [Fact]
    public async Task SshLogProvider_RunsBuiltCommand()
    {
        var runner = new FakeSshCommandRunner(CollectorFixtures.JournalOutput);
        var provider = new SshLogProvider(runner);

        var lines = await provider.ReadAsync(
            new HostConfig(), LogSource.Journal("nginx"), new LogQuery { Lines = 3 }, CancellationToken.None);

        Assert.Contains("journalctl", runner.Commands.Single());
        Assert.Equal(3, lines.Count);
    }

    [Fact]
    public async Task MockLogProvider_HonorsFilter()
    {
        var provider = new MockLogProvider(() => new DateTimeOffset(2026, 8, 26, 12, 0, 0, TimeSpan.Zero));
        var host = new HostConfig { Name = "vps01" };

        var filtered = await provider.ReadAsync(
            host, LogSource.Journal("sshd"), new LogQuery { Lines = 40, Filter = "sshd" }, CancellationToken.None);

        Assert.NotEmpty(filtered);
        Assert.All(filtered, line => Assert.Contains("sshd", line.Raw));
    }
}
