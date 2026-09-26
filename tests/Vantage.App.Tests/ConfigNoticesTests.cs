using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Vantage.App.Tests.Fakes;
using Vantage.App.ViewModels;
using Vantage.App.Views;
using Vantage.Core.Alerts;
using Vantage.Core.Configuration;
using Vantage.Core.Docker;
using Vantage.Core.Health;
using Vantage.Core.Metrics;
using Xunit;

namespace Vantage.App.Tests;

public class ConfigNoticesTests : IDisposable
{
    private readonly string tempDir = Path.Combine(Path.GetTempPath(), "vantage-notices-" + Guid.NewGuid().ToString("N"));

    private string ConfigPath => Path.Combine(tempDir, "config.json");

    private JsonConfigStore Store() => new(ConfigPath, Path.Combine(tempDir, "home"));

    private MainWindowViewModel Build(IConfigStore store, BufferedDesktopNotifier? notifier = null) =>
        new(new MockMetricsProvider(), new MockDockerProvider(), new HealthChecker(new HttpClient()), store,
            TestCollectors.Mock(), desktopNotifier: notifier);

    private void WriteConfig(string json)
    {
        Directory.CreateDirectory(tempDir);
        File.WriteAllText(ConfigPath, json);
    }

    private const string LegacyAgentConfig =
        "{ \"hosts\": [ { \"id\": \"nas\", \"name\": \"nas-01\", \"address\": \"192.0.2.30\", \"username\": \"ops\", \"authMethod\": \"agent\" } ] }";

    [Fact]
    public async Task LegacyAgentConfig_ShowsANoticeOnceAndRewritesTheFile()
    {
        WriteConfig(LegacyAgentConfig);

        var first = Build(Store());
        await first.InitializeAsync();
        var second = Build(Store());
        await second.InitializeAsync();

        Assert.True(first.HasNotices);
        Assert.Contains("nas-01 used SSH agent authentication", Assert.Single(first.Notices));
        Assert.Contains("\"authMethod\": \"privateKey\"", File.ReadAllText(ConfigPath));
        Assert.False(second.HasNotices);
        Assert.Equal("nas-01", second.Hosts.Single().Name);
    }

    [Fact]
    public async Task DismissNotices_HidesTheBanner()
    {
        WriteConfig(LegacyAgentConfig);
        var viewModel = Build(Store());
        await viewModel.InitializeAsync();

        viewModel.DismissNoticesCommand.Execute(null);

        Assert.False(viewModel.HasNotices);
        Assert.Empty(viewModel.Notices);
    }

    [Fact]
    public async Task DesktopNotificationsSetting_ControlsTheNotifier()
    {
        WriteConfig("{ \"hosts\": [ { \"id\": \"a\", \"address\": \"192.0.2.1\", \"username\": \"ops\" } ], \"desktopNotifications\": false }");
        var notifier = new BufferedDesktopNotifier();

        await Build(Store(), notifier).InitializeAsync();

        Assert.False(notifier.SendToDesktop);
    }

    [Theory]
    [InlineData(", \"desktopNotificationsPerMinute\": 10", 10)]
    [InlineData(", \"desktopNotificationsPerMinute\": 0", 1)]
    [InlineData("", BufferedDesktopNotifier.DefaultMaxPerWindow)]
    public async Task DesktopNotificationsPerMinute_SetsTheRateLimit(string setting, int expected)
    {
        WriteConfig("{ \"hosts\": [ { \"id\": \"a\", \"address\": \"192.0.2.1\", \"username\": \"ops\" } ]" + setting + " }");
        var notifier = new BufferedDesktopNotifier { MaxPerWindow = 99 };

        await Build(Store(), notifier).InitializeAsync();

        Assert.Equal(expected, notifier.MaxPerWindow);
    }

    [Fact]
    public async Task HostEditor_KnowsWhetherSecretsGoToTheKeyring()
    {
        var plain = Build(Store());
        var keyring = Build(new KeyringBackedConfigStore(Store()));

        await plain.InitializeAsync();
        await keyring.InitializeAsync();

        Assert.False(plain.HostEditor.SecretsInKeyring);
        Assert.True(keyring.HostEditor.SecretsInKeyring);
    }

    [AvaloniaFact]
    public async Task MainWindow_ShowsNoticesInABanner()
    {
        WriteConfig(LegacyAgentConfig);
        var viewModel = Build(Store());
        await viewModel.InitializeAsync();

        var window = new MainWindow { DataContext = viewModel };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        var texts = window.GetVisualDescendants().OfType<TextBlock>().Where(text => text.IsEffectivelyVisible).Select(text => text.Text).ToArray();
        Assert.Contains(texts, text => text is not null && text.Contains("nas-01 used SSH agent authentication"));
        Assert.Contains(window.GetVisualDescendants().OfType<Button>(), button => Equals(button.Content, "Dismiss") && button.IsEffectivelyVisible);
    }

    public void Dispose()
    {
        if (Directory.Exists(tempDir))
            Directory.Delete(tempDir, recursive: true);
    }
}
