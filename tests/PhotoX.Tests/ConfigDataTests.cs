using ImageMagick;
using PhotoX.Core.Formats;
using PhotoX.Core.Id;
using PhotoX.Imaging.Id;
using PhotoX.Storage;

namespace PhotoX.Tests;

/// <summary>Print formats and ID sizes are process-wide; these tests change them, so they never run alongside others.</summary>
[CollectionDefinition(nameof(StaticConfiguration), DisableParallelization = true)]
public sealed class StaticConfiguration;

/// <summary>FR-010, FR-040, FR-061, FR-071: configuration data files and the ID print scale correction.</summary>
[Collection(nameof(StaticConfiguration))]
public class ConfigDataTests
{
    private static void Restore()
    {
        PrintFormats.Configure(PrintFormats.Defaults);
        IdSizes.Configure(IdSizes.Defaults);
    }

    /// <summary>prompt_04 §8: a data file written by 0.6.0 gets the new 3×4, 2×2 in and 9×12 defaults unless edited.</summary>
    [Fact]
    public void Unedited_earlier_id_defaults_are_updated_in_the_file()
    {
        using var dir = new TempDir();
        var paths = new AppPaths(dir.Path);
        var file = Path.Combine(paths.ConfigDir, ConfigData.IdSizesFile);
        var edited = IdSizes.S35x45 with { HeadFraction = 0.72 };
        JsonFile.WriteAtomic(file, new List<IdSize> { IdSizes.Superseded[0], edited, IdSizes.Superseded[1], IdSizes.Superseded[2] });
        try
        {
            ConfigData.Load(paths);
            Assert.Equal(0.70, IdSizes.BuiltIn.Single(s => s.Id == "3x4").HeadFraction);
            Assert.Equal(0.72, IdSizes.BuiltIn.Single(s => s.Id == "35x45").HeadFraction);
            Assert.False(IdSizes.BuiltIn.Single(s => s.Id == "9x12").Guides);
            var reread = JsonFile.Read<List<IdSize>>(file)!;
            Assert.Equal([1, 2, 4], reread.Single(s => s.Id == "2x2in").FamiliarCounts);
            Assert.Equal(0.0, reread.Single(s => s.Id == "2x2in").SheetMarginMm);
            Assert.Equal(0.72, reread.Single(s => s.Id == "35x45").HeadFraction);
        }
        finally
        {
            Restore();
        }
    }

    [Fact]
    public void Missing_files_are_created_with_the_planned_values()
    {
        using var dir = new TempDir();
        var paths = new AppPaths(dir.Path);
        try
        {
            ConfigData.Load(paths);
            var formats = File.ReadAllText(Path.Combine(paths.ConfigDir, ConfigData.PrintFormatsFile));
            Assert.Contains("\"widthMm\": 101.6", formats);
            Assert.DoesNotContain("portraitRatio", formats); // only editable values are written
            Assert.Contains("\"stripBelowMm\": 7", File.ReadAllText(Path.Combine(paths.ConfigDir, ConfigData.IdSizesFile)));
            Assert.Equal(152.4, PrintFormats.P10x15.HeightMm);
            Assert.Equal(6, IdSizes.BuiltIn.Count);
        }
        finally
        {
            Restore();
        }
    }

    [Fact]
    public void Edited_values_apply_and_implausible_ones_are_ignored()
    {
        using var dir = new TempDir();
        var paths = new AppPaths(dir.Path);
        Directory.CreateDirectory(paths.ConfigDir);
        File.WriteAllText(Path.Combine(paths.ConfigDir, ConfigData.PrintFormatsFile), """
            [ { "id": "10x15", "name": "10 × 15", "widthMm": 102, "heightMm": 152 },
              { "id": "15x20", "name": "15 × 20", "widthMm": -1, "heightMm": 203.2 },
              { "id": "9x13", "name": "9 × 13", "widthMm": 89, "heightMm": 127 },
              { "id": "8x10", "name": "8 × 10", "widthMm": 203.2, "heightMm": 254 } ]
            """);
        File.WriteAllText(Path.Combine(paths.ConfigDir, ConfigData.IdSizesFile), """
            [ { "id": "3x4", "label": "3 × 4 cm", "widthMm": 30, "heightMm": 40, "stripBelowMm": 7, "familiarCounts": [4, 8], "crownFraction": 0.1, "headFraction": 0.75 },
              { "id": "bad", "label": "bad", "widthMm": 500, "heightMm": 40, "stripBelowMm": 0, "familiarCounts": [1], "crownFraction": 0.1, "headFraction": 0.75 } ]
            """);
        try
        {
            ConfigData.Load(paths);
            Assert.Equal((102.0, 152.0), (PrintFormats.P10x15.WidthMm, PrintFormats.P10x15.HeightMm));
            Assert.Equal(152.4, PrintFormats.P15x20.WidthMm); // the negative width was ignored
            Assert.Null(PrintFormats.Find("8x10"));           // the presets are fixed; unknown ids are ignored
            Assert.Equal(90, PrintFormats.Find("9x13")!.WidthMm); // 9×13 needs its 10×15 canvas; an entry without one is ignored
            Assert.Equal("3x4", Assert.Single(IdSizes.BuiltIn).Id);
        }
        finally
        {
            Restore();
        }
    }

    [Fact]
    public void Unreadable_file_is_left_untouched_and_defaults_apply()
    {
        using var dir = new TempDir();
        var paths = new AppPaths(dir.Path);
        Directory.CreateDirectory(paths.ConfigDir);
        var file = Path.Combine(paths.ConfigDir, ConfigData.PrintFormatsFile);
        File.WriteAllText(file, "{ not json");
        try
        {
            var messages = ConfigData.Load(paths);
            Assert.Contains(messages, m => m.Contains(ConfigData.PrintFormatsFile));
            Assert.Equal("{ not json", File.ReadAllText(file));
            Assert.Equal(101.6, PrintFormats.P10x15.WidthMm);
        }
        finally
        {
            Restore();
        }
    }

    [Fact]
    public void Scale_correction_shrinks_photos_and_layout_about_the_page_centre()
    {
        var size = IdSizes.S3x4;
        Assert.Equal((354, 472), SheetRenderer.PhotoPixels(size));
        Assert.Equal((347, 463), SheetRenderer.PhotoPixels(size, 0.98));
        var layout = SheetLayout.Compute(size, new SheetOptions(8));
        var (w, h) = SheetRenderer.PhotoPixels(size, 0.98);
        using var photo = new MagickImage(new MagickColor("#204060"), (uint)w, (uint)h);
        using var page = SheetRenderer.Render(photo, layout, 0.98);
        using var plain = SheetRenderer.Render(photo, layout);
        // The layout block is centred in both cases, but its extent shrinks by the correction.
        static (int Min, int Max) Columns(MagickImage p)
        {
            using var px = p.GetPixels();
            var y = (int)p.Height / 4;
            var cols = Enumerable.Range(0, (int)p.Width).Where(x => px.GetPixel(x, y).ToColor()!.R < 30000).ToList();
            return (cols.Min(), cols.Max());
        }

        var (a, b) = Columns(page);
        var (c, d) = Columns(plain);
        Assert.True(b - a < d - c);
        Assert.InRange(a - ((int)page.Width - 1 - b), -2, 2); // still centred
    }
}
