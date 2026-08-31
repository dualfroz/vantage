using Avalonia;
using Avalonia.Headless;
using Vantage.App;
using Vantage.App.Tests;

[assembly: AvaloniaTestApplication(typeof(HeadlessTestApp))]

namespace Vantage.App.Tests;

public static class HeadlessTestApp
{
    public static AppBuilder BuildAvaloniaApp() => AppBuilder
        .Configure<App>()
        .UseHeadless(new AvaloniaHeadlessPlatformOptions());
}
