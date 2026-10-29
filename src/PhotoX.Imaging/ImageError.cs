namespace PhotoX.Imaging;

/// <summary>Reasons an image cannot be used; the UI maps them to localized explanations (FR-031, FR-036, FR-037).</summary>
public enum ImageError
{
    None,
    UnsupportedHeic,
    UnsupportedFormat,
    Corrupt,
    TooLarge,
    NotEnoughMemory,
    Unreadable,
}

public sealed class ImageLoadException(ImageError error, string message, Exception? inner = null)
    : Exception(message, inner)
{
    public ImageError Error { get; } = error;
}
