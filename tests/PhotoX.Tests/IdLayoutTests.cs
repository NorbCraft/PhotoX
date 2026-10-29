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
    [InlineData("3x4", 9)]   // 3 × 3 with the 7 mm strip
    [InlineData("35x45", 6)] // 2 × 3
    [InlineData("2x2in", 2)]
    [InlineData("4x6cm", 4)]
    [InlineData("5x7cm", 2)]
    [InlineData("9x12", 1)]
    public void Capacities_with_default_margins(string id, int capacity) =>
        Assert.Equal(capacity, SheetLayout.CapacityFor(IdSizes.BuiltIn.Single(s => s.Id == id)));

    [Fact]
    public void Three_by_four_keeps_7mm_strip_below_every_photo_and_cuts_below_it()
    {
        var size = IdSizes.S3x4;
        var layout = SheetLayout.Compute(size, new SheetOptions(8));
        Assert.Equal(8, layout.Photos.Count);
        foreach (var (photo, cell) in layout.Photos.Zip(layout.Cells))
        {
            Assert.Equal(30, photo.Width, 9);
            Assert.Equal(40, photo.Height, 9);
            Assert.Equal(47, cell.Height, 9);
            Assert.Equal(photo.Y, cell.Y, 9);
            // No cut line runs along the photo's lower edge (the strip is kept), but one runs 7 mm below it.
            Assert.DoesNotContain(layout.CutY, y => Math.Abs(y - photo.Bottom) < 1e-6);
            Assert.Contains(layout.CutY, y => Math.Abs(y - (photo.Bottom + 7)) < 1e-6);
        }

        // Nothing overlaps and everything stays on the page with at least the margin.
        foreach (var c in layout.Cells)
        {
            Assert.True(c.X >= 3 - 1e-6 && c.Right <= SheetOptions.PageWidthMm - 3 + 1e-6);
            Assert.True(c.Y >= 3 - 1e-6 && c.Bottom <= SheetOptions.PageHeightMm - 3 + 1e-6);
        }
    }

    [Fact]
    public void Fewer_copies_are_centred_and_never_shrink_photos()
    {
        var layout = SheetLayout.Compute(IdSizes.S35x45, new SheetOptions(2));
        Assert.Equal(2, layout.Photos.Count);
        Assert.All(layout.Photos, p => Assert.Equal((35.0, 45.0), (p.Width, p.Height)));
        var left = layout.Photos.Min(p => p.X);
        var right = SheetOptions.PageWidthMm - layout.Photos.Max(p => p.Right);
        Assert.Equal(left, right, 6);
        Assert.Equal(6, SheetLayout.Compute(IdSizes.S35x45, new SheetOptions(99)).Photos.Count); // capped at capacity
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
