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
/// On a page with bleed, content kept at true size inside the finished print has the finished print's outer pixels
/// carried out to the page edge (prompt_11 §1), so a cut slightly off centre shows the same edge, not paper.
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
            if (!placement.CoversPage(pageWidthMm, pageHeightMm))
            {
                var f = placement.FinishedMm;
                var fx = (int)Math.Round(f.X * kx);
                var fy = (int)Math.Round(f.Y * ky);
                ExtendIntoBleed(page, fx, fy, (int)Math.Round(f.Right * kx) - fx, (int)Math.Round(f.Bottom * ky) - fy);
            }

            return page;
        }
        catch
        {
            page.Dispose();
            throw;
        }
    }

    /// <summary>
    /// Fills the bleed around the finished print (x, y, width, height in pixels) with the finished print's outermost
    /// columns and rows, repeated outwards: white margins stay white, a picture touching the edge continues.
    /// </summary>
    public static void ExtendIntoBleed(MagickImage page, int x, int y, int width, int height)
    {
        var (pw, ph) = ((int)page.Width, (int)page.Height);
        x = Math.Clamp(x, 0, pw - 1);
        y = Math.Clamp(y, 0, ph - 1);
        width = Math.Clamp(width, 1, pw - x);
        height = Math.Clamp(height, 1, ph - y);
        if (x == 0 && y == 0 && width == pw && height == ph) return;
        void Stretch(int sx, int sy, int sw, int sh, int tx, int ty, int tw, int th)
        {
            if (tw <= 0 || th <= 0) return;
            using var strip = (MagickImage)page.CloneArea(sx, sy, (uint)sw, (uint)sh);
            strip.Sample(new MagickGeometry((uint)tw, (uint)th) { IgnoreAspectRatio = true });
            page.Composite(strip, tx, ty, CompositeOperator.Copy);
        }

        Stretch(x, y, 1, height, 0, y, x, height);                                        // left
        Stretch(x + width - 1, y, 1, height, x + width, y, pw - x - width, height);       // right
        Stretch(0, y, pw, 1, 0, 0, pw, y);                                                 // top, corners included
        Stretch(0, y + height - 1, pw, 1, 0, y + height, pw, ph - y - height);            // bottom
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
