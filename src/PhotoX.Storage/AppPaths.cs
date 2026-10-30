namespace PhotoX.Storage;

/// <summary>Per-user locations for settings, logs and caches (ARCHITECTURE §6.4, §8).</summary>
public sealed class AppPaths
{
    public AppPaths(string? rootOverride = null)
    {
        if (rootOverride is not null)
        {
            ConfigDir = Path.Combine(rootOverride, "config");
            DataDir = Path.Combine(rootOverride, "data");
            CacheDir = Path.Combine(rootOverride, "cache");
            UserDataDir = Path.Combine(rootOverride, "userdata");
        }
        else if (OperatingSystem.IsWindows())
        {
            ConfigDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "PhotoX");
            DataDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PhotoX");
            CacheDir = Path.Combine(DataDir, "cache");
            // The installer owns %LOCALAPPDATA%\PhotoX and removes it on uninstall; print history must survive that.
            UserDataDir = ConfigDir;
        }
        else
        {
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            ConfigDir = Path.Combine(Env("XDG_CONFIG_HOME") ?? Path.Combine(home, ".config"), "PhotoX");
            DataDir = Path.Combine(Env("XDG_STATE_HOME") ?? Path.Combine(home, ".local", "state"), "PhotoX");
            CacheDir = Path.Combine(Env("XDG_CACHE_HOME") ?? Path.Combine(home, ".cache"), "PhotoX");
            UserDataDir = Path.Combine(Env("XDG_DATA_HOME") ?? Path.Combine(home, ".local", "share"), "PhotoX");
        }
    }

    public string ConfigDir { get; }

    public string DataDir { get; }

    public string CacheDir { get; }

    /// <summary>
    /// Records that must outlive updates, reinstalls and the cache: print counters and the print history. On Windows
    /// this is the roaming folder of the settings, not %LOCALAPPDATA%\PhotoX, which the installer removes on uninstall.
    /// </summary>
    public string UserDataDir { get; }

    public string SettingsFile => Path.Combine(ConfigDir, "settings.json");

    public string LogDir => Path.Combine(DataDir, "logs");

    public string ThumbnailDir => Path.Combine(CacheDir, "thumbs");

    /// <summary>Print counters (prompt_05 §6), outside the customer's folders.</summary>
    public string PrintHistoryFile => Path.Combine(UserDataDir, "print-history.json");

    /// <summary>Where 0.8 kept the print counters (inside the installation folder on Windows).</summary>
    public string LegacyPrintHistoryFile => Path.Combine(DataDir, "print-history.json");

    /// <summary>The print history (prompt_06 §7): one file of print jobs per month.</summary>
    public string PrintLogDir => Path.Combine(UserDataDir, "print-log");

    /// <summary>
    /// Copies the 0.8 print counters to <see cref="PrintHistoryFile"/> once. The old file is left in place (it goes
    /// with the installation folder).
    /// </summary>
    public void MigratePrintHistory()
    {
        try
        {
            if (File.Exists(PrintHistoryFile) || !File.Exists(LegacyPrintHistoryFile) || PathsEqual(PrintHistoryFile, LegacyPrintHistoryFile)) return;
            Directory.CreateDirectory(UserDataDir);
            File.Copy(LegacyPrintHistoryFile, PrintHistoryFile);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // The counters start empty rather than stopping the app.
        }
    }

    private static bool PathsEqual(string a, string b) => string.Equals(Path.GetFullPath(a), Path.GetFullPath(b), StringComparison.OrdinalIgnoreCase);

    private static string? Env(string name) => Environment.GetEnvironmentVariable(name) is { Length: > 0 } v ? v : null;
}
