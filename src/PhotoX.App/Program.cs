using Avalonia;
using PhotoX.Storage;
using Serilog;

namespace PhotoX.App;

internal static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        // Velopack install/uninstall hooks (DEC-037). Updates are manual: no update check is made.
        Velopack.VelopackApp.Build().Run();
        var paths = new AppPaths();
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .WriteTo.File(Path.Combine(paths.LogDir, "photox-.log"), rollingInterval: RollingInterval.Day, retainedFileCountLimit: 14)
            .CreateLogger();
        AppDomain.CurrentDomain.UnhandledException += (_, e) => Log.Fatal(e.ExceptionObject as Exception, "Unhandled exception");
        TaskScheduler.UnobservedTaskException += (_, e) => Log.Error(e.Exception, "Unobserved task exception");
        try
        {
            Log.Information("PhotoX {Version} starting on {OS}", typeof(Program).Assembly.GetName().Version, Environment.OSVersion);
            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        }
        finally
        {
            Log.CloseAndFlush();
        }
    }

    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
            .UsePlatformDetect()
            // On Linux the window class matches the desktop entry (packaging/linux), so the dock shows the PhotoX icon.
            .With(new X11PlatformOptions { WmClass = "PhotoX" })
            .WithInterFont()
            .LogToTrace();
}
