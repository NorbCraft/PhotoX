using PhotoX.Printing;
using PhotoX.Storage;
using Serilog;

namespace PhotoX.App.Services;

/// <summary>Composition root: paths, settings, saver and logging (kept deliberately small).</summary>
public sealed class AppServices
{
    /// <param name="printBackend">The printing system; by default the Windows driver on Windows and none elsewhere.</param>
    public AppServices(AppPaths paths, IPrintBackend? printBackend = null)
    {
        Paths = paths;
        SettingsStore = new SettingsStore(paths);
        Settings = SettingsStore.Load();
        foreach (var message in ConfigData.Load(paths)) Log.Information("{Message}", message);
        ThumbnailCache = new ThumbnailCache(paths.ThumbnailDir, (long)Settings.ThumbnailCacheMaxMb * 1048576, Settings.ThumbnailCacheDays);
        PrintHistory = new PrintHistory(paths.PrintHistoryFile);
        printBackend ??= OperatingSystem.IsWindows() ? new WindowsPrintBackend() : new UnavailablePrintBackend();
        Printing = new PrintService(printBackend, PrintHistory);
    }

    /// <summary>Print counters per photo (prompt_05 §6).</summary>
    public PrintHistory PrintHistory { get; }

    /// <summary>The print queue (prompt_05 §§2–4).</summary>
    public PrintService Printing { get; }

    public static AppServices Current { get; set; } = null!;

    public AppPaths Paths { get; }

    public SettingsStore SettingsStore { get; }

    public Settings Settings { get; }

    /// <summary>A saver using the current JPEG quality setting.</summary>
    public PhotoSaver Saver => new(Settings.JpegQuality);

    public ThumbnailCache ThumbnailCache { get; }

    public void SaveSettings()
    {
        try
        {
            SettingsStore.Save(Settings);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Log.Warning(ex, "Could not save settings");
        }
    }
}
