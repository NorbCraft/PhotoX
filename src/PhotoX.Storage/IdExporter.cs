using PhotoX.Core.Id;

namespace PhotoX.Storage;

/// <summary>Writes ID outputs with timestamp names, never overwriting (FR-068, FR-075).</summary>
public static class IdExporter
{
    public static string Write(string folder, byte[] bytes, string extension, DateTime localNow, string? baseName = null)
    {
        Directory.CreateDirectory(folder);
        var candidates = baseName is null ? IdFileNames.Candidates(localNow, extension) : Suffixed(baseName, extension);
        foreach (var name in candidates.Take(1000))
        {
            var path = Path.Combine(folder, name);
            try
            {
                using var fs = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
                fs.Write(bytes);
                fs.Flush(true);
                return path;
            }
            catch (IOException) when (File.Exists(path))
            {
                // Taken: try the next suffix. An existing output is never overwritten.
            }
        }

        throw new IOException("No free file name.");
    }

    private static IEnumerable<string> Suffixed(string baseName, string extension)
    {
        yield return baseName + extension;
        for (var n = 1; ; n++) yield return $"{baseName}_{n}{extension}";
    }
}
