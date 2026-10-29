using ImageMagick;
using ImageMagick.Formats;

namespace PhotoX.Imaging;

/// <summary>
/// Loads JPEG/PNG photos upright and in sRGB (FR-030, FR-032, FR-034). With <paramref name="maxEdge"/>, a smaller
/// proxy is produced (JPEG DCT downscaling, then box averaging) for previews.
/// </summary>
public static class ImageLoader
{
    static ImageLoader() => MagickSetup.EnsureInitialized();

    public static MagickImage Load(string path, int? maxEdge = null)
    {
        var kind = FormatSniffer.Detect(path);
        switch (kind)
        {
            case ImageKind.Heic:
                throw new ImageLoadException(ImageError.UnsupportedHeic, "HEIC/HEIF is not supported.");
            case ImageKind.OtherImage:
                throw new ImageLoadException(ImageError.UnsupportedFormat, "Only JPEG and PNG are supported.");
            case ImageKind.Unknown:
                throw new ImageLoadException(File.Exists(path) ? ImageError.Corrupt : ImageError.Unreadable, "Not a readable image.");
        }

        var info = ImageProbe.Probe(path);
        if (info.Error is not null || info.Width == 0)
        {
            throw new ImageLoadException(ImageError.Corrupt, info.Error ?? "Invalid image header.");
        }

        if (maxEdge is null)
        {
            var limit = SizePolicy.Check(info.Pixels);
            if (limit != ImageError.None)
            {
                throw new ImageLoadException(limit, $"Image of {info.Width}×{info.Height} exceeds the size policy.");
            }
        }
        else if (info.Pixels > SizePolicy.AbsoluteMaxPixels)
        {
            throw new ImageLoadException(ImageError.TooLarge, $"Image of {info.Width}×{info.Height} is too large.");
        }

        var settings = new MagickReadSettings();
        if (kind == ImageKind.Jpeg && maxEdge is int edge)
        {
            // libjpeg decodes directly at a reduced scale ≥ the hint (much faster for previews and thumbnails).
            settings.Defines = new JpegReadDefines { Size = new MagickGeometry((uint)edge, (uint)edge) };
        }

        MagickImage image;
        try
        {
            image = new MagickImage(path, settings);
        }
        catch (MagickCorruptImageErrorException ex)
        {
            throw new ImageLoadException(ImageError.Corrupt, ex.Message, ex);
        }
        catch (MagickResourceLimitErrorException ex)
        {
            throw new ImageLoadException(ImageError.TooLarge, ex.Message, ex);
        }
        catch (MagickException ex)
        {
            throw new ImageLoadException(ImageError.Corrupt, ex.Message, ex);
        }
        catch (OutOfMemoryException ex)
        {
            throw new ImageLoadException(ImageError.NotEnoughMemory, ex.Message, ex);
        }

        try
        {
            image.AutoOrient();
            ColorNormalizer.ToSrgb(image);
            if (maxEdge is int e && Math.Max(image.Width, image.Height) > e)
            {
                // Previews only: box averaging is a good downscale and about 2.5× faster than the default Lanczos.
                image.Scale(new MagickGeometry((uint)e, (uint)e));
            }

            return image;
        }
        catch
        {
            image.Dispose();
            throw;
        }
    }
}
