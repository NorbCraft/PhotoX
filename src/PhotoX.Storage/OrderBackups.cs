using System.Security.Cryptography;
using PhotoX.Core.Orders;
using PhotoX.Imaging;

namespace PhotoX.Storage;

/// <summary>Why a backup was kept by <see cref="OrderBackups.RemoveBackups"/>.</summary>
public enum KeepReason
{
    /// <summary>The edited photo is missing, empty or unreadable: the backup may be the only copy.</summary>
    PhotoNotVerified,

    /// <summary>The photo changed outside PhotoX since it was saved, so the backup's relation is uncertain.</summary>
    PhotoChanged,

    /// <summary>The file in <c>Original/</c> is not the one PhotoX created (size or time differ), or its name is not plain.</summary>
    NotOwnBackup,

    /// <summary>Deleting failed (in use, read-only, access denied).</summary>
    DeleteFailed,
}

public sealed record KeptBackup(string Photo, string Backup, KeepReason Reason);

public sealed record RemoveBackupsResult(IReadOnlyList<string> Finalized, IReadOnlyList<KeptBackup> Kept, bool OriginalFolderRemoved)
{
    public bool Complete => Kept.Count == 0;

    /// <summary>Deleted photos removed for good by <see cref="OrderBackups.DiscardFolder"/> (their names in the folder).</summary>
    public IReadOnlyList<string> RemovedDeleted { get; init; } = [];
}

public enum RestoreError
{
    None,
    NoBackup,
    BackupNotVerified,
    ReadOnlyFile,
    WriteFailed,
}

public sealed record RestoreResult(RestoreError Error, string? Detail = null)
{
    public bool Succeeded => Error == RestoreError.None;
}

/// <summary>
/// Save Changes and Restore Original (prompt_05 §5), one photo at a time and only through the sidecar:
/// a file in <c>Original/</c> is touched only when the sidecar names it for that photo with plain names, it still has
/// the size and write time PhotoX recorded, and (for removal) the edited photo is the file PhotoX saved and reads as
/// an image. Nothing is ever deleted recursively; finished photos and unrelated files are never touched (DEC-056).
/// </summary>
public static class OrderBackups
{
    /// <summary>
    /// Finalizes the named photos (after their edits were saved): deletes each verified PhotoX backup and the photo's
    /// edit history. A backup that cannot be verified is kept, stays recorded and is reported.
    /// </summary>
    public static RemoveBackupsResult RemoveBackups(string folder, IEnumerable<string> photoNames)
    {
        var store = new OrderStore(folder);
        var sidecar = store.Load();
        var (finalized, kept) = RemoveBackups(store, sidecar, photoNames);
        SaveOrDeleteSidecar(store, sidecar);
        return new RemoveBackupsResult(finalized, kept, RemoveOriginalFolderIfEmpty(store));
    }

    /// <summary>
    /// Discard Originals for the whole order (prompt_08 §5): every photo recorded in the sidecar is finalized as by
    /// <see cref="RemoveBackups(string, IEnumerable{string})"/>, and every deleted photo is removed for good together with
    /// its kept original. Still one recorded file at a time and only when verified; files the sidecar does not name
    /// (the customer's own, other folders) are never touched and nothing is deleted recursively.
    /// </summary>
    public static RemoveBackupsResult DiscardFolder(string folder)
    {
        var store = new OrderStore(folder);
        var sidecar = store.Load();
        var (finalized, kept) = RemoveBackups(store, sidecar, sidecar.Photos.Keys.ToList());
        var removedDeleted = new List<string>();
        foreach (var (stored, record) in sidecar.Deleted.ToList())
        {
            var photo = OrderStore.IsPlainFileName(stored) ? Path.Combine(store.OriginalDir, stored) : null;
            var backup = record.Record?.Backup is { } b ? (OrderStore.IsPlainFileName(b) ? Path.Combine(store.OriginalDir, b) : null) : string.Empty;
            // Both files must be PhotoX's before either goes (an empty backup path: the photo had no kept original).
            KeepReason? reason = photo is null || backup is null ? KeepReason.NotOwnBackup
                : File.Exists(photo) && !PhotoTrash.IsOwn(photo, record) ? KeepReason.NotOwnBackup
                : backup.Length > 0 && File.Exists(backup) && !IsOwnBackup(backup, record.Record!) ? KeepReason.NotOwnBackup
                : null;
            if (reason is null && TryDelete(photo!) && (backup!.Length == 0 || TryDelete(backup)))
            {
                sidecar.Deleted.Remove(stored);
                removedDeleted.Add(record.Name);
            }
            else
            {
                kept.Add(new KeptBackup(record.Name, photo ?? stored, reason ?? KeepReason.DeleteFailed));
            }
        }

        SaveOrDeleteSidecar(store, sidecar);
        return new RemoveBackupsResult(finalized, kept, RemoveOriginalFolderIfEmpty(store)) { RemovedDeleted = removedDeleted };
    }

    private static (List<string> Finalized, List<KeptBackup> Kept) RemoveBackups(OrderStore store, OrderSidecar sidecar, IEnumerable<string> photoNames)
    {
        var folder = store.Folder;
        var finalized = new List<string>();
        var kept = new List<KeptBackup>();
        foreach (var name in photoNames.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (!OrderStore.IsPlainFileName(name) || sidecar.Find(name) is not { } record) continue;
            if (record.Backup is null)
            {
                // Reviewed without a backup: only the history entry goes.
                sidecar.Photos.Remove(name);
                finalized.Add(name);
                continue;
            }

            var backup = OrderStore.IsPlainFileName(record.Backup) ? Path.Combine(store.OriginalDir, record.Backup) : null;
            if (backup is null)
            {
                kept.Add(new KeptBackup(name, record.Backup, KeepReason.NotOwnBackup));
                continue;
            }

            if (!File.Exists(backup))
            {
                sidecar.Photos.Remove(name); // nothing left to delete
                finalized.Add(name);
                continue;
            }

            var reason = CheckRemovable(Path.Combine(folder, name), backup, record);
            if (reason is null && TryDelete(backup))
            {
                sidecar.Photos.Remove(name);
                finalized.Add(name);
                TryDelete(SaveNames.TempFor(Path.Combine(folder, name)));
            }
            else
            {
                kept.Add(new KeptBackup(name, backup, reason ?? KeepReason.DeleteFailed));
            }
        }

        return (finalized, kept);
    }

    /// <summary>
    /// Restores the preserved original over the photo: copied to a temp file next to it, compared byte for byte,
    /// then swapped in atomically. Only after the photo equals the original are the backup and the history removed.
    /// The current photo content is replaced (the caller warns first).
    /// </summary>
    public static RestoreResult Restore(string photoPath)
    {
        var folder = Path.GetDirectoryName(photoPath)!;
        var name = Path.GetFileName(photoPath);
        var store = new OrderStore(folder);
        var sidecar = store.Load();
        if (sidecar.Find(name) is not { Backup: { } backupName } record || !OrderStore.IsPlainFileName(backupName)) return new RestoreResult(RestoreError.NoBackup);
        var backup = Path.Combine(store.OriginalDir, backupName);
        if (!File.Exists(backup)) return new RestoreResult(RestoreError.NoBackup);
        if (!IsOwnBackup(backup, record) || !ReadsAsImage(backup)) return new RestoreResult(RestoreError.BackupNotVerified);
        if (File.Exists(photoPath) && (File.GetAttributes(photoPath) & FileAttributes.ReadOnly) != 0) return new RestoreResult(RestoreError.ReadOnlyFile);

        var temp = SaveNames.TempFor(photoPath);
        try
        {
            var expected = Hash(backup);
            File.Copy(backup, temp, overwrite: true);
            File.SetLastWriteTimeUtc(temp, File.GetLastWriteTimeUtc(backup));
            if (!Hash(temp).AsSpan().SequenceEqual(expected))
            {
                TryDelete(temp);
                return new RestoreResult(RestoreError.WriteFailed, "copy differs");
            }

            File.Move(temp, photoPath, overwrite: true);
            PhotoSaver.ClearHidden(photoPath);
            if (!Hash(photoPath).AsSpan().SequenceEqual(expected)) return new RestoreResult(RestoreError.WriteFailed, "restored photo differs");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            TryDelete(temp);
            return new RestoreResult(RestoreError.WriteFailed, ex.Message);
        }

        // The photo is the original again: the backup and the history are no longer needed.
        if (TryDelete(backup)) sidecar.Photos.Remove(name);
        else sidecar.Photos[name] = RecordForRestored(record, photoPath);
        SaveOrDeleteSidecar(store, sidecar);
        RemoveOriginalFolderIfEmpty(store);
        return new RestoreResult(RestoreError.None);
    }

    /// <summary>The photo may lose its backup only while it is the readable file PhotoX saved and the backup is still ours.</summary>
    private static KeepReason? CheckRemovable(string photo, string backup, PhotoRecord record)
    {
        var stamp = FileStamp.Of(photo);
        if (stamp is null || stamp.Value.Length == 0 || !ReadsAsImage(photo)) return KeepReason.PhotoNotVerified;
        if (stamp.Value.Length != record.OutputLength || stamp.Value.LastWriteUtc != record.OutputWriteTimeUtc) return KeepReason.PhotoChanged;
        return IsOwnBackup(backup, record) ? null : KeepReason.NotOwnBackup;
    }

    /// <summary>Sidecars from 0.4.0 and earlier did not record the backup identity; their entry identifies it.</summary>
    private static bool IsOwnBackup(string backup, PhotoRecord record)
    {
        var info = new FileInfo(backup);
        if (record.BackupLength is { } length && info.Length != length) return false;
        return record.BackupWriteTimeUtc is not { } time || info.LastWriteTimeUtc == time;
    }

    private static bool ReadsAsImage(string path)
    {
        var info = ImageProbe.Probe(path);
        return info.Error is null && info.Width > 0 && info.Height > 0;
    }

    /// <summary>If the backup could not be deleted after restoring, the record must now describe the restored photo.</summary>
    private static PhotoRecord RecordForRestored(PhotoRecord record, string photo)
    {
        var stamp = FileStamp.Of(photo);
        record.State = SavedState.Reviewed;
        record.Recipe = null;
        record.OutputLength = stamp?.Length ?? 0;
        record.OutputWriteTimeUtc = stamp?.LastWriteUtc ?? default;
        return record;
    }

    private static byte[] Hash(string path)
    {
        using var fs = File.OpenRead(path);
        return SHA256.HashData(fs);
    }

    private static void SaveOrDeleteSidecar(OrderStore store, OrderSidecar sidecar)
    {
        try
        {
            // Deleted photos keep the sidecar too: it is how they are found and restored (prompt_08 §5).
            if (!sidecar.IsEmpty) store.Save(sidecar);
            else if (File.Exists(store.SidecarPath)) TryDelete(store.SidecarPath);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }
    }

    /// <summary>Removes <c>Original/</c> only when it is completely empty (never recursively).</summary>
    internal static bool RemoveOriginalFolderIfEmpty(OrderStore store)
    {
        try
        {
            if (!Directory.Exists(store.OriginalDir) || Directory.EnumerateFileSystemEntries(store.OriginalDir).Any()) return false;
            Directory.Delete(store.OriginalDir, recursive: false);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static bool TryDelete(string path)
    {
        try
        {
            if (!File.Exists(path)) return true;
            File.SetAttributes(path, FileAttributes.Normal);
            File.Delete(path);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }
}
