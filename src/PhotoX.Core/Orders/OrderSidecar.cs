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

/// <summary>Small, versioned sidecar stored as <c>Original/.photox-order.json</c> (FR-026, DEC-028).</summary>
public sealed class OrderSidecar
{
    public const int CurrentVersion = 1;
    public const string FileName = ".photox-order.json";
    public const string OriginalFolderName = "Original";

    public int Version { get; set; } = CurrentVersion;

    public string App { get; set; } = "PhotoX";

    public Dictionary<string, PhotoRecord> Photos { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    public PhotoRecord? Find(string fileName) => Photos.GetValueOrDefault(fileName);
}
