using ImageMagick;

namespace PhotoX.Imaging;

public static class ImageEncoder
{
    /// <summary>Encodes to memory so the caller controls the file write and flushing (FR-022).</summary>
    public static byte[] Encode(MagickImage image, ImageKind kind, OutputOptions options)
    {
        using var ms = new MemoryStream();
        ImageWriter.Write(image, ms, kind, options);
        return ms.ToArray();
    }
}
