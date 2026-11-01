using PhotoX.Core.Orders;

namespace PhotoX.Storage;

/// <summary>Reads and writes the order sidecar <c>Original/.photox-order.json</c> (FR-026, FR-027).</summary>
public sealed class OrderStore(string folder)
{
    public string Folder { get; } = folder;

    public string OriginalDir => Path.Combine(Folder, OrderSidecar.OriginalFolderName);

    public string SidecarPath => Path.Combine(OriginalDir, OrderSidecar.FileName);

    public OrderSidecar Load()
    {
        try
        {
            var s = JsonFile.Read<OrderSidecar>(SidecarPath);
            if (s is null) return new OrderSidecar();
            // Re-create the dictionary with case-insensitive keys (deserialisation uses the default comparer).
            s.Photos = new Dictionary<string, PhotoRecord>(s.Photos, StringComparer.OrdinalIgnoreCase);
            s.Deleted = new Dictionary<string, DeletedRecord>(s.Deleted ?? [], StringComparer.OrdinalIgnoreCase);
            return s;
        }
        catch (System.Text.Json.JsonException)
        {
            return new OrderSidecar();
        }
    }

    public void Save(OrderSidecar sidecar)
    {
        Directory.CreateDirectory(OriginalDir);
        sidecar.Version = OrderSidecar.CurrentVersion;
        JsonFile.WriteAtomic(SidecarPath, sidecar);
    }

    /// <summary>Full path of the preserved original for <paramref name="fileName"/>, if the sidecar records one that exists.</summary>
    public string? BackupPathFor(OrderSidecar sidecar, string fileName)
    {
        var rec = sidecar.Find(fileName);
        if (rec?.Backup is null) return null;
        var p = Path.Combine(OriginalDir, rec.Backup);
        return File.Exists(p) ? p : null;
    }

    /// <summary>
    /// Repairs photos that PhotoX 0.4.0 left hidden on Windows (prompt_03 §6): clears the Hidden attribute of
    /// photos recorded in the sidecar. Returns how many were repaired. Nothing else is touched.
    /// </summary>
    public int UnhideRecordedPhotos(OrderSidecar sidecar)
    {
        if (!OperatingSystem.IsWindows()) return 0;
        var repaired = 0;
        foreach (var name in sidecar.Photos.Keys)
        {
            if (!IsPlainFileName(name)) continue;
            var path = Path.Combine(Folder, name);
            try
            {
                if (!File.Exists(path) || (File.GetAttributes(path) & FileAttributes.Hidden) == 0) continue;
                PhotoSaver.ClearHidden(path);
                repaired++;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
            }
        }

        return repaired;
    }

    /// <summary>True for a bare file name: no directory parts, no "." or "..". Sidecar names must be bare.</summary>
    public static bool IsPlainFileName(string? name) =>
        !string.IsNullOrWhiteSpace(name) && name is not "." and not ".." && Path.GetFileName(name) == name
        && name.IndexOfAny(['/', '\\', ':']) < 0;

    /// <summary>
    /// Backup name for a first save: the file's own name, unless a file of that name (case-insensitive) already
    /// exists in <c>Original/</c> or a record refers to it; then "name (2).ext", "name (3).ext" … (FR-021). Never an
    /// existing name.
    /// </summary>
    public string ChooseBackupName(string fileName, IEnumerable<string>? recorded = null)
    {
        var taken = NamesIn(OriginalDir);
        taken.UnionWith(recorded ?? []);
        return FreeName(fileName, taken, n => $" ({n})");
    }

    /// <summary>File names in <paramref name="dir"/>, case-insensitive (empty when it does not exist).</summary>
    internal static HashSet<string> NamesIn(string dir) =>
        Directory.Exists(dir)
            ? new HashSet<string>(Directory.EnumerateFileSystemEntries(dir).Select(Path.GetFileName)!, StringComparer.OrdinalIgnoreCase)
            : new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// <paramref name="fileName"/> if it is not taken, otherwise the stem with the first free <paramref name="suffix"/>
    /// (numbered from 2) and the extension.
    /// </summary>
    internal static string FreeName(string fileName, IReadOnlySet<string> taken, Func<int, string> suffix, string? first = null)
    {
        var stem = Path.GetFileNameWithoutExtension(fileName);
        var ext = Path.GetExtension(fileName);
        var candidate = first is null ? fileName : stem + first + ext;
        for (var n = 2; taken.Contains(candidate); n++) candidate = stem + suffix(n) + ext;
        return candidate;
    }
}
