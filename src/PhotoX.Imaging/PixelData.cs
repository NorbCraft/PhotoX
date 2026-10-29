using ImageMagick;

namespace PhotoX.Imaging;

/// <summary>8-bit BGRA pixels of an image, ready to copy into a UI bitmap.</summary>
public sealed record PixelData(int Width, int Height, byte[] Bgra)
{
    public static PixelData From(MagickImage image)
    {
        using var pixels = image.GetPixelsUnsafe();
        var bytes = pixels.ToByteArray(PixelMapping.BGRA) ?? throw new InvalidOperationException("No pixel data.");
        return new PixelData((int)image.Width, (int)image.Height, bytes);
    }
}
