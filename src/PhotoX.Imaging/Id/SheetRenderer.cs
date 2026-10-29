using ImageMagick;
using PhotoX.Core.Id;

namespace PhotoX.Imaging.Id;

/// <summary>Composes the 4×6 in sheet at 300 ppi (FR-066–FR-068): white page (portrait or landscape) with identical photos.</summary>
public static class SheetRenderer
{
    public const int PageShortPx = 1200; // 101.6 mm at 300 ppi
    public const int PageLongPx = 1800;  // 152.4 mm at 300 ppi

    /// <summary>
    /// Pixel size of each photo with the print scale correction of FR-071 (1 = none): a printer that enlarges by 2 %
    /// is corrected with 0.98, so the printed photo has its nominal size.
    /// </summary>
    public static (int Width, int Height) PhotoPixels(IdSize size, double scale = 1) =>
        ((int)Math.Round(size.WidthPx * scale), (int)Math.Round(size.HeightPx * scale));

    /// <summary>
    /// Places the photos (rendered at <see cref="PhotoPixels"/>) on the page. With a scale correction, positions are
    /// scaled about the page centre too, so the whole layout keeps its proportions after printing.
    /// </summary>
    public static MagickImage Render(MagickImage photo, SheetLayout layout, double scale = 1)
    {
        var k = IdSize.Ppi / 25.4;
        var page = layout.Landscape
            ? new MagickImage(MagickColors.White, PageLongPx, PageShortPx)
            : new MagickImage(MagickColors.White, PageShortPx, PageLongPx);
        try
        {
            double cx = page.Width / 2.0, cy = page.Height / 2.0;
            foreach (var r in layout.Photos)
            {
                var x = cx + (r.X * k - cx) * scale;
                var y = cy + (r.Y * k - cy) * scale;
                page.Composite(photo, (int)Math.Round(x), (int)Math.Round(y), CompositeOperator.Over);
            }

            page.Density = new Density(IdSize.Ppi, IdSize.Ppi, DensityUnit.PixelsPerInch);
            return page;
        }
        catch
        {
            page.Dispose();
            throw;
        }
    }
}
