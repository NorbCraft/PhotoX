using ImageMagick;
using PhotoX.Core.Id;

namespace PhotoX.Imaging.Id;

/// <summary>Composes the 4×6 in sheet at 300 ppi (FR-066–FR-068): white page (portrait or landscape) with identical photos.</summary>
public static class SheetRenderer
{
    public const int PageShortPx = 1200; // 101.6 mm at 300 ppi
    public const int PageLongPx = 1800;  // 152.4 mm at 300 ppi

    public static MagickImage Render(MagickImage photo, SheetLayout layout)
    {
        var k = IdSize.Ppi / 25.4;
        var page = layout.Landscape
            ? new MagickImage(MagickColors.White, PageLongPx, PageShortPx)
            : new MagickImage(MagickColors.White, PageShortPx, PageLongPx);
        try
        {
            foreach (var r in layout.Photos)
            {
                page.Composite(photo, (int)Math.Round(r.X * k), (int)Math.Round(r.Y * k), CompositeOperator.Over);
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
