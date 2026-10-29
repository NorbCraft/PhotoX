using PhotoX.Core.Orders;

namespace PhotoX.Storage;

public sealed record CleanupResult(int DeletedOriginals, int DeletedTemporary, bool OriginalFolderRemoved, IReadOnlyList<string> Remaining)
{
    public bool Complete => Remaining.Count == 0;
}

/// <summary>
/// Finish Order cleanup (FR-072): deletes only originals that PhotoX can identify as its own backups for this
/// order, the edit history, PhotoX temp files and cached thumbnails. Finished photos, ID exports and unknown files
/// are never touched, nothing is deleted recursively, and <c>Original/</c> is removed only when empty.
/// </summary>
public static class OrderCleanup
{
    public static CleanupResult Erase(string folder, ThumbnailCache? cache)
    {
        var store = new OrderStore(folder);
        var sidecar = store.Load();
        var remaining = new List<string>();
        var kept = new Dictionary<string, PhotoRecord>(StringComparer.OrdinalIgnoreCase);
        int originals = 0, temps = 0;

        foreach (var (name, record) in sidecar.Photos)
        {
            if (record.Backup is null) continue;
            if (!OrderStore.IsPlainFileName(name) || !OrderStore.IsPlainFileName(record.Backup)) continue;
            var backup = Path.Combine(store.OriginalDir, record.Backup);
            if (!File.Exists(backup)) continue;
            if (IsOwnBackup(backup, Path.Combine(folder, name), record) && TryDelete(backup))
            {
                originals++;
            }
            else
            {
                kept[name] = record;
                remaining.Add(backup);
            }
        }

        foreach (var tmp in TempFiles(folder).Concat(TempFiles(store.OriginalDir)))
        {
            if (TryDelete(tmp)) temps++;
            else remaining.Add(tmp);
        }

        // History goes with its originals; records of originals left in place stay, so they remain identifiable.
        if (kept.Count == 0)
        {
            if (File.Exists(store.SidecarPath) && !TryDelete(store.SidecarPath)) remaining.Add(store.SidecarPath);
        }
        else
        {
            try
            {
                sidecar.Photos = kept;
                store.Save(sidecar);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                remaining.Add(store.SidecarPath);
            }
        }

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

    /// <summary>
    /// A backup is deleted only while its finished photo still exists in the order folder (otherwise the backup
    /// may be the only copy), and only if it is still the file PhotoX created (same size and write time).
    /// Sidecars from 0.4.0 and earlier did not record the backup identity; their sidecar entry identifies it.
    /// </summary>
    private static bool IsOwnBackup(string backup, string photo, PhotoRecord record)
    {
        var photoInfo = new FileInfo(photo);
        if (!photoInfo.Exists || photoInfo.Length == 0) return false;
        var info = new FileInfo(backup);
        if (record.BackupLength is { } length && info.Length != length) return false;
        if (record.BackupWriteTimeUtc is { } time && info.LastWriteTimeUtc != time) return false;
        return true;
    }

    private static IEnumerable<string> TempFiles(string dir) =>
        Directory.Exists(dir)
            ? Directory.EnumerateFiles(dir, ".*" + SaveNames.TempSuffix).Concat(Directory.EnumerateFiles(dir, OrderSidecar.FileName + ".tmp"))
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
