using Vantage.Core.Logs;
using Vantage.Core.Models;
using Xunit;

namespace Vantage.Core.Tests.Logs;

public class LogSinceTests
{
    private static readonly DateTimeOffset Now = new(2026, 3, 2, 12, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData("15 min ago", 15)]
    [InlineData("1 hour ago", 60)]
    [InlineData("6 hours ago", 360)]
    [InlineData("2 days ago", 2880)]
    public void RelativePhrases_Resolve(string since, int minutes)
    {
        Assert.True(LogSince.TryResolve(since, Now, out var cutoff));
        Assert.Equal(Now.AddMinutes(-minutes), cutoff);
    }

    [Fact]
    public void TodayAndYesterday_ResolveToMidnight()
    {
        Assert.True(LogSince.TryResolve("today", Now, out var today));
        Assert.True(LogSince.TryResolve("yesterday", Now, out var yesterday));

        Assert.Equal(new DateTimeOffset(2026, 3, 2, 0, 0, 0, TimeSpan.Zero), today);
        Assert.Equal(new DateTimeOffset(2026, 3, 1, 0, 0, 0, TimeSpan.Zero), yesterday);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("whenever")]
    [InlineData("5 fortnights ago")]
    public void UnknownPhrases_DoNotResolve(string? since)
    {
        Assert.False(LogSince.TryResolve(since, Now, out _));
    }

    [Fact]
    public void EveryPresetIsUnderstoodByTheMock()
    {
        foreach (var preset in LogSince.Presets.Where(p => p.Value is not null))
            Assert.True(LogSince.TryResolve(preset.Value, Now, out _), preset.Label);
    }

    [Fact]
    public async Task MockLogProvider_HonorsSinceForJournalSources()
    {
        var provider = new MockLogProvider(() => Now);
        var host = new HostConfig { Name = "demo-host" };

        var all = await provider.ReadAsync(host, LogSource.Journal("nginx"), new LogQuery { Lines = 1000 }, CancellationToken.None);
        var recent = await provider.ReadAsync(host, LogSource.Journal("nginx"), new LogQuery { Lines = 1000, Since = "15 min ago" }, CancellationToken.None);

        Assert.Equal(1000, all.Count);
        Assert.Equal(300, recent.Count);
        Assert.All(recent, line => Assert.True(line.Timestamp >= Now.AddMinutes(-15)));
    }

    [Fact]
    public async Task MockLogProvider_UsesNeutralDocumentationAddresses()
    {
        var provider = new MockLogProvider(() => Now);

        var lines = await provider.ReadAsync(new HostConfig { Name = "demo-host" }, LogSource.Journal("ssh"), new LogQuery { Lines = 1000 }, CancellationToken.None);

        Assert.DoesNotContain(lines, line => line.Raw.Contains(" 10.") || line.Raw.Contains("192.168."));
        Assert.Contains(lines, line => line.Message.StartsWith("Accepted publickey for demo from 192.0.2.4 ", StringComparison.Ordinal));
    }
}
