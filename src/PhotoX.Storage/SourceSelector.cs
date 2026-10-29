using PhotoX.Core.Editing;
using PhotoX.Core.Orders;

namespace PhotoX.Storage;

/// <summary>Decides where an edit starts from (FR-026): the preserved original, or the file itself.</summary>
public static class SourceSelector
{
    /// <summary>
    /// The preserved original, but only when the sidecar records it and the photo is still the file PhotoX
    /// wrote (otherwise the photo was changed outside PhotoX and the backup's relation is uncertain).
    /// </summary>
    public static string? TrustedBackup(OrderStore store, OrderSidecar sidecar, string fileName, FileStamp current)
    {
        var record = sidecar.Find(fileName);
        var backup = store.BackupPathFor(sidecar, fileName);
        if (record is null || backup is null) return null;
        return record.OutputLength == current.Length && record.OutputWriteTimeUtc == current.LastWriteUtc ? backup : null;
    }

    /// <summary>Saved recipe to restore when the photo is reopened, if it is still valid.</summary>
    public static EditRecipe? SavedRecipe(OrderStore store, OrderSidecar sidecar, string fileName, FileStamp current)
    {
        var record = sidecar.Find(fileName);
        if (record?.Recipe is null) return null;
        return record.OutputLength == current.Length && record.OutputWriteTimeUtc == current.LastWriteUtc ? record.Recipe : null;
    }
}
