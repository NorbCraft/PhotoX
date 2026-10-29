using ImageMagick;
using PhotoX.Core.Editing;
using PhotoX.Imaging;

namespace PhotoX.Tests;

/// <summary>DEC-044: the fast tone path produces the pixels of the Magick.NET reference operations.</summary>
public class ToneProcessorTests
{
    public static TheoryData<string, EditRecipe> Recipes => new()
    {
        { "exposure", new EditRecipe { Exposure = 0.5 } },
        { "dark", new EditRecipe { Exposure = -1.2, Contrast = -30 } },
        { "warm-green", new EditRecipe { Temperature = 40, Tint = -25 } },
        { "levels", new EditRecipe { LevelsBlack = 0.06, LevelsMid = 1.4, LevelsWhite = 0.9 } },
        { "saturation+", new EditRecipe { Saturation = 35 } },
        { "saturation-", new EditRecipe { Saturation = -60, Contrast = 15 } },
        { "everything", new EditRecipe { Exposure = 0.3, Contrast = 20, Saturation = 20, Temperature = -15, Tint = 10, LevelsBlack = 0.02, LevelsMid = 0.8, LevelsWhite = 0.97 } },
    };

    private static MagickImage Colourful(int depth)
    {
        var image = new MagickImage("plasma:", new MagickReadSettings { Width = 160, Height = 120 });
        image.Depth = (uint)depth;
        return image;
    }

    private static int MaxDifference(MagickImage a, MagickImage b)
    {
        Assert.Equal((a.Width, a.Height, a.ChannelCount), (b.Width, b.Height, b.ChannelCount));
        using var pa = a.GetPixelsUnsafe();
        using var pb = b.GetPixelsUnsafe();
        var x = pa.GetArea(0, 0, a.Width, a.Height)!;
        var y = pb.GetArea(0, 0, b.Width, b.Height)!;
        var max = 0;
        for (var i = 0; i < x.Length; i++) max = Math.Max(max, Math.Abs(x[i] - y[i]));
        return max;
    }

    [Theory]
    [MemberData(nameof(Recipes))]
    public void Fast_path_equals_magick_operations_8_and_16_bit(string name, EditRecipe recipe)
    {
        foreach (var depth in new[] { 8, 16 })
        {
            using var reference = Colourful(depth);
            using var fast = (MagickImage)reference.Clone();
            RecipeRenderer.ApplyTone(reference, recipe);
            ToneProcessor.Apply(fast, recipe);
            // Curves are exact; HCL saturation is the same formula in double precision (rounding may differ by 1).
            Assert.True(MaxDifference(reference, fast) <= 1, $"{name} at {depth} bit");
        }
    }

    [Fact]
    public void Alpha_is_kept_and_colour_matches()
    {
        using var reference = Colourful(16);
        reference.Alpha(AlphaOption.Set);
        reference.Evaluate(Channels.Alpha, EvaluateOperator.Multiply, 0.5);
        using var fast = (MagickImage)reference.Clone();
        var recipe = new EditRecipe { Exposure = 0.4, Contrast = 25, Saturation = 20, Temperature = 10 };
        RecipeRenderer.ApplyTone(reference, recipe);
        ToneProcessor.Apply(fast, recipe);
        Assert.True(MaxDifference(reference, fast) <= 1);
    }

    [Fact]
    public void Greyscale_stays_grey_without_colour_changes_and_matches_with_them()
    {
        using var grey = Colourful(8);
        grey.Grayscale();
        grey.ColorType = ColorType.Grayscale;

        using (var reference = (MagickImage)grey.Clone())
        using (var fast = (MagickImage)grey.Clone())
        {
            var recipe = new EditRecipe { Contrast = 30, LevelsBlack = 0.05 };
            RecipeRenderer.ApplyTone(reference, recipe);
            ToneProcessor.Apply(fast, recipe);
            Assert.Equal(ColorType.Grayscale, fast.ColorType);
            Assert.True(MaxDifference(reference, fast) <= 1);
        }

        using (var reference = (MagickImage)grey.Clone())
        using (var fast = (MagickImage)grey.Clone())
        {
            var recipe = new EditRecipe { Temperature = 30, Exposure = 0.2 };
            RecipeRenderer.ApplyTone(reference, recipe);
            ToneProcessor.Apply(fast, recipe);
            Assert.True(MaxDifference(reference, fast) <= 1);
        }
    }

    [Fact]
    public void Preview_pixels_match_the_saved_rendering_within_one_level()
    {
        using var image = Colourful(8);
        var recipe = new EditRecipe { Exposure = 0.3, Contrast = 20, Saturation = 25, Temperature = -15, Tint = 10, LevelsBlack = 0.02 };
        var source = PixelData.From(image).Bgra;
        var preview = new byte[source.Length];
        ToneProcessor.ApplyToPreview(source, preview, ToneCurve.For(recipe, exact: false), recipe.Saturation);
        RecipeRenderer.ApplyTone(image, recipe);
        var expected = PixelData.From(image).Bgra;
        var max = 0;
        for (var i = 0; i < expected.Length; i++) max = Math.Max(max, Math.Abs(expected[i] - preview[i]));
        Assert.True(max <= 1, $"max difference {max}");
    }

    [Fact]
    public void Neutral_recipe_changes_nothing()
    {
        using var image = Colourful(8);
        using var copy = (MagickImage)image.Clone();
        ToneProcessor.Apply(copy, new EditRecipe());
        Assert.Equal(0, MaxDifference(image, copy));
        Assert.True(ToneCurve.For(new EditRecipe { Saturation = 20 }).IsIdentity);
    }
}
