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
        }
        else if (OperatingSystem.IsWindows())
        {
            ConfigDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "PhotoX");
            DataDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PhotoX");
            CacheDir = Path.Combine(DataDir, "cache");
        }
        else
        {
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            ConfigDir = Path.Combine(Env("XDG_CONFIG_HOME") ?? Path.Combine(home, ".config"), "PhotoX");
            DataDir = Path.Combine(Env("XDG_STATE_HOME") ?? Path.Combine(home, ".local", "state"), "PhotoX");
            CacheDir = Path.Combine(Env("XDG_CACHE_HOME") ?? Path.Combine(home, ".cache"), "PhotoX");
        }
    }

    public string ConfigDir { get; }

    public string DataDir { get; }

    public string CacheDir { get; }

    public string SettingsFile => Path.Combine(ConfigDir, "settings.json");

    public string LogDir => Path.Combine(DataDir, "logs");

    public string ThumbnailDir => Path.Combine(CacheDir, "thumbs");

    /// <summary>Print counters (prompt_05 §6), outside the customer's folders.</summary>
    public string PrintHistoryFile => Path.Combine(DataDir, "print-history.json");

    private static string? Env(string name) => Environment.GetEnvironmentVariable(name) is { Length: > 0 } v ? v : null;
}
