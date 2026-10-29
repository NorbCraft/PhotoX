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
        Saver = new PhotoSaver(Settings.JpegQuality);
    }

    public static AppServices Current { get; set; } = null!;

    public AppPaths Paths { get; }

    public SettingsStore SettingsStore { get; }

    public Settings Settings { get; }

    public PhotoSaver Saver { get; }

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
