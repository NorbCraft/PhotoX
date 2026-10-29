using PhotoX.Core.Geometry;

namespace PhotoX.Core.Id;

/// <summary>
/// Sheet options (FR-066, FR-067). Millimetres. <paramref name="Strip"/> keeps the 7 mm white strip below each
/// photo for sizes that have one (3×4, FR-074); other sizes ignore it.
/// </summary>
public sealed record SheetOptions(int Copies, double MarginMm = 3, double SpacingMm = 2, bool Strip = true)
{
    /// <summary>Short edge of the 4×6 in (10×15) page.</summary>
    public const double PageShortMm = 101.6;

    /// <summary>Long edge of the 4×6 in (10×15) page.</summary>
    public const double PageLongMm = 152.4;
}

/// <summary>
/// Simple uniform grid on a 4×6 in page (FR-066, prompt_03 §7). Photos are never shrunk or rotated; familiar lower
/// counts are allowed. 3×4 photos go on a landscape page; other sizes use landscape only when more copies fit.
/// With the 3×4 strip, each photo's 7 mm white strip is also the gap to the row below, so it can be cut as 30×47 mm.
/// </summary>
public sealed record SheetLayout(
    bool Landscape,
    int Columns,
    int Rows,
    int Capacity,
    IReadOnlyList<RectD> Photos,
    IReadOnlyList<RectD> Cells)
{
    public double PageWidthMm => Landscape ? SheetOptions.PageLongMm : SheetOptions.PageShortMm;

    public double PageHeightMm => Landscape ? SheetOptions.PageShortMm : SheetOptions.PageLongMm;

    public static int CapacityFor(IdSize size, double marginMm = 3, double spacingMm = 2, bool strip = true) =>
        Compute(size, new SheetOptions(0, marginMm, spacingMm, strip)).Capacity;

    public static SheetLayout Compute(IdSize size, SheetOptions options)
    {
        var strip = options.Strip ? size.StripBelowMm : 0;
        var portrait = Grid(size, strip, options, SheetOptions.PageShortMm, SheetOptions.PageLongMm);
        var landscape = Grid(size, strip, options, SheetOptions.PageLongMm, SheetOptions.PageShortMm);
        var useLandscape = size.Id == IdSizes.S3x4.Id || landscape.Cols * landscape.Rows > portrait.Cols * portrait.Rows;
        var (cols, rows) = useLandscape ? landscape : portrait;
        var pageW = useLandscape ? SheetOptions.PageLongMm : SheetOptions.PageShortMm;
        var pageH = useLandscape ? SheetOptions.PageShortMm : SheetOptions.PageLongMm;
        var capacity = cols * rows;
        var copies = Math.Clamp(options.Copies, 0, capacity);
        var cellH = size.HeightMm + strip;
        var pitchY = cellH + (strip > 0 ? 0 : options.SpacingMm);

        // Use only as many rows as the copies need, and centre that block on the page.
        var usedRows = copies == 0 ? 0 : (int)Math.Ceiling(copies / (double)cols);
        var usedCols = Math.Min(cols, Math.Max(copies, 1));
        var gridW = usedCols * size.WidthMm + (usedCols - 1) * options.SpacingMm;
        var gridH = usedRows * cellH + Math.Max(0, usedRows - 1) * (pitchY - cellH);
        var x0 = (pageW - gridW) / 2;
        var y0 = (pageH - gridH) / 2;

        var photos = new List<RectD>();
        var cells = new List<RectD>();
        for (var i = 0; i < copies; i++)
        {
            var x = x0 + i % usedCols * (size.WidthMm + options.SpacingMm);
            var y = y0 + i / usedCols * pitchY;
            photos.Add(new RectD(x, y, size.WidthMm, size.HeightMm));
            cells.Add(new RectD(x, y, size.WidthMm, cellH));
        }

        return new SheetLayout(useLandscape, cols, rows, capacity, photos, cells);
    }

    private static (int Cols, int Rows) Grid(IdSize size, double strip, SheetOptions o, double pageW, double pageH)
    {
        var availW = pageW - 2 * o.MarginMm;
        var availH = pageH - 2 * o.MarginMm;
        var cellH = size.HeightMm + strip;
        var gapY = strip > 0 ? 0 : o.SpacingMm;
        var cols = availW < size.WidthMm ? 0 : (int)Math.Floor((availW + o.SpacingMm) / (size.WidthMm + o.SpacingMm) + 1e-9);
        var rows = availH < cellH ? 0 : (int)Math.Floor((availH + gapY) / (cellH + gapY) + 1e-9);
        return (cols, rows);
    }
}
