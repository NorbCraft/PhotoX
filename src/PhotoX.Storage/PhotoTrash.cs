using PhotoX.Core.Orders;

namespace PhotoX.Storage;

/// <summary>A photo deleted from the gallery, kept in <c>Original/</c> under <see cref="StoredName"/> (prompt_08 §5).</summary>
public sealed record DeletedPhoto(string StoredName, string StoredPath, string Name, DateTime DeletedUtc, long Length, DateTime WriteTimeUtc)
{
    public PhotoEntry Entry(Imaging.ImageKind kind) => new(StoredPath, Name, Length, WriteTimeUtc, kind);
}

public enum TrashError
{
    /// <summary>The photo (or the deleted photo in <c>Original/</c>) is not there.</summary>
    NotFound,

    /// <summary>The file in <c>Original/</c> is not the one PhotoX moved there (size or time differ).</summary>
    NotVerified,

    /// <summary>Moving failed (in use, access denied); nothing changed.</summary>
    MoveFailed,
}

public sealed record TrashFailure(string Name, TrashError Error, string? Detail = null);

public sealed record DeleteResult(IReadOnlyList<DeletedPhoto> Deleted, IReadOnlyList<TrashFailure> Failed);

/// <summary>A deleted photo back in the order folder, under its own name or, when that was taken, a new free name.</summary>
public sealed record RestoredPhoto(string StoredName, string Name, string Path)
{
    public bool Renamed(string originalName) => !string.Equals(Name, originalName, StringComparison.Ordinal);
}

public sealed record RestoreDeletedResult(IReadOnlyList<RestoredPhoto> Restored, IReadOnlyList<TrashFailure> Failed);

/// <summary>
/// Delete and restore photos of an order (prompt_08 §5). Deleting never removes a file: the photo is moved (renamed)
/// into <c>Original/</c> under a free name such as "IMG_1 (deleted).jpg" and recorded in the sidecar with its size,
/// write time and edit record, so it leaves the gallery but stays recoverable. A name already used in
/// <c>Original/</c> (a kept original, a customer's file) is never reused or overwritten. Restoring moves it back under
/// its own name, or "name (2).ext" when a photo of that name exists now. Only Discard Originals removes deleted photos
/// for good (<see cref="OrderBackups.DiscardFolder"/>).
/// </summary>
public static class PhotoTrash
{
    /// <summary>Moves the named photos of <paramref name="folder"/> into <c>Original/</c> and records them as deleted.</summary>
    public static DeleteResult Delete(string folder, IEnumerable<string> names)
    {
        var store = new OrderStore(folder);
        var sidecar = store.Load();
        Directory.CreateDirectory(store.OriginalDir);
        var taken = OrderStore.NamesIn(store.OriginalDir);
        taken.UnionWith(sidecar.RecordedOriginalNames());

        // 1. Record every deletion before any file moves, so a moved photo is always recorded.
        var planned = new List<(string Name, string Source, string StoredName, DeletedRecord Record)>();
        var failed = new List<TrashFailure>();
        foreach (var name in names.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var source = Path.Combine(folder, name);
            if (!OrderStore.IsPlainFileName(name) || !File.Exists(source))
            {
                failed.Add(new TrashFailure(name, TrashError.NotFound));
                continue;
            }

            var info = new FileInfo(source);
            var stored = OrderStore.FreeName(name, taken, n => $" (deleted {n})", " (deleted)");
            taken.Add(stored);
            var record = new DeletedRecord
            {
                Name = name,
                DeletedUtc = DateTime.UtcNow,
                Length = info.Length,
                WriteTimeUtc = info.LastWriteTimeUtc,
                Record = sidecar.Find(name),
            };
            sidecar.Deleted[stored] = record;
            planned.Add((name, source, stored, record));
        }

        if (planned.Count == 0) return new DeleteResult([], failed);
        try
        {
            store.Save(sidecar);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Not recorded, so nothing moves.
            return new DeleteResult([], [.. failed, .. planned.Select(p => new TrashFailure(p.Name, TrashError.MoveFailed, ex.Message))]);
        }

        // 2. Move each photo; one that cannot move stays in the folder and loses its deletion record.
        var deleted = new List<DeletedPhoto>();
        foreach (var (name, source, storedName, record) in planned)
        {
            var target = Path.Combine(store.OriginalDir, storedName);
            try
            {
                File.Move(source, target, overwrite: false);
                sidecar.Photos.Remove(name);
                deleted.Add(new DeletedPhoto(storedName, target, name, record.DeletedUtc, record.Length, record.WriteTimeUtc));
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                sidecar.Deleted.Remove(storedName);
                failed.Add(new TrashFailure(name, TrashError.MoveFailed, ex.Message));
            }
        }

        TrySave(store, sidecar);
        return new DeleteResult(deleted, failed);
    }

    /// <summary>The deleted photos of <paramref name="folder"/> that are still in <c>Original/</c> as PhotoX left them.</summary>
    public static IReadOnlyList<DeletedPhoto> List(string folder)
    {
        var store = new OrderStore(folder);
        var sidecar = store.Load();
        var result = new List<DeletedPhoto>();
        foreach (var (stored, record) in sidecar.Deleted)
        {
            var path = Path.Combine(store.OriginalDir, stored);
            if (!OrderStore.IsPlainFileName(stored) || !IsOwn(path, record)) continue;
            result.Add(new DeletedPhoto(stored, path, record.Name, record.DeletedUtc, record.Length, record.WriteTimeUtc));
        }

        result.Sort((a, b) => Core.Naming.NaturalComparer.Instance.Compare(a.Name, b.Name));
        return result;
    }

    /// <summary>
    /// Moves deleted photos (by stored name) back into <paramref name="folder"/> with their edit record. A photo whose
    /// name is taken by a file now (or by another record) comes back as "name (2).ext"; nothing is overwritten.
    /// </summary>
    public static RestoreDeletedResult Restore(string folder, IEnumerable<string> storedNames)
    {
        var store = new OrderStore(folder);
        var sidecar = store.Load();
        var restored = new List<RestoredPhoto>();
        var failed = new List<TrashFailure>();
        foreach (var stored in storedNames.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (!OrderStore.IsPlainFileName(stored) || !sidecar.Deleted.TryGetValue(stored, out var record) || !OrderStore.IsPlainFileName(record.Name))
            {
                failed.Add(new TrashFailure(stored, TrashError.NotFound));
                continue;
            }

            var source = Path.Combine(store.OriginalDir, stored);
            if (!File.Exists(source))
            {
                failed.Add(new TrashFailure(record.Name, TrashError.NotFound));
                continue;
            }

            if (!IsOwn(source, record))
            {
                failed.Add(new TrashFailure(record.Name, TrashError.NotVerified));
                continue;
            }

            var taken = OrderStore.NamesIn(folder);
            taken.UnionWith(sidecar.Photos.Keys);
            var name = OrderStore.FreeName(record.Name, taken, n => $" ({n})");
            var target = Path.Combine(folder, name);
            try
            {
                // The file goes back first: if recording fails after that, the photo is visible again (only its
                // edit state is lost), never hidden away unrecorded.
                File.Move(source, target, overwrite: false);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                failed.Add(new TrashFailure(record.Name, TrashError.MoveFailed, ex.Message));
                continue;
            }

            sidecar.Deleted.Remove(stored);
            if (record.Record is { } edit) sidecar.Photos[name] = edit;
            restored.Add(new RestoredPhoto(stored, name, target));
        }

        if (restored.Count > 0)
        {
            TrySave(store, sidecar);
            OrderBackups.RemoveOriginalFolderIfEmpty(store);
        }

        return new RestoreDeletedResult(restored, failed);
    }

    /// <summary>The file is the one PhotoX moved: same size and write time as recorded.</summary>
    internal static bool IsOwn(string path, DeletedRecord record)
    {
        var info = new FileInfo(path);
        return info.Exists && info.Length == record.Length && info.LastWriteTimeUtc == record.WriteTimeUtc;
    }

    private static void TrySave(OrderStore store, OrderSidecar sidecar)
    {
        try
        {
            if (!sidecar.IsEmpty) store.Save(sidecar);
            else if (File.Exists(store.SidecarPath)) File.Delete(store.SidecarPath);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }
    }
}
