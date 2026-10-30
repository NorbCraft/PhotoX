using ImageMagick;
using PhotoX.Core.Geometry;
using PhotoX.Core.Printing;

namespace PhotoX.Imaging.Printing;

/// <summary>A print page as a top-down 24-bit BGR bitmap with rows padded to 4 bytes (the Windows DIB layout).</summary>
public sealed record PageBitmap(int Width, int Height, byte[] Bgr)
{
    public int Stride => (Width * 3 + 3) & ~3;
}

/// <summary>
/// Renders print pages (prompt_05): a white page of the device's pixel size with the content placed by
/// <see cref="PrintLayout"/>. The content is resampled once (Lanczos) to its exact device size, so the printer driver
/// gets pixels it does not need to scale. Colours stay sRGB; the driver's colour handling is the only conversion.
/// </summary>
public static class PrintRenderer
{
    public static MagickImage RenderPage(MagickImage content, PrintPlacement placement, int pageWidthPx, int pageHeightPx, double pageWidthMm, double pageHeightMm)
    {
        var page = new MagickImage(MagickColors.White, (uint)pageWidthPx, (uint)pageHeightPx);
        try
        {
            using var image = (MagickImage)content.Clone();
            if (image.HasAlpha)
            {
                image.BackgroundColor = MagickColors.White;
                image.Alpha(AlphaOption.Remove);
            }

            if (placement.Rotate) image.Rotate(90);
            var crop = CropMath.ToPixelRect(placement.SourceCrop, (int)image.Width, (int)image.Height);
            if (crop.Width != (int)image.Width || crop.Height != (int)image.Height)
            {
                image.Crop(new MagickGeometry(crop.X, crop.Y, (uint)Math.Max(1, crop.Width), (uint)Math.Max(1, crop.Height)));
                image.ResetPage();
            }

            // Target edges are rounded to device pixels, like collage slots.
            var kx = pageWidthPx / pageWidthMm;
            var ky = pageHeightPx / pageHeightMm;
            var t = placement.TargetMm;
            var x0 = (int)Math.Round(t.X * kx);
            var y0 = (int)Math.Round(t.Y * ky);
            var w = Math.Max(1, (int)Math.Round(t.Right * kx) - x0);
            var h = Math.Max(1, (int)Math.Round(t.Bottom * ky) - y0);
            if (w != (int)image.Width || h != (int)image.Height) image.Resize(new MagickGeometry((uint)w, (uint)h) { IgnoreAspectRatio = true });
            image.ColorSpace = ColorSpace.sRGB;
            page.Composite(image, x0, y0, CompositeOperator.Over);
            return page;
        }
        catch
        {
            page.Dispose();
            throw;
        }
    }

    /// <summary>The page as 8-bit BGR rows for the Windows printing API.</summary>
    public static PageBitmap ToBitmap(MagickImage page)
    {
        var w = (int)page.Width;
        var h = (int)page.Height;
        using var pixels = page.GetPixelsUnsafe();
        var bgr = pixels.ToByteArray(PixelMapping.BGR) ?? throw new InvalidOperationException("No pixel data.");
        var stride = (w * 3 + 3) & ~3;
        if (stride == w * 3) return new PageBitmap(w, h, bgr);
        var padded = new byte[stride * h];
        for (var y = 0; y < h; y++) Buffer.BlockCopy(bgr, y * w * 3, padded, y * stride, w * 3);
        return new PageBitmap(w, h, padded);
    }
}
