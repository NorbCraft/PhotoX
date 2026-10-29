using ImageMagick;

namespace PhotoX.Imaging;

/// <summary>Header-only information about a photo.</summary>
public sealed record ImageInfo(ImageKind Kind, int Width, int Height, int OrientedWidth, int OrientedHeight, string? Error = null)
{
    public long Pixels => (long)Width * Height;
}

/// <summary>Image-size policy (FR-037): ≤ 64 MP normal, 64–210 MP slower, larger refused.</summary>
public static class SizePolicy
{
    public const long NormalMaxPixels = 64_000_000;
    public const long AbsoluteMaxPixels = 210_000_000;

    /// <summary>Approximate peak bytes needed to process an image of this size (Q16 RGBA plus working copies).</summary>
    public static long EstimatedBytes(long pixels) => pixels * 8 * 3;

    public static ImageError Check(long pixels)
    {
        if (pixels > AbsoluteMaxPixels) return ImageError.TooLarge;
        if (pixels > NormalMaxPixels)
        {
            var available = GC.GetGCMemoryInfo().TotalAvailableMemoryBytes;
            if (available > 0 && EstimatedBytes(pixels) > available * 0.6) return ImageError.NotEnoughMemory;
        }

        return ImageError.None;
    }
}

public static class ImageProbe
{
    static ImageProbe() => MagickSetup.EnsureInitialized();

    public static ImageInfo Probe(string path)
    {
        var kind = FormatSniffer.Detect(path);
        if (kind is ImageKind.Heic or ImageKind.OtherImage or ImageKind.Unknown)
        {
            return new ImageInfo(kind, 0, 0, 0, 0);
        }

        try
        {
            using var image = new MagickImage();
            image.Ping(path);
            var w = (int)image.Width;
            var h = (int)image.Height;
            var swap = image.Orientation is OrientationType.LeftTop or OrientationType.RightTop
                or OrientationType.RightBottom or OrientationType.LeftBottom;
            return new ImageInfo(kind, w, h, swap ? h : w, swap ? w : h);
        }
        catch (MagickException ex)
        {
            return new ImageInfo(kind, 0, 0, 0, 0, ex.Message);
        }
    }
}
