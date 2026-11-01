using ImageMagick;
using PhotoX.Core.Editing;
using PhotoX.Core.Geometry;

namespace PhotoX.Imaging;

/// <summary>
/// Applies an <see cref="EditRecipe"/> with Magick.NET operations (DEC-026); tone adjustments run through
/// <see cref="ToneProcessor"/>, which reproduces <see cref="ApplyTone"/> exactly but faster (DEC-044).
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
            ToneProcessor.Apply(image, recipe);
        }

        return image;
    }

    /// <summary>Full render for saving: rotation, the exact selected crop, tone and composition (FR-004, FR-049, FR-073).</summary>
    public static MagickImage RenderFinal(MagickImage source, EditRecipe recipe)
    {
        var image = RenderGeometry(source, recipe);
        try
        {
            ToneProcessor.Apply(image, recipe);

            var (canvasW, canvasH, _) = RenderPlan.CanvasFor((int)image.Width, (int)image.Height, recipe);
            if (canvasW > image.Width || canvasH > image.Height)
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

    /// <summary>
    /// The kept part of the photo, turned and straightened (prompt_07 §1.4): the same pixels as
    /// <see cref="ApplyRotation"/> followed by the crop, but only the kept pixels are computed. Without straightening
    /// the crop is taken from the photo first and only that part is turned (both steps copy pixels exactly). With
    /// straightening the rotation renders straight into the crop, in parallel bands where the Magick library itself
    /// runs on one thread.
    /// </summary>
    public static MagickImage RenderGeometry(MagickImage source, EditRecipe recipe)
    {
        var turns = ((recipe.QuarterTurns % 4) + 4) % 4;
        var crop = recipe.Composition == Composition.Fill ? recipe.Crop : NormalizedRect.Full;
        var (w, h) = ((int)source.Width, (int)source.Height);
        var (tw, th) = turns % 2 == 1 ? (h, w) : (w, h);
        if (Math.Abs(recipe.Straighten) <= 1e-6)
        {
            var c = CropMath.ToPixelRect(crop, tw, th);
            var s = UnturnRect(c, w, h, turns);
            var image = (MagickImage)source.Clone();
            try
            {
                if (s.X != 0 || s.Y != 0 || s.Width != w || s.Height != h)
                {
                    image.Crop(new MagickGeometry(s.X, s.Y, (uint)s.Width, (uint)s.Height));
                    image.ResetPage();
                }

                if (turns != 0) image.Rotate(90 * turns);
                return image;
            }
            catch
            {
                image.Dispose();
                throw;
            }
        }

        MagickImage? turned = null;
        try
        {
            if (turns != 0)
            {
                turned = (MagickImage)source.Clone();
                turned.Rotate(90 * turns);
            }

            var from = turned ?? source;
            var (rw, rh) = CropMath.RotatedSize(from.Width, from.Height, recipe.Straighten);
            var vw = (int)Math.Round(rw);
            var vh = (int)Math.Round(rh);
            var ox = (int)Math.Round((from.Width - vw) / 2.0);
            var oy = (int)Math.Round((from.Height - vh) / 2.0);
            var c = CropMath.ToPixelRect(crop, vw, vh);
            return Straighten(from, recipe.Straighten, ox + c.X, oy + c.Y, c.Width, c.Height);
        }
        finally
        {
            turned?.Dispose();
        }
    }

    /// <summary>A crop of the turned photo, as the matching rectangle of the photo before turning.</summary>
    internal static PixelRect UnturnRect(PixelRect c, int width, int height, int turns) => turns switch
    {
        1 => new PixelRect(c.Y, height - c.X - c.Width, c.Height, c.Width),
        2 => new PixelRect(width - c.X - c.Width, height - c.Y - c.Height, c.Width, c.Height),
        3 => new PixelRect(width - c.Y - c.Height, c.X, c.Height, c.Width),
        _ => c,
    };

    /// <summary>
    /// Rotates <paramref name="image"/> by <paramref name="degrees"/> about its centre and renders only the region
    /// (<paramref name="x"/>, <paramref name="y"/>, <paramref name="width"/> × <paramref name="height"/>) of the rotated
    /// frame, with the same resampling as <see cref="ApplyRotation"/>. The bundled Magick library runs a distortion on
    /// one thread unless it was built with OpenMP, so the region is then split into bands rendered in parallel from
    /// clones that only read the shared pixels.
    /// </summary>
    private static MagickImage Straighten(MagickImage image, double degrees, int x, int y, int width, int height)
    {
        var bands = MagickNET.Features.Contains("OpenMP", StringComparison.OrdinalIgnoreCase) ? 1 : Math.Clamp(Environment.ProcessorCount, 1, 8);
        bands = Math.Max(1, Math.Min(bands, height / 64));
        var rows = (height + bands - 1) / bands;
        var parts = new MagickImage?[bands];
        var clones = Enumerable.Range(0, bands).Select(_ => (MagickImage)image.Clone()).ToArray();
        try
        {
            Parallel.For(0, bands, i =>
            {
                var top = i * rows;
                var h = Math.Min(rows, height - top);
                if (h <= 0) return;
                var part = clones[i];
                part.BackgroundColor = MagickColors.White;
                part.VirtualPixelMethod = VirtualPixelMethod.Background;
                part.SetArtifact("distort:viewport", $"{width}x{h}{x:+0;-0;+0}{y + top:+0;-0;+0}");
                part.Distort(DistortMethod.ScaleRotateTranslate, degrees);
                part.RemoveArtifact("distort:viewport");
                part.ResetPage();
                parts[i] = part;
            });

            if (bands == 1) return Take(ref clones[0]);
            using var collection = new MagickImageCollection();
            for (var i = 0; i < bands; i++)
            {
                if (parts[i] is not null) collection.Add(Take(ref clones[i]));
            }

            var result = (MagickImage)collection.AppendVertically();
            result.ResetPage();
            return result;
        }
        finally
        {
            foreach (var c in clones) c?.Dispose();
        }
    }

    private static MagickImage Take(ref MagickImage image)
    {
        var taken = image;
        image = null!;
        return taken;
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
            // Distort with an explicit viewport gives exactly the planned bounding box at every resolution, so a
            // normalized crop maps to the same content in the preview and in the saved photo (Rotate pads unevenly).
            var (w, h) = CropMath.RotatedSize(image.Width, image.Height, recipe.Straighten);
            var vw = (int)Math.Round(w);
            var vh = (int)Math.Round(h);
            var ox = (int)Math.Round((image.Width - vw) / 2.0);
            var oy = (int)Math.Round((image.Height - vh) / 2.0);
            image.BackgroundColor = MagickColors.White;
            image.VirtualPixelMethod = VirtualPixelMethod.Background;
            image.SetArtifact("distort:viewport", $"{vw}x{vh}{ox:+0;-0;+0}{oy:+0;-0;+0}");
            image.Distort(DistortMethod.ScaleRotateTranslate, recipe.Straighten);
            image.RemoveArtifact("distort:viewport");
            image.ResetPage();
        }
    }

    /// <summary>
    /// Reference implementation of the tone adjustments with Magick.NET operations: <see cref="ApplyCurves"/> then
    /// <see cref="ApplySaturation"/>. Rendering uses <see cref="ToneProcessor"/>, which produces identical pixels faster.
    /// </summary>
    public static void ApplyTone(MagickImage image, EditRecipe r)
    {
        ApplyCurves(image, r);
        ApplySaturation(image, r);
    }

    /// <summary>
    /// Steps 1–3 (white balance, tint, exposure, levels, contrast). Each output channel depends only on the same input
    /// channel, which lets <see cref="ToneCurve"/> evaluate these operations once on a gradient.
    /// </summary>
    public static void ApplyCurves(MagickImage image, EditRecipe r)
    {
        // 1. White balance, tint and exposure as per-channel gains in linear light.
        var (gr, gg, gb) = ChannelGains(r);
        if (Math.Abs(gr - 1) > 1e-6 || Math.Abs(gg - 1) > 1e-6 || Math.Abs(gb - 1) > 1e-6)
        {
            var wasGray = !image.Channels.Contains(PixelChannel.Green);
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
    }

    /// <summary>Step 4: saturation as HCL chroma (Magick's modulate in the HCL colour space).</summary>
    public static void ApplySaturation(MagickImage image, EditRecipe r)
    {
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
