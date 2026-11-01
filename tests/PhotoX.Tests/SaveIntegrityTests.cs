using ImageMagick;
using PhotoX.Core.Editing;
using PhotoX.Core.Geometry;
using PhotoX.Imaging;
using PhotoX.Storage;

namespace PhotoX.Tests;

/// <summary>
/// prompt_07 §1.6: saved photos sporadically mostly black on Windows. Full-size photos (22 MP, like the studio's
/// camera files) go through the real save pipeline in several variants, several at the same time, and every saved
/// file is read back and checked band by band for black areas.
/// </summary>
public class SaveIntegrityTests(ITestOutputHelper output)
{
    private const int W = 3840;
    private const int H = 5760;

    /// <summary>A smooth, textured 22 MP photo without dark areas (every band is mid-grey or brighter).</summary>
    private static string WriteLargePhoto(string dir, string name)
    {
        using var small = new MagickImage("plasma:fractal", new MagickReadSettings { Width = 240, Height = 360 });
        small.Level(new Percentage(0), new Percentage(100));
        small.Evaluate(Channels.All, EvaluateOperator.Multiply, 0.5);
        small.Evaluate(Channels.All, EvaluateOperator.Add, 65535 * 0.3);
        small.Resize(new MagickGeometry(W, H) { IgnoreAspectRatio = true });
        small.Quality = 90;
        var path = Path.Combine(dir, name);
        small.Write(path, MagickFormat.Jpeg);
        return path;
    }

    private static readonly (string Name, EditRecipe Recipe)[] Variants =
    [
        ("tone", new EditRecipe { Crop = new NormalizedRect(0, 0, 1, 1), Exposure = 0.3 }),
        ("turn-right-crop", new EditRecipe { QuarterTurns = 1, Orientation = CropOrientation.Landscape, Crop = new NormalizedRect(0.05, 0.05, 0.9, 0.9) }),
        ("turn-right-crop-tone", new EditRecipe { QuarterTurns = 1, Orientation = CropOrientation.Landscape, Crop = new NormalizedRect(0.05, 0.05, 0.9, 0.9), Exposure = 0.3, Contrast = 15 }),
        ("turn-left-straighten-tone", new EditRecipe { QuarterTurns = 3, Straighten = 3, Orientation = CropOrientation.Landscape, Crop = new NormalizedRect(0.1, 0.1, 0.8, 0.8), Saturation = 20 }),
        ("half-turn-crop", new EditRecipe { QuarterTurns = 2, Crop = new NormalizedRect(0.1, 0.05, 0.8, 0.8) }),
        ("straighten-tone", new EditRecipe { Straighten = -2, Crop = new NormalizedRect(0.1, 0.1, 0.8, 0.8), Temperature = 20 }),
    ];

    [Fact]
    public async Task Full_size_saves_are_complete_in_every_variant_even_concurrently()
    {
        using var dir = new TempDir();
        var source = WriteLargePhoto(dir.Path, "source.jpg");
        var failures = new List<string>();
        for (var round = 0; round < 2; round++)
        {
            var jobs = Variants.Select(v => Task.Run(() =>
            {
                var folder = Path.Combine(dir.Path, $"{v.Name}-{round}");
                Directory.CreateDirectory(folder);
                var photo = Path.Combine(folder, "photo.jpg");
                File.Copy(source, photo);
                var result = new PhotoSaver(100).Save(photo, v.Recipe, FileStamp.Of(photo)!.Value);
                if (!result.Succeeded) return $"{v.Name}: save failed {result.Error}";
                return CheckBands(photo, v.Name);
            })).ToList();
            // Meanwhile, previews and thumbnails are decoded as in the app.
            var readers = Enumerable.Range(0, 3).Select(_ => Task.Run(() =>
            {
                using var preview = ImageLoader.Load(source, 1600);
                return (string?)null;
            }));
            foreach (var r in await Task.WhenAll(jobs.Concat(readers))) if (r is not null) failures.Add(r);
        }

        Assert.True(failures.Count == 0, string.Join("\n", failures));
    }

    [Fact]
    public void The_tone_pass_matches_the_reference_on_a_full_size_photo()
    {
        // The tone pass works in strips of rows; a 22 MP photo has several strips (small test images have one).
        using var dir = new TempDir();
        var source = WriteLargePhoto(dir.Path, "source.jpg");
        using var a = new MagickImage(source);
        using var b = (MagickImage)a.Clone();
        var recipe = new EditRecipe { Exposure = 0.4, Contrast = 20, Saturation = 15 };
        ToneProcessor.Apply(a, recipe);
        RecipeRenderer.ApplyTone(b, recipe);
        for (var band = 0; band < 16; band++)
        {
            var y = (int)(a.Height * band / 16);
            var h = (uint)(a.Height / 16);
            using var ca = (MagickImage)a.Clone();
            using var cb = (MagickImage)b.Clone();
            ca.Crop(new MagickGeometry(0, y, a.Width, h));
            cb.Crop(new MagickGeometry(0, y, b.Width, h));
            var diff = ca.Compare(cb, ErrorMetric.MeanAbsolute);
            Assert.True(diff < 0.002, $"band {band}: {diff:P2}");
        }
    }

    /// <summary>
    /// The owner's examples (prompt_07 §1.6, never committed; skipped when absent): copies are saved with the default
    /// crop and an exposure change, also turned by 90°, and each saved photo is read back the way the gallery
    /// thumbnail, ID Photos, Collages and printing read it. The examples themselves are only read.
    /// </summary>
    [Fact]
    public void Owner_examples_save_completely_and_read_back_in_every_workspace()
    {
        var folder = OwnerExamples();
        Assert.SkipWhen(folder is null, "Owner examples not available");
        var files = Directory.GetFiles(folder!, "C33A00*.jpg").Order().Take(2).Concat(Directory.GetFiles(folder!, "PXL_*.jpg").Order().Take(1)).ToList();
        using var dir = new TempDir();
        var failures = new List<string>();
        foreach (var file in files)
        {
            var before = Fixtures.Sha(file);
            foreach (var turns in new[] { 0, 1 })
            {
                var copyDir = Path.Combine(dir.Path, $"{Path.GetFileNameWithoutExtension(file)}-{turns}");
                Directory.CreateDirectory(copyDir);
                var copy = Path.Combine(copyDir, Path.GetFileName(file));
                File.Copy(file, copy);
                var info = ImageProbe.Probe(copy);
                var (w, h) = turns % 2 == 1 ? (info.OrientedHeight, info.OrientedWidth) : (info.OrientedWidth, info.OrientedHeight);
                var recipe = new EditRecipe { QuarterTurns = turns, Orientation = CropMath.OrientationFor(w, h), Exposure = 0.3 };
                recipe = recipe with { Crop = NormalizedRect.FromPixels(CropMath.LargestCentered(RectD.FromSize(w, h), recipe.CropRatio), w, h) };
                var sw = System.Diagnostics.Stopwatch.StartNew();
                var result = new PhotoSaver(100).Save(copy, recipe, FileStamp.Of(copy)!.Value);
                output.WriteLine($"{Path.GetFileName(file)} ({info.OrientedWidth}×{info.OrientedHeight}), turns {turns}: save {sw.ElapsedMilliseconds} ms");
                if (!result.Succeeded)
                {
                    failures.Add($"{file}: {result.Error}");
                    continue;
                }

                // The reference: the original (kept in Original/) turned, cropped and toned with Magick's own operations.
                using var reference = ImageLoader.Load(Path.Combine(copyDir, "Original", Path.GetFileName(file)));
                RecipeRenderer.ApplyRotation(reference, recipe);
                var c = CropMath.ToPixelRect(recipe.Crop, (int)reference.Width, (int)reference.Height);
                reference.Crop(new MagickGeometry(c.X, c.Y, (uint)c.Width, (uint)c.Height));
                reference.ResetPage();
                RecipeRenderer.ApplyTone(reference, recipe);
                using (var saved = ImageLoader.Load(copy))
                {
                    if (Compare(saved, reference, $"{Path.GetFileName(file)} turns {turns}, full size (ID Photos, printing)") is { } f) failures.Add(f);
                }

                using (var preview = ImageLoader.Load(copy, 1600))
                {
                    if (Compare(preview, reference, $"{Path.GetFileName(file)} turns {turns}, preview (Collages, print preview)") is { } f) failures.Add(f);
                }

                using (var thumb = new MagickImage(Thumbnailer.Create(copy)))
                {
                    if (Compare(thumb, reference, $"{Path.GetFileName(file)} turns {turns}, gallery thumbnail") is { } f) failures.Add(f);
                }
            }

            Assert.Equal(before, Fixtures.Sha(file));
        }

        Assert.True(failures.Count == 0, string.Join("\n", failures));
    }

    /// <summary>Compares 16 horizontal bands of <paramref name="image"/> with the reference at the same size.</summary>
    private string? Compare(MagickImage image, MagickImage reference, string name)
    {
        using var expected = (MagickImage)reference.Clone();
        if (expected.Width != image.Width || expected.Height != image.Height) expected.Resize(new MagickGeometry(image.Width, image.Height) { IgnoreAspectRatio = true });
        var diffs = new List<double>();
        for (var band = 0; band < 16; band++)
        {
            var y = (int)(image.Height * band / 16);
            using var a = (MagickImage)image.Clone();
            using var b = (MagickImage)expected.Clone();
            a.Crop(new MagickGeometry(0, y, image.Width, image.Height / 16));
            b.Crop(new MagickGeometry(0, y, image.Width, image.Height / 16));
            diffs.Add(a.Compare(b, ErrorMetric.MeanAbsolute));
        }

        var line = $"{name}: {image.Width}×{image.Height}, band differences " + string.Join(" ", diffs.Select(d => d.ToString("0.000")));
        output.WriteLine(line);
        return diffs.Any(d => d > 0.04) ? "DIFFERENT " + line : null;
    }

    private static string? OwnerExamples()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "PhotoX.slnx"))) dir = dir.Parent;
        var p = dir is null ? null : Path.Combine(dir.FullName, "photos_example");
        return p is not null && Directory.Exists(p) ? p : null;
    }

    /// <summary>Reads the saved photo at full size and reports any band whose brightness collapsed.</summary>
    private string? CheckBands(string path, string name)
    {
        using var saved = ImageLoader.Load(path);
        return CheckBands(saved, name);
    }

    private string? CheckBands(MagickImage saved, string name)
    {
        var stats = new List<double>();
        for (var band = 0; band < 16; band++)
        {
            var y = (int)(saved.Height * band / 16);
            using var part = (MagickImage)saved.Clone();
            part.Crop(new MagickGeometry(0, y, saved.Width, saved.Height / 16));
            part.ColorSpace = ColorSpace.Gray;
            stats.Add(part.Statistics().GetChannel(PixelChannel.Gray)!.Mean / 65535);
        }

        var line = $"{name}: {saved.Width}×{saved.Height}, band means " + string.Join(" ", stats.Select(s => s.ToString("0.00")));
        output.WriteLine(line);
        return stats.Any(s => s < 0.12) ? "BLACK " + line : null;
    }
}
