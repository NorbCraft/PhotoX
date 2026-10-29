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
    /// Backup name for a first save: the file's own name, unless a file of that name (case-insensitive) already
    /// exists in <c>Original/</c>; then "name (2).ext", "name (3).ext" … (FR-021). Never an existing name.
    /// </summary>
    public string ChooseBackupName(string fileName)
    {
        var existing = Directory.Exists(OriginalDir)
            ? new HashSet<string>(Directory.EnumerateFiles(OriginalDir).Select(Path.GetFileName)!, StringComparer.OrdinalIgnoreCase)
            : new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (!existing.Contains(fileName)) return fileName;
        var stem = Path.GetFileNameWithoutExtension(fileName);
        var ext = Path.GetExtension(fileName);
        for (var n = 2; ; n++)
        {
            var candidate = $"{stem} ({n}){ext}";
            if (!existing.Contains(candidate)) return candidate;
        }
    }
}
