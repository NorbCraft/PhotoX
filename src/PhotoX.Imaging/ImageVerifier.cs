using ImageMagick;

namespace PhotoX.Imaging;

public static class ImageVerifier
{
    /// <summary>Reads back a written file's header and checks format and dimensions (FR-022).</summary>
    public static bool Verify(string path, ImageKind kind, int width, int height)
    {
        try
        {
            var info = new MagickImageInfo(path);
            var formatOk = kind == ImageKind.Jpeg ? info.Format == MagickFormat.Jpeg : info.Format is MagickFormat.Png or MagickFormat.Png8 or MagickFormat.Png24 or MagickFormat.Png32 or MagickFormat.Png48 or MagickFormat.Png64;
            return formatOk && info.Width == (uint)width && info.Height == (uint)height;
        }
        catch (MagickException)
        {
            return false;
        }
    }
}
