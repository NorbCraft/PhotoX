using PhotoX.Core.Orders;

namespace PhotoX.Storage;

public sealed record CleanupResult(int DeletedOriginals, int DeletedTemporary, bool OriginalFolderRemoved, IReadOnlyList<string> Remaining)
{
    public bool Complete => Remaining.Count == 0;
}

/// <summary>
/// Finish Order cleanup (FR-072): deletes only originals and history recorded by PhotoX for this order, its
/// temporary files and cached thumbnails. Finished photos, ID exports and unknown files are never touched;
/// <c>Original/</c> is removed only when empty.
/// </summary>
public static class OrderCleanup
{
    public static CleanupResult Erase(string folder, ThumbnailCache? cache)
    {
        var store = new OrderStore(folder);
        var sidecar = store.Load();
        var remaining = new List<string>();
        int originals = 0, temps = 0;

        foreach (var (name, record) in sidecar.Photos)
        {
            if (record.Backup is null) continue;
            var backup = Path.Combine(store.OriginalDir, record.Backup);
            if (!File.Exists(backup)) continue;
            if (TryDelete(backup)) originals++;
            else remaining.Add(backup);
            _ = name;
        }

        foreach (var tmp in TempFiles(folder).Concat(TempFiles(store.OriginalDir)))
        {
            if (TryDelete(tmp)) temps++;
            else remaining.Add(tmp);
        }

        if (File.Exists(store.SidecarPath) && !TryDelete(store.SidecarPath)) remaining.Add(store.SidecarPath);

        try
        {
            cache?.RemoveFolder(folder);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }

        var removed = false;
        try
        {
            if (Directory.Exists(store.OriginalDir) && !Directory.EnumerateFileSystemEntries(store.OriginalDir).Any())
            {
                Directory.Delete(store.OriginalDir);
                removed = true;
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }

        return new CleanupResult(originals, temps, removed, remaining);
    }

    private static IEnumerable<string> TempFiles(string dir) =>
        Directory.Exists(dir)
            ? Directory.EnumerateFiles(dir, "*" + SaveNames.TempSuffix).Concat(Directory.EnumerateFiles(dir, OrderSidecar.FileName + ".tmp"))
            : [];

    private static bool TryDelete(string path)
    {
        try
        {
            File.SetAttributes(path, FileAttributes.Normal);
            File.Delete(path);
            return true;
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }
}
