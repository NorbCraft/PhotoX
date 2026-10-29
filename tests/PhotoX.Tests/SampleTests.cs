using System.Diagnostics;
using ImageMagick;
using PhotoX.Core.Editing;
using PhotoX.Core.Formats;
using PhotoX.Core.Geometry;
using PhotoX.Imaging;
using PhotoX.Storage;

namespace PhotoX.Tests;

/// <summary>
/// Optional checks with the owner's real sample (never committed). The sample is only ever read; all writes happen
/// on a copy in a temporary folder, and the original's checksum is verified afterwards.
/// </summary>
public class SampleTests(ITestOutputHelper output)
{
    public static string? SamplePath()
    {
        var env = Environment.GetEnvironmentVariable("PHOTOX_SAMPLE");
        if (env is not null && File.Exists(env)) return env;
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "PhotoX.slnx"))) dir = dir.Parent;
        var p = dir is null ? null : Path.Combine(dir.FullName, "id_photo", "IMG_6153.JPG");
        return p is not null && File.Exists(p) ? p : null;
    }

    [Fact]
    public void Real_sample_saves_with_default_crop_on_a_copy()
    {
        var sample = SamplePath();
        Assert.SkipWhen(sample is null, "Owner sample not available");
        var before = Fixtures.Sha(sample!);
        using var dir = new TempDir();
        var copy = dir.File("IMG_6153.JPG");
        File.Copy(sample!, copy);

        var sw = Stopwatch.StartNew();
        var info = ImageProbe.Probe(copy);
        using (var proxy = ImageLoader.Load(copy, 1800))
        {
            output.WriteLine($"Probe+proxy load: {sw.ElapsedMilliseconds} ms ({info.OrientedWidth}×{info.OrientedHeight} → {proxy.Width}×{proxy.Height})");
        }

        var orientation = CropMath.OrientationFor(info.OrientedWidth, info.OrientedHeight);
        var recipe = new EditRecipe { Orientation = orientation, Exposure = 0.2 };
        var crop = CropMath.LargestCentered(RectD.FromSize(info.OrientedWidth, info.OrientedHeight), recipe.CropRatio);
        recipe = recipe with { Crop = NormalizedRect.FromPixels(crop, info.OrientedWidth, info.OrientedHeight) };

        sw.Restart();
        var result = new PhotoSaver().Save(copy, recipe, FileStamp.Of(copy)!.Value);
        output.WriteLine($"Full save: {sw.ElapsedMilliseconds} ms, outcome {result.Outcome}");
        Assert.True(result.Succeeded, result.Detail);

        using var saved = new MagickImage(copy);
        var expected = CropMath.ToPixelRect(recipe.Crop, info.OrientedWidth, info.OrientedHeight);
        Assert.Equal((uint)expected.Width, saved.Width);
        Assert.Equal((uint)expected.Height, saved.Height);
        Assert.Equal(2.0 / 3.0, Math.Min(saved.Width, saved.Height) / (double)Math.Max(saved.Width, saved.Height), 2);
        Assert.Equal(95u, saved.Quality);
        Assert.Equal(before, Fixtures.Sha(Path.Combine(dir.Path, "Original", "IMG_6153.JPG")));
        Assert.Equal(before, Fixtures.Sha(sample!)); // the supplied reference is untouched
        output.WriteLine($"Output {saved.Width}×{saved.Height}, {new FileInfo(copy).Length / 1024} KB, ppi {saved.Density.X:0}");
        _ = PrintFormats.P10x15;
    }
}
