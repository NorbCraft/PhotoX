using Avalonia;
using Avalonia.Headless;
using PhotoX.App.Tests;

[assembly: AvaloniaTestApplication(typeof(TestAppBuilder))]

// One application and UI thread for the whole test run. With a new application per test, background work left by an
// earlier test (thumbnails, the print queue) could touch the dispatcher while the next test's application was being
// set up, which failed that test's setup on Linux CI ("a different thread owns it").
[assembly: AvaloniaTestIsolation(AvaloniaTestIsolationLevel.PerAssembly)]

namespace PhotoX.App.Tests;

public static class TestAppBuilder
{
    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
            .UseSkia()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false })
            .WithInterFont();
}
