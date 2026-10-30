namespace PhotoX.Core.Collage;

/// <summary>A cell of a collage template on its grid, with optional spans.</summary>
public readonly record struct CollageCell(int Column, int Row, int ColumnSpan = 1, int RowSpan = 1);

/// <summary>
/// A collage template (prompt_04 §5): cells on a grid of <paramref name="Columns"/> × <paramref name="Rows"/> equal
/// units, defined for a portrait sheet. A landscape sheet uses the transposed grid, so every template works in both
/// orientations and mixes portrait and landscape slots where its cells differ in shape.
/// </summary>
public sealed record CollageTemplate(string Id, int Columns, int Rows, IReadOnlyList<CollageCell> Cells)
{
    public int Count => Cells.Count;

    /// <summary>The same template on a landscape sheet: columns and rows swapped.</summary>
    public CollageTemplate Transposed() =>
        this with { Columns = Rows, Rows = Columns, Cells = Cells.Select(c => new CollageCell(c.Row, c.Column, c.RowSpan, c.ColumnSpan)).ToList() };
}

/// <summary>The built-in templates, from two photos to nine. All of them are offered for every sheet size.</summary>
public static class CollageTemplates
{
    public static IReadOnlyList<CollageTemplate> All { get; } =
    [
        new("2-stacked", 1, 2, [new(0, 0), new(0, 1)]),
        new("2-side", 2, 1, [new(0, 0), new(1, 0)]),
        new("3-stacked", 1, 3, [new(0, 0), new(0, 1), new(0, 2)]),
        new("1-big-2-below", 2, 3, [new(0, 0, 2, 2), new(0, 2), new(1, 2)]),
        new("2-above-1-big", 2, 3, [new(0, 0), new(1, 0), new(0, 1, 2, 2)]),
        new("1-big-2-side", 3, 2, [new(0, 0, 2, 2), new(2, 0), new(2, 1)]),
        new("4-grid", 2, 2, [new(0, 0), new(1, 0), new(0, 1), new(1, 1)]),
        new("1-big-3-below", 3, 3, [new(0, 0, 3, 2), new(0, 2), new(1, 2), new(2, 2)]),
        new("1-big-3-side", 3, 3, [new(0, 0, 2, 3), new(2, 0), new(2, 1), new(2, 2)]),
        new("6-grid", 2, 3, [new(0, 0), new(1, 0), new(0, 1), new(1, 1), new(0, 2), new(1, 2)]),
        new("9-grid", 3, 3, [new(0, 0), new(1, 0), new(2, 0), new(0, 1), new(1, 1), new(2, 1), new(0, 2), new(1, 2), new(2, 2)]),
    ];

    public static CollageTemplate Default => All[6];

    public static CollageTemplate? Find(string id) => All.FirstOrDefault(t => t.Id == id);
}
