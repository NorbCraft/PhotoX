using ImageMagick;
using PhotoX.Core.Editing;
using PhotoX.Core.Geometry;

namespace PhotoX.Imaging;

/// <summary>
/// Applies an <see cref="EditRecipe"/> with Magick.NET operations (DEC-026). Preview and save use the same steps;
/// the preview omits crop and canvas because the crop is drawn as an overlay.
/// </summary>
public static class RecipeRenderer
{
    /// <summary>Rotation and tone adjustments only (no crop/canvas), for the editor preview.</summary>
    public static MagickImage RenderPreview(MagickImage proxy, EditRecipe recipe, bool applyTone = true)
    {
        var image = (MagickImage)proxy.Clone();
        ApplyRotation(image, recipe);
        if (applyTone)
        {
            ApplyTone(image, recipe);
        }

        return image;
    }

    /// <summary>Full render for saving: rotation, the exact selected crop, tone and composition (FR-004, FR-049, FR-073).</summary>
    public static MagickImage RenderFinal(MagickImage source, EditRecipe recipe)
    {
        var image = (MagickImage)source.Clone();
        try
        {
            ApplyRotation(image, recipe);
            var crop = recipe.Composition == Composition.Fill ? recipe.Crop : NormalizedRect.Full;
            var c = CropMath.ToPixelRect(crop, (int)image.Width, (int)image.Height);
            if (c.X != 0 || c.Y != 0 || c.Width != (int)image.Width || c.Height != (int)image.Height)
            {
                image.Crop(new MagickGeometry(c.X, c.Y, (uint)c.Width, (uint)c.Height));
                image.ResetPage();
            }

            ApplyTone(image, recipe);

            var (canvasW, canvasH, _) = RenderPlan.CanvasFor(c.Width, c.Height, recipe);
            if (canvasW > c.Width || canvasH > c.Height)
            {
                image.BackgroundColor = MagickColors.White;
                image.Extent((uint)canvasW, (uint)canvasH, Gravity.Center, MagickColors.White);
            }

            return image;
        }
        catch
        {
            image.Dispose();
            throw;
        }
    }

    public static void ApplyRotation(MagickImage image, EditRecipe recipe)
    {
        var turns = ((recipe.QuarterTurns % 4) + 4) % 4;
        if (turns != 0)
        {
            image.Rotate(90 * turns);
        }

        if (Math.Abs(recipe.Straighten) > 1e-6)
        {
            image.BackgroundColor = MagickColors.White;
            image.Rotate(recipe.Straighten);
            image.ResetPage();
        }
    }

    public static void ApplyTone(MagickImage image, EditRecipe r)
    {
        // 1. White balance, tint and exposure as per-channel gains in linear light.
        var (gr, gg, gb) = ChannelGains(r);
        if (Math.Abs(gr - 1) > 1e-6 || Math.Abs(gg - 1) > 1e-6 || Math.Abs(gb - 1) > 1e-6)
        {
            var wasGray = image.ColorType is ColorType.Grayscale or ColorType.GrayscaleAlpha;
            if (wasGray)
            {
                image.ColorType = image.HasAlpha ? ColorType.TrueColorAlpha : ColorType.TrueColor;
            }

            image.ColorSpace = ColorSpace.RGB;
            if (Math.Abs(gr - 1) > 1e-6) image.Evaluate(Channels.Red, EvaluateOperator.Multiply, gr);
            if (Math.Abs(gg - 1) > 1e-6) image.Evaluate(Channels.Green, EvaluateOperator.Multiply, gg);
            if (Math.Abs(gb - 1) > 1e-6) image.Evaluate(Channels.Blue, EvaluateOperator.Multiply, gb);
            image.ColorSpace = ColorSpace.sRGB;
        }

        // 2. Levels.
        if (r.LevelsBlack > 1e-6 || r.LevelsWhite < 1 - 1e-6 || Math.Abs(r.LevelsMid - 1) > 1e-6)
        {
            var black = Math.Clamp(r.LevelsBlack, 0, 0.95);
            var white = Math.Clamp(r.LevelsWhite, black + 0.02, 1);
            image.Level(new Percentage(black * 100), new Percentage(white * 100), Math.Clamp(r.LevelsMid, 0.2, 5));
        }

        // 3. Contrast (sigmoidal, around mid-grey).
        var strength = Math.Abs(r.Contrast) / 100 * 8;
        if (strength > 0.05)
        {
            if (r.Contrast > 0)
            {
                image.SigmoidalContrast(strength, new Percentage(50));
            }
            else
            {
                image.InverseSigmoidalContrast(strength, new Percentage(50));
            }
        }

        // 4. Saturation in a perceptual colour space.
        if (Math.Abs(r.Saturation) > 1e-6)
        {
            image.SetArtifact("modulate:colorspace", "HCL");
            image.Modulate(new Percentage(100), new Percentage(Math.Max(0, 100 + r.Saturation)), new Percentage(100));
            image.RemoveArtifact("modulate:colorspace");
        }
    }

    /// <summary>
    /// Linear-light gains: exposure 2^EV; temperature moves red/blue in opposite directions; tint moves green
    /// against magenta. Colour gains are normalized to keep Rec. 709 luminance constant.
    /// </summary>
    public static (double R, double G, double B) ChannelGains(EditRecipe r)
    {
        var t = Math.Clamp(r.Temperature, -100, 100) / 100 * 0.30;
        var u = Math.Clamp(r.Tint, -100, 100) / 100 * 0.20;
        var red = 1 + t;
        var blue = 1 - t;
        var green = 1 - u;
        var luminance = 0.2126 * red + 0.7152 * green + 0.0722 * blue;
        var e = Math.Pow(2, Math.Clamp(r.Exposure, -4, 4));
        return (red / luminance * e, green / luminance * e, blue / luminance * e);
    }
}
