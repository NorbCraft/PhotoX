using PhotoX.App.Localization;
using PhotoX.Imaging;
using PhotoX.Storage;

namespace PhotoX.App.Services;

/// <summary>Maps typed failures to localized, plain-language explanations (FR-023, FR-031, FR-036).</summary>
public static class ErrorTexts
{
    public static string ForImage(ImageError error) => Localizer.Instance[error switch
    {
        ImageError.UnsupportedHeic => "ErrHeic",
        ImageError.UnsupportedFormat => "ErrUnsupported",
        ImageError.TooLarge => "ErrTooLarge",
        ImageError.NotEnoughMemory => "ErrMemory",
        ImageError.Unreadable => "ErrUnreadable",
        _ => "ErrCorrupt",
    }];

    public static string ForSave(SaveResult result) => result.Error switch
    {
        SaveError.FileMissing => Localizer.Instance["ErrFileMissing"],
        SaveError.ChangedExternally => Localizer.Instance["ErrChangedExternally"],
        SaveError.ReadOnlyFile => Localizer.Instance["ErrReadOnlyFile"],
        SaveError.AccessDenied => Localizer.Instance["ErrAccessDenied"],
        SaveError.DiskFull => Localizer.Instance["ErrDiskFull"],
        SaveError.SourceUnreadable => Localizer.Instance.Format("ErrSourceUnreadable", ForImage(result.ImageError)),
        SaveError.VerifyFailed => Localizer.Instance["ErrVerifyFailed"],
        _ => Localizer.Instance.Format("ErrWriteFailed", result.Detail ?? string.Empty),
    };
}
