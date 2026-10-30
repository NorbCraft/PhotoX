using ImageMagick;
using PhotoX.Core.Collage;
using PhotoX.Core.Geometry;
using PhotoX.Core.Id;

namespace PhotoX.Imaging.Collage;

/// <summary>A photo placed in a collage slot: its file and the part of it shown (relative to the upright photo).</summary>
public sealed record CollagePhoto(string Path, NormalizedRect Crop);

/// <summary>
/// Renders a print-ready collage (prompt_04 §5): a white sheet at 300 ppi; each photo is loaded at full resolution,
/// cropped to its selected part and resampled once into its slot. Photos are processed one at a time to bound memory.
/// </summary>
public static class CollageRenderer
{
    public const double Ppi = 300;

    public static (int Width, int Height) PagePixels(double widthMm, double heightMm) => (Units.MmToPx(widthMm, Ppi), Units.MmToPx(heightMm, Ppi));

    public static MagickImage Render(double pageWidthMm, double pageHeightMm, IReadOnlyList<RectD> slotsMm, IReadOnlyList<CollagePhoto?> photos)
    {
        var (w, h) = PagePixels(pageWidthMm, pageHeightMm);
        var page = new MagickImage(MagickColors.White, (uint)w, (uint)h);
        try
        {
            for (var i = 0; i < slotsMm.Count && i < photos.Count; i++)
            {
                if (photos[i] is not { } photo) continue;
                var slot = CollageLayout.ToPixels(slotsMm[i], Ppi);
                if (slot.Width <= 0 || slot.Height <= 0) continue;
                using var image = ImageLoader.Load(photo.Path);
                var crop = CropMath.ToPixelRect(photo.Crop, (int)image.Width, (int)image.Height);
                image.Crop(new MagickGeometry(crop.X, crop.Y, (uint)Math.Max(1, crop.Width), (uint)Math.Max(1, crop.Height)));
                image.ResetPage();
                image.Resize(new MagickGeometry((uint)slot.Width, (uint)slot.Height) { IgnoreAspectRatio = true });
                page.Composite(image, slot.X, slot.Y, CompositeOperator.Over);
            }

            page.Density = new Density(Ppi, Ppi, DensityUnit.PixelsPerInch);
            return page;
        }
        catch
        {
            page.Dispose();
            throw;
        }
    }
}
