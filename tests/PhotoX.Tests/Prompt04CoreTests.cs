using ImageMagick;
using PhotoX.Core.Collage;
using PhotoX.Core.Editing;
using PhotoX.Core.Formats;
using PhotoX.Core.Geometry;
using PhotoX.Core.Id;
using PhotoX.Imaging;
using PhotoX.Imaging.Collage;
using PhotoX.Storage;

namespace PhotoX.Tests;

/// <summary>prompt_04: custom print sizes, ID size changes, collages, the levels histogram and Duplicate image.</summary>
public class Prompt04CoreTests
{
    [Theory]
    [InlineData(90, 130, "10x15")]
    [InlineData(130, 90, "10x15")]   // entered landscape: ordered to portrait
    [InlineData(140, 170, "15x20")]
    [InlineData(180, 240, "20x30")]
    [InlineData(101.6, 152.4, "10x15")]
    [InlineData(250, 350, null)]     // larger than every sheet: no canvas
    public void Custom_size_goes_on_the_smallest_sheet_that_fits(double w, double h, string? sheet)
    {
        var f = PrintFormats.Custom(w, h);
        Assert.Equal((Math.Min(w, h), Math.Max(w, h)), (f.WidthMm, f.HeightMm));
        var expected = sheet is null ? null : PrintFormats.FindSheet(sheet);
        Assert.Equal(expected?.WidthMm, f.CanvasWidthMm);
        Assert.Equal(expected?.HeightMm, f.CanvasHeightMm);
    }

    [Fact]
    public void Custom_9x13_keeps_its_physical_size_on_the_10x15_canvas()
    {
        var recipe = new EditRecipe { FormatId = PrintFormats.CustomId, CustomWidthMm = 90, CustomHeightMm = 130, Orientation = CropOrientation.Portrait };
        var crop = CropMath.LargestCentered(RectD.FromSize(3000, 4000), recipe.CropRatio);
        var plan = RenderPlan.Create(3000, 4000, recipe with { Crop = NormalizedRect.FromPixels(crop, 3000, 4000) });
        Assert.Equal(101.6 / 152.4, plan.CanvasWidth / (double)plan.CanvasHeight, 3);  // the sheet's ratio
        Assert.Equal(90 / 101.6, plan.Crop.Width / (double)plan.CanvasWidth, 3);        // 9 cm of the sheet's width
        Assert.Equal(130 / 152.4, plan.Crop.Height / (double)plan.CanvasHeight, 3);
        Assert.Equal((plan.CanvasWidth - plan.Crop.Width) / 2, plan.PhotoX);           // centred
        Assert.InRange(plan.EffectivePpi!.Value - plan.Crop.Height / (130 / 25.4), -1, 1); // photo pixels are not resampled
    }

    [Fact]
    public void Legacy_custom_ratio_without_size_has_no_canvas()
    {
        var recipe = new EditRecipe { FormatId = PrintFormats.CustomId, CustomRatio = 0.7 };
        Assert.Null(recipe.Format);
        Assert.Equal(0.7, recipe.PortraitRatio);
    }

    [Fact]
    public void Romanian_3x4_head_is_smaller_with_the_crown_line_unchanged()
    {
        Assert.Equal(0.10, IdSizes.S3x4.CrownFraction);
        Assert.Equal(0.70, IdSizes.S3x4.HeadFraction);
        Assert.False(IdSizes.S9x12.Guides);
        Assert.True(IdSizes.S35x45.Guides);
    }

    /// <summary>prompt_04 §8: 2 × 50.8 mm is exactly the 101.6 mm short edge, so four 2×2 in photos fit only edge to edge.</summary>
    [Fact]
    public void Four_2x2_inch_photos_fit_edge_to_edge_on_the_4x6_page()
    {
        var size = IdSizes.S2x2in;
        Assert.Equal(2, SheetLayout.CapacityFor(size));                                    // with the usual 3 mm margins
        var layout = SheetLayout.Compute(size, new SheetOptions(4, size.SheetMarginMm!.Value, size.SheetSpacingMm!.Value));
        Assert.False(layout.Landscape);
        Assert.Equal(6, layout.Capacity);
        Assert.Equal(4, layout.Photos.Count);
        Assert.All(layout.Photos, p => Assert.Equal((50.8, 50.8), (p.Width, p.Height)));  // never resized
        Assert.Equal([0.0, 50.8, 0.0, 50.8], layout.Photos.Select(p => Math.Round(p.X, 6)));
        Assert.Equal([25.4, 25.4, 76.2, 76.2], layout.Photos.Select(p => Math.Round(p.Y, 6)));
        Assert.Equal(new[] { 0, 600, 0, 600 }, layout.Photos.Select(p => Units.MmToPx(p.X, 300)));
    }

    [Fact]
    public void Unedited_earlier_defaults_are_upgraded_and_edited_entries_kept()
    {
        var edited = IdSizes.Superseded[0] with { HeadFraction = 0.72 };
        var (sizes, changed) = IdSizes.UpgradeDefaults([edited, IdSizes.Superseded[1], IdSizes.Superseded[2], IdSizes.S4x6cm]);
        Assert.True(changed);
        Assert.Equal(0.72, sizes[0].HeadFraction);                    // the owner's edit stays
        Assert.True(IdSizes.SameValues(IdSizes.S2x2in, sizes[1]));
        Assert.True(IdSizes.SameValues(IdSizes.S9x12, sizes[2]));
        Assert.Same(IdSizes.S4x6cm, sizes[3]);
        Assert.False(IdSizes.UpgradeDefaults(IdSizes.Defaults).Changed);
    }

    [Fact]
    public void Custom_id_sizes_are_labelled_in_centimetres()
    {
        var c = IdSizes.Custom(25, 30);
        Assert.Equal("2.5 × 3 cm", c.Label);
        Assert.True(c.IsCustom);
        Assert.True(IdSizes.IsValidCustom(25, 30));
        Assert.True(IdSizes.IsValidCustom(140, 90));   // fits the page in landscape
        Assert.False(IdSizes.IsValidCustom(5, 30));
        Assert.False(IdSizes.IsValidCustom(110, 160));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Collage_slots_fill_the_sheet_with_equal_gaps(bool landscape)
    {
        var (w, h) = landscape ? (152.4, 101.6) : (101.6, 152.4);
        foreach (var t in CollageTemplates.All)
        {
            var slots = CollageLayout.Slots(t, w, h, 3);
            Assert.Equal(t.Count, slots.Count);
            Assert.All(slots, s => Assert.True(s.X >= 3 - 1e-9 && s.Y >= 3 - 1e-9 && s.Right <= w - 3 + 1e-9 && s.Bottom <= h - 3 + 1e-9));
            for (var i = 0; i < slots.Count; i++)
            {
                for (var j = i + 1; j < slots.Count; j++)
                {
                    var (a, b) = (slots[i], slots[j]);
                    var gapX = Math.Max(b.X - a.Right, a.X - b.Right);
                    var gapY = Math.Max(b.Y - a.Bottom, a.Y - b.Bottom);
                    Assert.True(Math.Max(gapX, gapY) >= 3 - 1e-9, $"{t.Id}: slots {i} and {j} overlap");
                }
            }

            // The photos cover most of the sheet: everything inside the margins except the gaps.
            var area = slots.Sum(s => s.Width * s.Height);
            var inner = (w - 6) * (h - 6);
            Assert.True(area < inner && area > 0.85 * inner, $"{t.Id}: {area / inner:P0}");
        }

        // A landscape sheet transposes the template: the big photo moves from the top to the left.
        var mixed = CollageLayout.Slots(CollageTemplates.Find("1-big-2-below")!, w, h, 3);
        Assert.All(mixed.Skip(1), s => Assert.True(landscape ? s.X > mixed[0].Right : s.Y > mixed[0].Bottom));
    }

    [Fact]
    public void Collage_crop_has_the_slot_ratio_stays_inside_and_zooms()
    {
        var full = CollageLayout.Crop(4000, 3000, 0.75, 1, 0.5, 0.5);
        Assert.Equal(0.75, full.Width * 4000 / (full.Height * 3000), 6);
        Assert.Equal(1, full.Height, 6);
        var zoomed = CollageLayout.Crop(4000, 3000, 0.75, 2, 0.95, 0.5);
        Assert.Equal(full.Width / 2, zoomed.Width, 6);
        Assert.Equal(1, zoomed.Right(), 6);            // clamped at the right edge
    }

    [Fact]
    public void Collage_renders_at_300_ppi_with_white_gaps()
    {
        using var dir = new TempDir();
        var a = Fixtures.WriteJpeg(dir.Path, "a.jpg", 900, 600);
        var b = Fixtures.WritePng(dir.Path, "b.png", 600, 900, alpha: true);
        var t = CollageTemplates.Find("2-stacked")!;
        var slots = CollageLayout.Slots(t, 152.4, 203.2, 3);
        var photos = new[] { a, b }.Select((p, i) => (CollagePhoto?)new CollagePhoto(p, CollageLayout.Crop(i == 0 ? 900 : 600, i == 0 ? 600 : 900, slots[i].Width / slots[i].Height, 1, 0.5, 0.5))).ToList();
        using var page = CollageRenderer.Render(152.4, 203.2, slots, photos);
        Assert.Equal((1800u, 2400u), (page.Width, page.Height));
        using var pixels = page.GetPixels();
        var gap = CollageLayout.ToPixels(slots[0], 300).Bottom + 10;   // between the two photos
        Assert.True(pixels.GetPixel(900, gap).ToColor()!.R > 65000);
        var s0 = CollageLayout.ToPixels(slots[0], 300);
        Assert.True(pixels.GetPixel(s0.X + 5, s0.Y + 5).ToColor()!.R < 65000 || pixels.GetPixel(s0.X + 5, s0.Y + 5).ToColor()!.G < 65000);
    }

    /// <summary>prompt_04 §7: the histogram under the levels handles shows the tones entering levels.</summary>
    [Fact]
    public void Levels_histogram_ignores_levels_and_follows_exposure()
    {
        using var img = Fixtures.Gradient(256, 64);
        var data = PixelData.From(img);
        var plain = ImageStatistics.Histogram(data);
        var leveled = ImageStatistics.LevelsInput(data, null, new EditRecipe { LevelsBlack = 0.2, LevelsWhite = 0.8, LevelsMid = 1.5, Contrast = 40, Saturation = 30 });
        Assert.Equal(plain.Luma, leveled.Luma);                      // handles do not reshape what they sit on

        var exposure = new EditRecipe { Exposure = 1 };
        var target = new byte[data.Bgra.Length];
        ToneProcessor.ApplyToPreview(data.Bgra, target, ToneCurve.For(exposure, exact: false), 0);
        var applied = ImageStatistics.Histogram(data with { Bgra = target });
        var input = ImageStatistics.LevelsInput(data, null, exposure);
        Assert.Equal(Mean(applied.Luma), Mean(input.Luma), 0);       // the same tones as the exposed preview
        Assert.True(Mean(input.Luma) > Mean(plain.Luma) + 20);
    }

    private static double Mean(long[] bins) => bins.Select((n, i) => (double)n * i).Sum() / bins.Sum();

    [Fact]
    public void Collage_outputs_are_not_listed_as_order_photos()
    {
        using var dir = new TempDir();
        Fixtures.WriteJpeg(dir.Path, "Collage_20261030_1015.jpg", 120, 80);
        Fixtures.WriteJpeg(dir.Path, "Collage_20261030_1015_1.jpg", 120, 80);
        Fixtures.WriteJpeg(dir.Path, "Collage of family.jpg", 120, 80);
        Assert.Equal(["Collage of family.jpg"], FolderScanner.Scan(dir.Path).Select(e => e.Name));
    }

    [Fact]
    public void Duplicate_never_overwrites_an_existing_copy()
    {
        using var dir = new TempDir();
        var photo = Fixtures.WriteJpeg(dir.Path, "IMG_1.jpg", 120, 80);
        File.WriteAllText(Path.Combine(dir.Path, "IMG_1_copy.jpg"), "someone else's file");
        var copy = PhotoDuplicator.Duplicate(photo);
        Assert.Equal("IMG_1_copy_2.jpg", Path.GetFileName(copy));
        Assert.Equal("someone else's file", File.ReadAllText(Path.Combine(dir.Path, "IMG_1_copy.jpg")));
        Assert.Equal(Fixtures.Sha(photo), Fixtures.Sha(copy));
    }
}

internal static class NormalizedRectExtensions
{
    public static double Right(this NormalizedRect r) => r.X + r.Width;
}
