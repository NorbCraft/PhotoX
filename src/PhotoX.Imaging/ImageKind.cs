namespace PhotoX.Imaging;

/// <summary>Real file format, detected from content rather than the extension (FR-030).</summary>
public enum ImageKind
{
    Unknown,
    Jpeg,
    Png,
    Heic,
    OtherImage,
}

public static class FormatSniffer
{
    public static ImageKind Detect(string path)
    {
        try
        {
            using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            Span<byte> b = stackalloc byte[16];
            var n = fs.Read(b);
            return Detect(b[..n]);
        }
        catch (IOException)
        {
            return ImageKind.Unknown;
        }
        catch (UnauthorizedAccessException)
        {
            return ImageKind.Unknown;
        }
    }

    public static ImageKind Detect(ReadOnlySpan<byte> b)
    {
        if (b.Length >= 3 && b[0] == 0xFF && b[1] == 0xD8 && b[2] == 0xFF) return ImageKind.Jpeg;
        if (b.Length >= 8 && b[0] == 0x89 && b[1] == 0x50 && b[2] == 0x4E && b[3] == 0x47) return ImageKind.Png;
        if (b.Length >= 12 && b[4] == 'f' && b[5] == 't' && b[6] == 'y' && b[7] == 'p')
        {
            var brand = System.Text.Encoding.ASCII.GetString(b.Slice(8, 4));
            return brand is "heic" or "heix" or "hevc" or "heim" or "heis" or "mif1" or "msf1" or "avif"
                ? ImageKind.Heic
                : ImageKind.OtherImage;
        }

        if (b.Length >= 4 && ((b[0] == 'I' && b[1] == 'I') || (b[0] == 'M' && b[1] == 'M'))) return ImageKind.OtherImage; // TIFF / some RAW
        if (b.Length >= 4 && b[0] == 'G' && b[1] == 'I' && b[2] == 'F') return ImageKind.OtherImage;
        if (b.Length >= 12 && b[0] == 'R' && b[1] == 'I' && b[2] == 'F' && b[3] == 'F' && b[8] == 'W' && b[9] == 'E') return ImageKind.OtherImage; // WebP
        if (b.Length >= 2 && b[0] == 'B' && b[1] == 'M') return ImageKind.OtherImage;
        return ImageKind.Unknown;
    }

    /// <summary>Extensions considered photos by the gallery (content is still sniffed).</summary>
    public static bool HasPhotoExtension(string path)
    {
        var ext = Path.GetExtension(path).ToLowerInvariant();
        return ext is ".jpg" or ".jpeg" or ".jpe" or ".png" or ".heic" or ".heif" or ".webp" or ".tif" or ".tiff" or ".dng";
    }
}
