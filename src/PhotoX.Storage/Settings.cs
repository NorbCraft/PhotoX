namespace PhotoX.Storage;

/// <summary>User settings (FR-009, FR-040). Stored as settings.json.</summary>
public sealed class Settings
{
    /// <summary>2 since 0.9.0: the JPEG quality default became 100 (prompt_06 §9).</summary>
    public const int CurrentVersion = 2;

    public int Version { get; set; } = CurrentVersion;

    public string Language { get; set; } = "ro";

    public string? LastFolder { get; set; }

    public List<string> RecentFolders { get; set; } = [];

    public string? IdOutputFolder { get; set; }

    /// <summary>ID sizes added with + New size (prompt_04 §8), in millimetres.</summary>
    public List<CustomIdSize> CustomIdSizes { get; set; } = [];

    /// <summary>Remembered output folder of the Collages workspace.</summary>
    public string? CollageOutputFolder { get; set; }

    /// <summary>The printer used last (prompt_05).</summary>
    public string? LastPrinter { get; set; }

    /// <summary>Driver settings chosen in each printer's own dialog (Windows DEVMODE, base64), by printer name.</summary>
    public Dictionary<string, string> PrinterSettings { get; set; } = [];

    /// <summary>JPEG quality of saved photos, ID sheets and collages; 100 by default (prompt_06 §9).</summary>
    public int JpegQuality { get; set; } = 100;

    /// <summary>"dark" or "light" (prompt_06 §3).</summary>
    public string Theme { get; set; } = "dark";

    /// <summary>Size of the gallery thumbnails relative to the default (prompt_06 §4).</summary>
    public double ThumbnailScale { get; set; } = 1;

    /// <summary>Widths of the resizable side panels, by panel name (prompt_06 §1).</summary>
    public Dictionary<string, double> PanelWidths { get; set; } = [];

    public int ThumbnailCacheMaxMb { get; set; } = 1024;

    public int ThumbnailCacheDays { get; set; } = 30;

    public bool BrowseDetailsCollapsed { get; set; }

    public bool RecentCollapsed { get; set; }

    /// <summary>Width of the adjustments panel in the Photos editor and the ID Photo Creator (resizable).</summary>
    public double EditorPanelWidth { get; set; } = 340;

    public double IdPanelWidth { get; set; } = 360;

    /// <summary>ID print scale correction in percent (FR-071); 100 = none. Set only after a calibration print.</summary>
    public double IdScalePercent { get; set; } = 100;

    /// <summary>The correction as a factor, limited to a plausible range.</summary>
    public double IdScale => Math.Clamp(IdScalePercent, 90, 110) / 100;

    /// <summary>
    /// Brings settings written by an earlier version up to date. Version 1 stored the old default quality 95 in every
    /// settings file; that value becomes the new default 100. Any other quality was chosen by the operator and stays.
    /// </summary>
    public bool Upgrade()
    {
        if (Version >= CurrentVersion) return false;
        if (Version < 2 && JpegQuality == 95) JpegQuality = 100;
        Version = CurrentVersion;
        return true;
    }

    public void AddRecent(string folder)
    {
        RecentFolders.RemoveAll(f => string.Equals(f, folder, StringComparison.OrdinalIgnoreCase));
        RecentFolders.Insert(0, folder);
        if (RecentFolders.Count > 8) RecentFolders.RemoveRange(8, RecentFolders.Count - 8);
    }
}

public sealed record CustomIdSize(double WidthMm, double HeightMm);

public sealed class SettingsStore(AppPaths paths)
{
    public Settings Load()
    {
        try
        {
            var settings = JsonFile.Read<Settings>(paths.SettingsFile) ?? new Settings();
            if (settings.Upgrade()) TrySave(settings);
            return settings;
        }
        catch (Exception ex) when (ex is IOException or System.Text.Json.JsonException or UnauthorizedAccessException)
        {
            // A damaged settings file must never stop the app; keep it aside and start with defaults.
            try
            {
                File.Move(paths.SettingsFile, paths.SettingsFile + $".invalid-{DateTime.Now:yyyyMMddHHmmss}", true);
            }
            catch (IOException)
            {
            }

            return new Settings();
        }
    }

    public void Save(Settings settings) => JsonFile.WriteAtomic(paths.SettingsFile, settings);

    private void TrySave(Settings settings)
    {
        try
        {
            Save(settings);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // The upgraded values apply in memory and are written with the next change.
        }
    }
}
