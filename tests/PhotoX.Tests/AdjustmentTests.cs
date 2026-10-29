using ImageMagick;
using PhotoX.Core.Editing;
using PhotoX.Core.Geometry;
using PhotoX.Imaging;

namespace PhotoX.Tests;

/// <summary>TS-03 / TS-04: histogram, levels, contrast, saturation, tint, rotation, Auto Correct.</summary>
public class AdjustmentTests
{
    private static PixelData Solid(byte r, byte g, byte b, int w = 10, int h = 10)
    {
        var bytes = new byte[w * h * 4];
        for (var i = 0; i < bytes.Length; i += 4)
        {
            bytes[i] = b;
            bytes[i + 1] = g;
            bytes[i + 2] = r;
            bytes[i + 3] = 255;
        }

        return new PixelData(w, h, bytes);
    }

    [Fact]
    public void Histogram_counts_bins_and_respects_region()
    {
        var data = Solid(200, 100, 50, 20, 10);
        // Paint the left half black.
        for (var y = 0; y < 10; y++)
        for (var x = 0; x < 10; x++)
        {
            var i = (y * 20 + x) * 4;
            data.Bgra[i] = data.Bgra[i + 1] = data.Bgra[i + 2] = 0;
        }

        var all = ImageStatistics.Histogram(data);
        Assert.Equal(200, all.Count);
        Assert.Equal(100, all.Red[0]);
        Assert.Equal(100, all.Red[200]);
        var right = ImageStatistics.Histogram(data, new PixelRect(10, 0, 10, 10));
        Assert.Equal(100, right.Count);
        Assert.Equal(0, right.Red[0]);
        Assert.Equal(100, right.Green[100]);
        Assert.Equal(100, right.Luma[(2126 * 200 + 7152 * 100 + 722 * 50 + 5000) / 10000]);
    }

    [Theory]
    [InlineData("contrast+")]
    [InlineData("contrast-")]
    [InlineData("saturation+")]
    [InlineData("saturation-")]
    [InlineData("tint+")]
    [InlineData("levels")]
    public void Adjustments_change_statistics_in_the_expected_direction(string which)
    {
        using var src = Fixtures.Gradient(96, 64);
        var r = which switch
        {
            "contrast+" => new EditRecipe { Contrast = 60 },
            "contrast-" => new EditRecipe { Contrast = -60 },
            "saturation+" => new EditRecipe { Saturation = 60 },
            "saturation-" => new EditRecipe { Saturation = -100 },
            "tint+" => new EditRecipe { Tint = 60 },
            _ => new EditRecipe { LevelsBlack = 0.2, LevelsWhite = 0.8 },
        };
        using var outImg = RecipeRenderer.RenderPreview(src, r);
        var a = src.Statistics();
        var b = outImg.Statistics();
        double Std(IStatistics s) => s.GetChannel(PixelChannel.Green)!.StandardDeviation;
        double Mean(IStatistics s, PixelChannel c) => s.GetChannel(c)!.Mean;
        switch (which)
        {
            case "contrast+": Assert.True(Std(b) > Std(a)); break;
            case "contrast-": Assert.True(Std(b) < Std(a)); break;
            case "levels": Assert.True(Std(b) > Std(a)); break;
            case "tint+": Assert.True(Mean(b, PixelChannel.Green) < Mean(a, PixelChannel.Green), "magenta = less green"); break;
            default:
            {
                using var hsl = (MagickImage)outImg.Clone();
                using var hslA = (MagickImage)src.Clone();
                hsl.ColorSpace = ColorSpace.HSL;
                hslA.ColorSpace = ColorSpace.HSL;
                var sb = hsl.Statistics().GetChannel(PixelChannel.Green)!.Mean;
                var sa = hslA.Statistics().GetChannel(PixelChannel.Green)!.Mean;
                Assert.True(which == "saturation+" ? sb > sa : sb < sa * 0.2, $"{sa} → {sb}");
                break;
            }
        }
    }

    [Fact]
    public void Quarter_turns_and_straighten_change_geometry_as_planned()
    {
        using var src = Fixtures.Gradient(90, 60);
        using var turned = RecipeRenderer.RenderPreview(src, new EditRecipe { QuarterTurns = 1 }, applyTone: false);
        Assert.Equal((60u, 90u), (turned.Width, turned.Height));
        using var straight = RecipeRenderer.RenderPreview(src, new EditRecipe { Straighten = 5 }, applyTone: false);
        var (w, h) = CropMath.RotatedSize(90, 60, 5);
        Assert.Equal((uint)Math.Round(w), straight.Width);
        Assert.Equal((uint)Math.Round(h), straight.Height);

        // A crop inside the valid area has no white (empty) corners after straightening.
        var valid = CropMath.ValidAreaAfterRotation(900, 600, 5);
        var recipe = new EditRecipe { Straighten = 5, Crop = NormalizedRect.FromPixels(valid, w * 10, h * 10) };
        using var big = Fixtures.Gradient(900, 600);
        using var final = RecipeRenderer.RenderFinal(big, recipe);
        using var px = final.GetPixels();
        foreach (var (x, y) in new[] { (0, 0), ((int)final.Width - 1, 0), (0, (int)final.Height - 1), ((int)final.Width - 1, (int)final.Height - 1) })
        {
            var c = px.GetPixel(x, y).ToColor()!;
            Assert.False(c.R > 64000 && c.G > 64000 && c.B > 64000, $"corner {x},{y} is empty");
        }
    }

    [Fact]
    public void Auto_correct_brightens_dark_photo_within_limits_and_leaves_good_one_nearly_alone()
    {
        using var dark = Fixtures.Gradient(100, 80);
        dark.Evaluate(Channels.RGB, EvaluateOperator.Multiply, 0.35);
        var hDark = ImageStatistics.Histogram(PixelData.From(dark));
        var r = AutoCorrect.Apply(new EditRecipe(), hDark, null);
        Assert.InRange(r.Exposure, 0.2, 1.0);
        Assert.InRange(r.LevelsBlack, 0, 0.06);
        Assert.InRange(r.LevelsWhite, 0.88, 1);

        using var good = Fixtures.Gradient(100, 80);
        var rGood = AutoCorrect.Apply(new EditRecipe(), ImageStatistics.Histogram(PixelData.From(good)), null);
        Assert.InRange(rGood.Exposure, -0.35, 0.35);
    }

    [Fact]
    public void Auto_correct_reduces_a_blue_cast_and_caps_strength()
    {
        var bluish = Solid(120, 125, 145, 50, 50);
        var neutral = ImageStatistics.NeutralMeans(bluish);
        Assert.True(neutral.Fraction > 0.9);
        var r = AutoCorrect.Apply(new EditRecipe(), ImageStatistics.Histogram(bluish), neutral);
        Assert.InRange(r.Temperature, 1, 25); // warmer
        Assert.InRange(Math.Abs(r.Tint), 0, 25);
    }
}
