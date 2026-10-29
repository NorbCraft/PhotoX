using PhotoX.Core.Geometry;
using PhotoX.Core.Id;

namespace PhotoX.Tests;

/// <summary>TS-14 / TS-15: ID sizes, sheet grids, the 7 mm strip and file names.</summary>
public class IdLayoutTests
{
    [Theory]
    [InlineData("3x4", 354, 472)]
    [InlineData("35x45", 413, 531)]
    [InlineData("2x2in", 600, 600)]
    [InlineData("4x6cm", 472, 709)]
    [InlineData("5x7cm", 591, 827)]
    [InlineData("9x12", 1063, 1417)]
    public void Sizes_convert_to_exact_pixels_at_300_ppi(string id, int w, int h)
    {
        var size = IdSizes.BuiltIn.Single(s => s.Id == id);
        Assert.Equal((w, h), (size.WidthPx, size.HeightPx));
    }

    [Theory]
    [InlineData("3x4", true, 8)]   // landscape 4 × 2; each 7 mm strip is also the gap to the next row (prompt_03 §7)
    [InlineData("35x45", true, 8)] // landscape 4 × 2 fits more than portrait 2 × 3
    [InlineData("2x2in", false, 2)]
    [InlineData("4x6cm", false, 4)]
    [InlineData("5x7cm", false, 2)]
    [InlineData("9x12", false, 1)]
    public void Orientation_and_capacity_with_default_margins(string id, bool landscape, int capacity)
    {
        var layout = SheetLayout.Compute(IdSizes.BuiltIn.Single(s => s.Id == id), new SheetOptions(99));
        Assert.Equal((landscape, capacity), (layout.Landscape, layout.Capacity));
        Assert.Equal(capacity, layout.Photos.Count);
    }

    [Fact]
    public void Three_by_four_keeps_7mm_strip_below_every_photo_on_a_landscape_page()
    {
        var size = IdSizes.S3x4;
        var layout = SheetLayout.Compute(size, new SheetOptions(8));
        Assert.True(layout.Landscape);
        Assert.Equal((152.4, 101.6), (layout.PageWidthMm, layout.PageHeightMm));
        Assert.Equal(8, layout.Photos.Count);
        foreach (var (photo, cell) in layout.Photos.Zip(layout.Cells))
        {
            Assert.Equal((30.0, 40.0), (photo.Width, photo.Height));  // upright, exact size
            Assert.Equal(47, cell.Height, 9);
            Assert.Equal(photo.Y, cell.Y, 9);
            // The strip below each photo is free of other photos, so every photo can be cut as 30 × 47 mm.
            var strip = new RectD(photo.X, photo.Bottom, photo.Width, 7);
            Assert.DoesNotContain(layout.Photos, other => other != photo && other.Y < strip.Bottom - 1e-6 && other.Bottom > strip.Y + 1e-6
                && other.X < strip.Right && other.Right > strip.X);
        }

        foreach (var c in layout.Cells)
        {
            Assert.True(c.X >= 3 - 1e-6 && c.Right <= layout.PageWidthMm - 3 + 1e-6);
            Assert.True(c.Y >= 3 - 1e-6 && c.Bottom <= layout.PageHeightMm - 3 + 1e-6);
        }
    }

    [Fact]
    public void Three_by_four_without_strip_uses_the_spacing_between_rows()
    {
        var layout = SheetLayout.Compute(IdSizes.S3x4, new SheetOptions(8, Strip: false));
        Assert.True(layout.Landscape);
        Assert.Equal(8, layout.Capacity);
        var rows = layout.Photos.Select(p => p.Y).Distinct().Order().ToList();
        Assert.Equal(42, rows[1] - rows[0], 6); // 40 mm photo + 2 mm spacing
    }

    [Fact]
    public void Fewer_copies_are_centred_and_never_shrink_photos()
    {
        var layout = SheetLayout.Compute(IdSizes.S35x45, new SheetOptions(2));
        Assert.Equal(2, layout.Photos.Count);
        Assert.All(layout.Photos, p => Assert.Equal((35.0, 45.0), (p.Width, p.Height)));
        var left = layout.Photos.Min(p => p.X);
        var right = layout.PageWidthMm - layout.Photos.Max(p => p.Right);
        Assert.Equal(left, right, 6);
        Assert.Equal(8, SheetLayout.Compute(IdSizes.S35x45, new SheetOptions(99)).Photos.Count); // capped at capacity
    }

    [Fact]
    public void File_names_are_sortable_and_suffixed()
    {
        var t = new DateTime(2026, 10, 29, 18, 45, 12);
        Assert.Equal(["ID_20261029_1845.jpg", "ID_20261029_1845_1.jpg", "ID_20261029_1845_2.jpg"], IdFileNames.Candidates(t, ".jpg").Take(3));
    }

    [Fact]
    public void Face_framing_places_eyes_on_the_eye_line_and_levels_roll()
    {
        var size = IdSizes.S35x45;
        var f = IdFraming.FromFace(400, 500, 520, 500, 600, size);
        Assert.Equal(0, f.RotationDeg, 6);
        Assert.Equal(600 / 0.75 * 35 / 45, f.FrameWidthSource, 6);
        var frameH = f.FrameWidthSource / size.Ratio;
        Assert.Equal(500, f.CenterY - frameH * (0.5 - size.EyeFraction), 6); // eyes at the eye line
        var tilted = IdFraming.FromFace(400, 500, 520, 520, 600, size);
        Assert.True(tilted.RotationDeg > 9 && tilted.RotationDeg < 10);
    }
}
