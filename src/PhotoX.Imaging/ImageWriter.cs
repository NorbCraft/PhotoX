using ImageMagick;
using ImageMagick.Formats;

namespace PhotoX.Imaging;

/// <summary>Output encoding policy (FR-033, FR-035, OQ-005): JPEG q95 4:4:4, PNG lossless, embedded sRGB.</summary>
public sealed record OutputOptions(int JpegQuality = 95, double? Ppi = null);

public static class ImageWriter
{
    public static void Write(MagickImage image, string path, ImageKind kind, OutputOptions options)
    {
        using var fs = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None);
        Write(image, fs, kind, options);
    }

    public static void Write(MagickImage image, Stream stream, ImageKind kind, OutputOptions options)
    {
        PrepareMetadata(image, options);
        switch (kind)
        {
            case ImageKind.Jpeg:
                image.Quality = (uint)Math.Clamp(options.JpegQuality, 1, 100);
                image.Settings.SetDefines(new JpegWriteDefines { SamplingFactor = JpegSamplingFactor.Ratio444, OptimizeCoding = true });
                if (image.HasAlpha)
                {
                    image.BackgroundColor = MagickColors.White;
                    image.Alpha(AlphaOption.Remove);
                }

                image.Write(stream, MagickFormat.Jpeg);
                break;
            case ImageKind.Png:
                image.Write(stream, MagickFormat.Png);
                break;
            default:
                throw new NotSupportedException($"Cannot write {kind}.");
        }
    }

    private static void PrepareMetadata(MagickImage image, OutputOptions options)
    {
        image.Orientation = OrientationType.TopLeft;
        var exif = image.GetExifProfile();
        if (exif is not null)
        {
            exif.RemoveThumbnail();
            exif.SetValue(ExifTag.Orientation, (ushort)1);
            exif.SetValue(ExifTag.PixelXDimension, new Number(image.Width));
            exif.SetValue(ExifTag.PixelYDimension, new Number(image.Height));
            exif.SetValue(ExifTag.Software, "PhotoX");
            image.SetProfile(exif);
        }

        // Pixels are sRGB at this point (ColorNormalizer); embed the standard sRGB profile.
        image.SetProfile(ColorProfiles.SRGB);

        if (options.Ppi is double ppi && ppi > 1)
        {
            image.Density = new Density(Math.Round(ppi, 2), Math.Round(ppi, 2), DensityUnit.PixelsPerInch);
        }
    }
}
