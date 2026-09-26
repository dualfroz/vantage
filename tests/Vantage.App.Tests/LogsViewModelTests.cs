using Vantage.App.Tests.Fakes;
using Vantage.App.ViewModels;
using Vantage.Core.Logs;
using Vantage.Core.Models;
using Xunit;

namespace Vantage.App.Tests;

public class LogsViewModelTests
{
    private sealed class RecordingLogProvider : ILogProvider
    {
        private readonly ILogProvider inner;

        public RecordingLogProvider(ILogProvider inner)
        {
            this.inner = inner;
        }

        public List<(LogSource Source, LogQuery Query)> Calls { get; } = new();

        public Task<IReadOnlyList<LogLine>> ReadAsync(HostConfig host, LogSource source, LogQuery query, CancellationToken cancellationToken)
        {
            Calls.Add((source, query));
            return inner.ReadAsync(host, source, query, cancellationToken);
        }
    }

    private static (LogsViewModel ViewModel, RecordingLogProvider Provider) Build()
    {
        var clock = new ManualClock();
        var provider = new RecordingLogProvider(new MockLogProvider(() => clock.Now));
        var viewModel = new LogsViewModel(provider);
        viewModel.SetSources(new[]
        {
            LogSource.Journal("ssh", "SSH"),
            LogSource.File("/var/log/syslog", "syslog")
        });
        viewModel.SetHost(new HostConfig { Name = "demo-host" });
        return (viewModel, provider);
    }

    [Fact]
    public async Task Refresh_LoadsLinesForSelectedSource()
    {
        var (viewModel, provider) = Build();

        await viewModel.RefreshAsync(CancellationToken.None);

        Assert.Equal(200, viewModel.Lines.Count);
        Assert.Equal("ssh", provider.Calls.Last().Source.Target);
        Assert.Equal("200 lines", viewModel.SummaryText);
        Assert.Equal("journalctl -u ssh", viewModel.SourceTargetText);
        Assert.All(viewModel.Lines, line => Assert.Equal("ssh", line.UnitText));
    }

    [Fact]
    public async Task Filter_IsPassedToProviderAndReported()
    {
        var (viewModel, provider) = Build();
        viewModel.FilterText = "  Accepted publickey  ";

        await viewModel.RefreshAsync(CancellationToken.None);

        Assert.Equal("Accepted publickey", provider.Calls.Last().Query.Filter);
        Assert.NotEmpty(viewModel.Lines);
        Assert.All(viewModel.Lines, line => Assert.Contains("Accepted publickey", line.Message));
        Assert.StartsWith($"{viewModel.Lines.Count} lines containing", viewModel.SummaryText);
    }

    [Fact]
    public async Task ClearFilter_ReloadsEverything()
    {
        var (viewModel, _) = Build();
        viewModel.FilterText = "Invalid user";
        await viewModel.RefreshAsync(CancellationToken.None);
        var filtered = viewModel.Lines.Count;

        await viewModel.ClearFilterCommand.ExecuteAsync(null);

        Assert.Equal("", viewModel.FilterText);
        Assert.True(viewModel.Lines.Count > filtered);
        Assert.Equal("", viewModel.AppliedFilterText);
    }

    [Fact]
    public async Task SincePreset_AppliesOnlyToJournalSources()
    {
        var (viewModel, provider) = Build();
        await viewModel.RefreshAsync(CancellationToken.None);

        viewModel.SelectedSince = LogSince.Presets.Single(p => p.Value == "15 min ago");
        Assert.Equal("15 min ago", provider.Calls.Last().Query.Since);

        viewModel.SelectedSource = viewModel.Sources.Single(s => s.Kind == LogSourceKind.File);

        Assert.False(viewModel.IsJournalSource);
        Assert.Null(provider.Calls.Last().Query.Since);
        Assert.Equal("tail /var/log/syslog", viewModel.SourceTargetText);
    }

    [Fact]
    public async Task LineCount_ReloadsWithNewLimit()
    {
        var (viewModel, provider) = Build();
        await viewModel.RefreshAsync(CancellationToken.None);

        viewModel.SelectedLineCount = 500;

        Assert.Equal(500, provider.Calls.Last().Query.Lines);
        Assert.Equal(500, viewModel.Lines.Count);
    }

    [Fact]
    public void NoSources_ReportsEmptyConfiguration()
    {
        var viewModel = new LogsViewModel(new MockLogProvider());
        viewModel.SetSources(Array.Empty<LogSource>());

        Assert.True(viewModel.HasNoSources);
        Assert.Null(viewModel.SelectedSource);
    }

    [Fact]
    public void AutoRefresh_ControlsTickRefresh()
    {
        var (viewModel, _) = Build();
        Assert.False(viewModel.RefreshOnTick);

        viewModel.AutoRefresh = true;

        Assert.True(viewModel.RefreshOnTick);
    }
}
