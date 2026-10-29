using PhotoX.Storage;
using Serilog;

namespace PhotoX.App.Services;

/// <summary>Composition root: paths, settings, saver and logging (kept deliberately small).</summary>
public sealed class AppServices
{
    public AppServices(AppPaths paths)
    {
        Paths = paths;
        SettingsStore = new SettingsStore(paths);
        Settings = SettingsStore.Load();
        ThumbnailCache = new ThumbnailCache(paths.ThumbnailDir, (long)Settings.ThumbnailCacheMaxMb * 1048576, Settings.ThumbnailCacheDays);
    }

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
