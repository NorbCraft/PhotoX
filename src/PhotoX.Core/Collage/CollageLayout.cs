using PhotoX.Core.Geometry;

namespace PhotoX.Core.Collage;

/// <summary>
/// Collage geometry (prompt_04 §5). Slots fill the sheet inside an outer margin, with the same gap between them,
/// so the photos use as much of the paper as practical. All values are millimetres.
/// </summary>
public static class CollageLayout
{
    /// <summary>Smallest column width or row height when the layout is adjusted (prompt_05 §8).</summary>
    public const double MinCellMm = 10;

    /// <summary>The template's grid on a sheet of this orientation: transposed for landscape.</summary>
    public static CollageTemplate GridFor(CollageTemplate template, double pageWidthMm, double pageHeightMm) =>
        pageWidthMm > pageHeightMm ? template.Transposed() : template;

    /// <summary>
    /// Slot rectangles on a sheet of <paramref name="pageWidthMm"/> × <paramref name="pageHeightMm"/>, in template order.
    /// Column and row weights (of the grid for this orientation) share out the width and height; equal by default.
    /// </summary>
    public static IReadOnlyList<RectD> Slots(CollageTemplate template, double pageWidthMm, double pageHeightMm, double spacingMm,
        IReadOnlyList<double>? columnWeights = null, IReadOnlyList<double>? rowWeights = null)
    {
        var t = GridFor(template, pageWidthMm, pageHeightMm);
        var xs = Edges(columnWeights, t.Columns, pageWidthMm, spacingMm);
        var ys = Edges(rowWeights, t.Rows, pageHeightMm, spacingMm);
        return t.Cells.Select(c => new RectD(
            xs[c.Column].Start,
            ys[c.Row].Start,
            xs[c.Column + c.ColumnSpan - 1].End - xs[c.Column].Start,
            ys[c.Row + c.RowSpan - 1].End - ys[c.Row].Start)).ToList();
    }

    /// <summary>Width (or height) left for photos once the margins and gaps of <paramref name="count"/> cells are taken.</summary>
    public static double Available(double pageMm, int count, double spacingMm) => pageMm - 2 * spacingMm - (count - 1) * spacingMm;

    /// <summary>
    /// New weights that give cells <paramref name="start"/> … <paramref name="start"/>+<paramref name="span"/>−1 together
    /// <paramref name="sizeMm"/> (gaps between them included); the other cells share the rest in their old proportions.
    /// Every cell keeps at least <see cref="MinCellMm"/>, so the photos always stay within the sheet.
    /// </summary>
    public static double[] Resize(IReadOnlyList<double> weights, int start, int span, double sizeMm, double availableMm, double spacingMm)
    {
        var n = weights.Count;
        if (span >= n) return [.. weights];
        var target = Math.Clamp(sizeMm - (span - 1) * spacingMm, span * MinCellMm, availableMm - (n - span) * MinCellMm);
        var total = weights.Sum();
        var inside = Enumerable.Range(start, span).Sum(i => weights[i]);
        var outside = total - inside;
        var result = new double[n];
        for (var i = 0; i < n; i++)
        {
            var mine = i >= start && i < start + span;
            var share = mine ? weights[i] / inside * target : weights[i] / outside * (availableMm - target);
            result[i] = share / availableMm;
        }

        return result;
    }

    /// <summary>Moves the boundary after cell <paramref name="index"/> by <paramref name="deltaMm"/>, keeping both neighbours at least <see cref="MinCellMm"/>.</summary>
    public static double[] MoveBoundary(IReadOnlyList<double> weights, int index, double deltaMm, double availableMm)
    {
        var total = weights.Sum();
        var sizes = weights.Select(w => w / total * availableMm).ToArray();
        var pair = sizes[index] + sizes[index + 1];
        sizes[index] = Math.Clamp(sizes[index] + deltaMm, MinCellMm, pair - MinCellMm);
        sizes[index + 1] = pair - sizes[index];
        return sizes.Select(s => s / availableMm).ToArray();
    }

    private static (double Start, double End)[] Edges(IReadOnlyList<double>? weights, int count, double pageMm, double spacingMm)
    {
        var w = weights is { } given && given.Count == count && given.All(v => v > 0) ? given : Enumerable.Repeat(1.0, count).ToList();
        var total = w.Sum();
        var available = Available(pageMm, count, spacingMm);
        var edges = new (double, double)[count];
        var x = spacingMm;
        for (var i = 0; i < count; i++)
        {
            var size = available * w[i] / total;
            edges[i] = (x, x + size);
            x += size + spacingMm;
        }

        return edges;
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
