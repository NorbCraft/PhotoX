using PhotoX.Core.Editing;

namespace PhotoX.Core.Orders;

public enum SavedState
{
    Edited,
    Reviewed,
}

/// <summary>Per-photo record in the order sidecar (FR-026).</summary>
public sealed class PhotoRecord
{
    /// <summary>File name of the preserved original inside <c>Original/</c>, or null when none was made.</summary>
    public string? Backup { get; set; }

    /// <summary>Size and write time of the backup when PhotoX created it; Finish Order deletes it only if they still match.</summary>
    public long? BackupLength { get; set; }

    public DateTime? BackupWriteTimeUtc { get; set; }

    public SavedState State { get; set; }

    public EditRecipe? Recipe { get; set; }

    public DateTime SavedUtc { get; set; }

    /// <summary>Size and write time of the file PhotoX wrote, to detect later external changes.</summary>
    public long OutputLength { get; set; }

    public DateTime OutputWriteTimeUtc { get; set; }
}

/// <summary>
/// A photo deleted from the gallery (prompt_08 §5): moved into <c>Original/</c> under a name of its own, so it can be
/// restored to the folder. Keyed in the sidecar by that stored file name.
/// </summary>
public sealed class DeletedRecord
{
    /// <summary>The photo's file name in the order folder before it was deleted.</summary>
    public string Name { get; set; } = string.Empty;

    public DateTime DeletedUtc { get; set; }

    /// <summary>Size and write time of the moved file: it is restored or removed only while they still match.</summary>
    public long Length { get; set; }

    public DateTime WriteTimeUtc { get; set; }

    /// <summary>The photo's edit record at deletion (with its kept original, if any); it returns with the photo.</summary>
    public PhotoRecord? Record { get; set; }
}

/// <summary>Small, versioned sidecar stored as <c>Original/.photox-order.json</c> (FR-026, DEC-028).</summary>
public sealed class OrderSidecar
{
    /// <summary>2 since 0.11.0: deleted photos are recorded (prompt_08 §5).</summary>
    public const int CurrentVersion = 2;
    public const string FileName = ".photox-order.json";
    public const string OriginalFolderName = "Original";

    public int Version { get; set; } = CurrentVersion;

    public string App { get; set; } = "PhotoX";

    public Dictionary<string, PhotoRecord> Photos { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Photos deleted from the gallery and kept in <c>Original/</c>, by their stored file name (prompt_08 §5).</summary>
    public Dictionary<string, DeletedRecord> Deleted { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    public PhotoRecord? Find(string fileName) => Photos.GetValueOrDefault(fileName);

    /// <summary>True when nothing is recorded, so the sidecar file is no longer needed.</summary>
    public bool IsEmpty => Photos.Count == 0 && Deleted.Count == 0;

    /// <summary>File names in <c>Original/</c> that records refer to: kept originals and deleted photos.</summary>
    public IEnumerable<string> RecordedOriginalNames() =>
        Photos.Values.Select(r => r.Backup)
            .Concat(Deleted.Values.Select(d => d.Record?.Backup))
            .OfType<string>()
            .Concat(Deleted.Keys);
}
