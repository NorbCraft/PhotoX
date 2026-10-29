using ImageMagick;

namespace PhotoX.Imaging;

/// <summary>Creates small JPEG thumbnails for the gallery (FR-053).</summary>
public static class Thumbnailer
{
    public static byte[] Create(string path, int size = 320)
    {
        using var image = ImageLoader.Load(path, size);
        image.Thumbnail(new MagickGeometry((uint)size, (uint)size));
        image.Strip();
        if (image.HasAlpha)
        {
            image.BackgroundColor = new MagickColor("#2B2B2B");
            image.Alpha(AlphaOption.Remove);
        }

        image.Quality = 82;
        return image.ToByteArray(MagickFormat.Jpeg);
    }
}
