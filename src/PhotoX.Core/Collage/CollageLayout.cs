using PhotoX.Core.Geometry;

namespace PhotoX.Core.Collage;

/// <summary>
/// Collage geometry (prompt_04 §5). Slots fill the sheet inside an outer margin, with the same gap between them,
/// so the photos use as much of the paper as practical. All values are millimetres.
/// </summary>
public static class CollageLayout
{
    /// <summary>Slot rectangles on a sheet of <paramref name="pageWidthMm"/> × <paramref name="pageHeightMm"/>, in template order.</summary>
    public static IReadOnlyList<RectD> Slots(CollageTemplate template, double pageWidthMm, double pageHeightMm, double spacingMm)
    {
        var t = pageWidthMm > pageHeightMm ? template.Transposed() : template;
        var unitW = (pageWidthMm - 2 * spacingMm - (t.Columns - 1) * spacingMm) / t.Columns;
        var unitH = (pageHeightMm - 2 * spacingMm - (t.Rows - 1) * spacingMm) / t.Rows;
        return t.Cells.Select(c => new RectD(
            spacingMm + c.Column * (unitW + spacingMm),
            spacingMm + c.Row * (unitH + spacingMm),
            c.ColumnSpan * unitW + (c.ColumnSpan - 1) * spacingMm,
            c.RowSpan * unitH + (c.RowSpan - 1) * spacingMm)).ToList();
    }

    /// <summary>
    /// The part of a photo shown in a slot, relative to the photo (0..1): the largest centred crop of the slot's ratio,
    /// reduced by <paramref name="zoom"/> (≥ 1) and centred on (<paramref name="centerX"/>, <paramref name="centerY"/>),
    /// kept inside the photo. The photo is never stretched.
    /// </summary>
    public static NormalizedRect Crop(double photoWidth, double photoHeight, double slotRatio, double zoom, double centerX, double centerY)
    {
        var full = CropMath.LargestCentered(RectD.FromSize(photoWidth, photoHeight), slotRatio);
        var z = Math.Max(1, zoom);
        var w = full.Width / z;
        var h = full.Height / z;
        var x = Math.Clamp(centerX * photoWidth - w / 2, 0, photoWidth - w);
        var y = Math.Clamp(centerY * photoHeight - h / 2, 0, photoHeight - h);
        return NormalizedRect.FromPixels(new RectD(x, y, w, h), photoWidth, photoHeight);
    }

    /// <summary>Slot pixel rectangle at <paramref name="ppi"/>: edges are rounded, so neighbouring slots keep exact gaps.</summary>
    public static PixelRect ToPixels(RectD slotMm, double ppi)
    {
        int Px(double mm) => (int)Math.Round(mm * ppi / 25.4, MidpointRounding.AwayFromZero);
        var x0 = Px(slotMm.X);
        var y0 = Px(slotMm.Y);
        return new PixelRect(x0, y0, Px(slotMm.Right) - x0, Px(slotMm.Bottom) - y0);
    }
}
