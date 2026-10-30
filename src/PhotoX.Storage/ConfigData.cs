using System.Text.Json;
using PhotoX.Core.Formats;
using PhotoX.Core.Id;

namespace PhotoX.Storage;

/// <summary>
/// Configuration data files in the settings folder (FR-010, FR-040, FR-061): <c>print-formats.json</c> (physical
/// dimensions of the print presets) and <c>id-sizes.json</c> (ID photo sizes). A missing file is created with the
/// planned defaults so it can be edited; an unreadable file is left untouched and the defaults are used.
/// </summary>
public static class ConfigData
{
    public const string PrintFormatsFile = "print-formats.json";
    public const string IdSizesFile = "id-sizes.json";

    /// <summary>Loads both files and applies them. Returns messages for the log (never throws for bad data).</summary>
    public static IReadOnlyList<string> Load(AppPaths paths)
    {
        var messages = new List<string>();
        var formats = LoadOrCreate(Path.Combine(paths.ConfigDir, PrintFormatsFile), PrintFormats.Defaults, messages);
        if (formats is not null) messages.Add($"Print formats configured: {string.Join(", ", PrintFormats.Configure(formats))}");
        var sizesPath = Path.Combine(paths.ConfigDir, IdSizesFile);
        var sizes = LoadOrCreate(sizesPath, IdSizes.Defaults, messages);
        if (sizes is not null)
        {
            sizes = Upgrade(sizesPath, sizes, messages);
            messages.Add($"ID sizes in use: {IdSizes.Configure(sizes)}");
        }

        return messages;
    }

    /// <summary>Entries still equal to an earlier version's default were never edited: they get the current default (DEC-052).</summary>
    private static IReadOnlyList<IdSize> Upgrade(string path, IReadOnlyList<IdSize> sizes, List<string> messages)
    {
        var (upgraded, changed) = IdSizes.UpgradeDefaults(sizes);
        if (!changed) return sizes;
        try
        {
            JsonFile.WriteAtomic(path, upgraded);
            messages.Add($"{IdSizesFile}: unedited earlier defaults updated");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            messages.Add($"{IdSizesFile} could not be updated ({ex.Message}); the new defaults apply for this session.");
        }

        return upgraded;
    }

    private static IReadOnlyList<T>? LoadOrCreate<T>(string path, IReadOnlyList<T> defaults, List<string> messages)
        where T : class
    {
        try
        {
            if (!File.Exists(path))
            {
                JsonFile.WriteAtomic(path, defaults);
                return defaults;
            }

            return JsonFile.Read<List<T>>(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or NotSupportedException)
        {
            messages.Add($"{Path.GetFileName(path)} not used ({ex.Message}); built-in values apply.");
            return null;
        }
    }
}
