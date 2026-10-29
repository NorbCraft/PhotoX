using Avalonia;
using Avalonia.Headless;
using PhotoX.App.Tests;

[assembly: AvaloniaTestApplication(typeof(TestAppBuilder))]

namespace PhotoX.App.Tests;

public static class TestAppBuilder
{
    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
            .UseSkia()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false })
            .WithInterFont();
}
