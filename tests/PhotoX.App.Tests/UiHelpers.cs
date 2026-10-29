using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Threading;
using PhotoX.App.Services;
using PhotoX.App.ViewModels;
using PhotoX.App.Views;
using PhotoX.Storage;

namespace PhotoX.App.Tests;

public static class UiHelpers
{
    /// <summary>Folder where headless screenshots are written for manual inspection (not committed).</summary>
    public static string ScreenshotDir { get; } = Path.Combine(Path.GetTempPath(), "photox-screens");

    public static (MainWindow Window, ShellViewModel Shell) CreateWindow(string configRoot, int width = 1366, int height = 768, string language = "en")
    {
        AppServices.Current = new AppServices(new AppPaths(configRoot));
        AppServices.Current.Settings.Language = language;
        var shell = new ShellViewModel(AppServices.Current);
        var window = new MainWindow { DataContext = shell, Width = width, Height = height };
        window.Show();
        shell.Initialize();
        return (window, shell);
    }

    /// <summary>Key press followed by release (a real keystroke, not auto-repeat).</summary>
    public static void Tap(Window window, Avalonia.Input.PhysicalKey key)
    {
        window.KeyPressQwerty(key, Avalonia.Input.RawInputModifiers.None);
        window.KeyReleaseQwerty(key, Avalonia.Input.RawInputModifiers.None);
    }

    public static async Task WaitUntil(Func<bool> condition, int timeoutMs = 15000)
    {
        var start = Environment.TickCount64;
        while (!condition())
        {
            Dispatcher.UIThread.RunJobs();
            if (Environment.TickCount64 - start > timeoutMs) throw new TimeoutException("Condition not reached");
            await Task.Delay(20);
        }

        Dispatcher.UIThread.RunJobs();
    }

    public static string Capture(Window window, string name)
    {
        Directory.CreateDirectory(ScreenshotDir);
        Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        var frame = window.CaptureRenderedFrame() ?? throw new InvalidOperationException("No frame");
        var path = Path.Combine(ScreenshotDir, name + ".png");
        frame.Save(path, new Avalonia.Media.Imaging.PngBitmapEncoderOptions());
        return path;
    }
}
