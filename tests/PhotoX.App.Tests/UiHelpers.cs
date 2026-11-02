using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Threading;
using PhotoX.App.Services;
using PhotoX.App.ViewModels;
using PhotoX.App.Views;
using PhotoX.Storage;
using PhotoX.Tests;

namespace PhotoX.App.Tests;

public static class UiHelpers
{
    /// <summary>Folder where headless screenshots are written for manual inspection (not committed).</summary>
    public static string ScreenshotDir { get; } = Path.Combine(Path.GetTempPath(), "photox-screens");

    /// <summary>The printing system of the last window created: a fake printer, since tests never print.</summary>
    public static FakePrintBackend Printer { get; private set; } = new();

    public static (MainWindow Window, ShellViewModel Shell) CreateWindow(string configRoot, int width = 1366, int height = 768, string language = "en")
    {
        Printer = new FakePrintBackend();
        AppServices.Current = new AppServices(new AppPaths(configRoot), Printer);
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
        using var frame = window.CaptureRenderedFrame() ?? throw new InvalidOperationException("No frame");
        var path = Path.Combine(ScreenshotDir, name + ".png");
        using var image = ToMagick(frame);
        image.Write(path, ImageMagick.MagickFormat.Png);
        return path;
    }

    /// <summary>
    /// A copy of a bitmap's pixels, taken under its lock, as a Magick image. Screenshots used to be written with
    /// <c>Bitmap.Save</c>, whose Skia PNG encoder crashed the test process now and then (SIGSEGV in skcms, seen in
    /// crash dumps while saving a captured frame that nothing kept alive, prompt_11); the bitmap is now held and disposed
    /// by the caller, and encoding is Magick.NET's.
    /// </summary>
    public static ImageMagick.MagickImage ToMagick(Avalonia.Media.Imaging.WriteableBitmap bitmap)
    {
        using var locked = bitmap.Lock();
        var (w, h) = (locked.Size.Width, locked.Size.Height);
        var bytes = new byte[w * h * 4];
        for (var y = 0; y < h; y++) System.Runtime.InteropServices.Marshal.Copy(locked.Address + y * locked.RowBytes, bytes, y * w * 4, w * 4);
        var mapping = locked.Format == Avalonia.Platform.PixelFormat.Rgba8888 ? ImageMagick.PixelMapping.RGBA : ImageMagick.PixelMapping.BGRA;
        return new ImageMagick.MagickImage(bytes, new ImageMagick.PixelReadSettings((uint)w, (uint)h, ImageMagick.StorageType.Char, mapping));
    }
}
