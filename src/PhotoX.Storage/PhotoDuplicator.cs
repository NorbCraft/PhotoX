namespace PhotoX.Storage;

/// <summary>Duplicate image (prompt_04 §4): copies a photo next to itself under a free name; never overwrites.</summary>
public static class PhotoDuplicator
{
    /// <summary>Copies <paramref name="path"/> to <c>name_copy.ext</c>, or <c>name_copy_2.ext</c>, … and returns the new path.</summary>
    public static string Duplicate(string path)
    {
        var folder = Path.GetDirectoryName(path)!;
        var stem = Path.GetFileNameWithoutExtension(path);
        var ext = Path.GetExtension(path);
        for (var n = 1; n < 1000; n++)
        {
            var target = Path.Combine(folder, n == 1 ? $"{stem}_copy{ext}" : $"{stem}_copy_{n}{ext}");
            if (File.Exists(target)) continue;
            try
            {
                File.Copy(path, target, overwrite: false);
                return target;
            }
            catch (IOException) when (File.Exists(target))
            {
                // Created meanwhile: try the next name.
            }
        }

        throw new IOException("No free file name.");
    }
}
