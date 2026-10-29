using ImageMagick;
using ImageMagick.Drawing;
using PhotoX.Core.Id;

namespace PhotoX.Imaging.Id;

/// <summary>Composes the 4×6 in sheet at 300 ppi (FR-066–FR-068): white page, optional cut guides, identical photos.</summary>
public static class SheetRenderer
{
    public const int PageWidthPx = 1200;  // 101.6 mm at 300 ppi
    public const int PageHeightPx = 1800; // 152.4 mm at 300 ppi

    public static MagickImage Render(MagickImage photo, SheetLayout layout, bool cutGuides)
    {
        var k = IdSize.Ppi / 25.4;
        var page = new MagickImage(MagickColors.White, PageWidthPx, PageHeightPx);
        try
        {
            if (cutGuides)
            {
                var d = new Drawables().StrokeColor(new MagickColor("#9A9A9A")).StrokeWidth(1).FillColor(MagickColors.Transparent);
                foreach (var x in layout.CutX) d.Line(Math.Round(x * k), 0, Math.Round(x * k), PageHeightPx);
                foreach (var y in layout.CutY) d.Line(0, Math.Round(y * k), PageWidthPx, Math.Round(y * k));
                d.Draw(page);
            }

            // Photos go on top of the guides, so guides show only in margins, gaps and the white strip.
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
