using System.Text.Json;
using System.Text.Json.Serialization;

namespace PhotoX.Storage;

/// <summary>Small JSON helpers with atomic replace-on-write.</summary>
public static class JsonFile
{
    public static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public static T? Read<T>(string path)
        where T : class
    {
        if (!File.Exists(path)) return null;
        using var fs = File.OpenRead(path);
        return JsonSerializer.Deserialize<T>(fs, Options);
    }

    public static void WriteAtomic<T>(string path, T value)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var tmp = path + ".tmp";
        using (var fs = new FileStream(tmp, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            JsonSerializer.Serialize(fs, value, Options);
            fs.Flush(true);
        }

        File.Move(tmp, path, overwrite: true);
    }
}
