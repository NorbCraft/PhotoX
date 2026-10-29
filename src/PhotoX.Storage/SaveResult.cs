using PhotoX.Imaging;

namespace PhotoX.Storage;

public enum SaveOutcome
{
    Saved,
    Reviewed,
    Failed,
}

/// <summary>Plain reasons for a failed save; the UI shows a localized explanation (FR-023).</summary>
public enum SaveError
{
    None,
    FileMissing,
    ChangedExternally,
    ReadOnlyFile,
    AccessDenied,
    DiskFull,
    SourceUnreadable,
    VerifyFailed,
    WriteFailed,
}

public sealed record SaveResult(SaveOutcome Outcome, SaveError Error = SaveError.None, ImageError ImageError = ImageError.None, string? Detail = null, FileStamp? NewStamp = null)
{
    public bool Succeeded => Outcome != SaveOutcome.Failed;

    public static SaveResult Fail(SaveError error, string? detail = null, ImageError imageError = ImageError.None) =>
        new(SaveOutcome.Failed, error, imageError, detail);
}
