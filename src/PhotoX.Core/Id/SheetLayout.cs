using PhotoX.Core.Geometry;

namespace PhotoX.Core.Id;

/// <summary>Sheet options (FR-066, FR-067). Millimetres.</summary>
public sealed record SheetOptions(int Copies, double MarginMm = 3, double SpacingMm = 2, bool CutGuides = true)
{
    public const double PageWidthMm = 101.6;
    public const double PageHeightMm = 152.4;
}

/// <summary>
/// Simple uniform grid on a portrait 4×6 in page (FR-066): photos are never shrunk to fit, familiar lower counts are
/// allowed, and 3×4 cells keep a 7 mm white strip below each photo (FR-074).
/// </summary>
public sealed record SheetLayout(
    int Columns,
    int Rows,
    int Capacity,
    IReadOnlyList<RectD> Photos,
    IReadOnlyList<RectD> Cells,
    IReadOnlyList<double> CutX,
    IReadOnlyList<double> CutY)
{
    public static int CapacityFor(IdSize size, double marginMm = 3, double spacingMm = 2)
    {
        var (c, r) = Grid(size, marginMm, spacingMm);
        return c * r;
    }

    public static SheetLayout Compute(IdSize size, SheetOptions options)
    {
        var (cols, rows) = Grid(size, options.MarginMm, options.SpacingMm);
        var capacity = cols * rows;
        var copies = Math.Clamp(options.Copies, 0, capacity);

        // Use only as many rows as the copies need, and centre that block on the page.
        var usedRows = copies == 0 ? 0 : (int)Math.Ceiling(copies / (double)cols);
        var usedCols = Math.Min(cols, Math.Max(copies, 1));
        var gridW = usedCols * size.WidthMm + (usedCols - 1) * options.SpacingMm;
        var gridH = usedRows * size.CellHeightMm + Math.Max(0, usedRows - 1) * options.SpacingMm;
        var x0 = (SheetOptions.PageWidthMm - gridW) / 2;
        var y0 = (SheetOptions.PageHeightMm - gridH) / 2;

        var photos = new List<RectD>();
        var cells = new List<RectD>();
        for (var i = 0; i < copies; i++)
        {
            var col = i % usedCols;
            var row = i / usedCols;
            var x = x0 + col * (size.WidthMm + options.SpacingMm);
            var y = y0 + row * (size.CellHeightMm + options.SpacingMm);
            photos.Add(new RectD(x, y, size.WidthMm, size.HeightMm));
            cells.Add(new RectD(x, y, size.WidthMm, size.CellHeightMm));
        }

        // Cut lines follow cell edges; for 3×4 the bottom cut is below the strip, never at the photo's lower edge.
        var cutX = cells.SelectMany(c => new[] { c.X, c.Right }).Distinct().OrderBy(v => v).ToList();
        var cutY = cells.SelectMany(c => new[] { c.Y, c.Bottom }).Distinct().OrderBy(v => v).ToList();
        return new SheetLayout(cols, rows, capacity, photos, cells, cutX, cutY);
    }

    private static (int Cols, int Rows) Grid(IdSize size, double margin, double spacing)
    {
        var availW = SheetOptions.PageWidthMm - 2 * margin;
        var availH = SheetOptions.PageHeightMm - 2 * margin;
        var cols = availW < size.WidthMm ? 0 : (int)Math.Floor((availW + spacing) / (size.WidthMm + spacing) + 1e-9);
        var rows = availH < size.CellHeightMm ? 0 : (int)Math.Floor((availH + spacing) / (size.CellHeightMm + spacing) + 1e-9);
        return (cols, rows);
    }
}
