using ImageMagick;
using PhotoX.Core.Editing;
using PhotoX.Core.Geometry;
using PhotoX.Core.Id;

namespace PhotoX.Imaging.Id;

/// <summary>
/// ID photo adjustments (FR-064): the tone adjustments of the Photos editor (only the tone fields of
/// <paramref name="Tone"/> are used) plus a manual, mask-based lift of light neutral background areas.
/// </summary>
public sealed record IdAdjustments(EditRecipe Tone, double BackgroundBrightness = 0)
{
    public IdAdjustments()
        : this(new EditRecipe())
    {
    }

    public EditRecipe ToRecipe() => new()
    {
        Exposure = Tone.Exposure, Contrast = Tone.Contrast, Saturation = Tone.Saturation, Temperature = Tone.Temperature, Tint = Tone.Tint,
        LevelsBlack = Tone.LevelsBlack, LevelsMid = Tone.LevelsMid, LevelsWhite = Tone.LevelsWhite,
    };
}

/// <summary>
/// Renders the framed ID photo (FR-062, FR-068) with a single Distort (scale, rotate, translate) from the
/// full-resolution portrait into the exact output size, then applies the adjustments. The portrait is never modified.
/// </summary>
public static class IdRenderer
{
    /// <summary>Exact output photo at 300 ppi (or the given size).</summary>
    public static MagickImage Render(MagickImage source, IdFraming framing, IdSize size, IdAdjustments adjustments, int? widthPx = null, int? heightPx = null)
    {
        var w = widthPx ?? size.WidthPx;
        var h = heightPx ?? size.HeightPx;
        var image = Frame(source, framing, w / framing.FrameWidthSource, w, h, w / 2.0, h / 2.0);
        ToneProcessor.Apply(image, adjustments.ToRecipe());
        BackgroundLift.Apply(image, adjustments.BackgroundBrightness, HeadOval(size, w, h));
        return image;
    }

    /// <summary>
    /// Preview with surroundings: the frame (<paramref name="frameWidthPx"/> wide) centred in a larger viewport so the
    /// operator sees what lies outside it. <paramref name="sourceScale"/> converts framing coordinates to the proxy.
    /// The proxy should be close to the preview scale: the preview uses fast bilinear lookups instead of the slower
    /// resampling filter of the exported photo.
    /// </summary>
    public static MagickImage RenderPreview(MagickImage proxy, double sourceScale, IdFraming framing, IdSize size, IdAdjustments adjustments, int frameWidthPx, int viewportWidth, int viewportHeight)
    {
        var scaled = framing with
        {
            CenterX = framing.CenterX * sourceScale,
            CenterY = framing.CenterY * sourceScale,
            FrameWidthSource = framing.FrameWidthSource * sourceScale,
        };
        var image = Frame(proxy, scaled, frameWidthPx / scaled.FrameWidthSource, viewportWidth, viewportHeight, viewportWidth / 2.0, viewportHeight / 2.0, fast: true);
        ToneProcessor.Apply(image, adjustments.ToRecipe(), exact: false);
        var frameH = frameWidthPx / size.Ratio;
        var oval = HeadOval(size, frameWidthPx, (int)Math.Round(frameH)).Offset((viewportWidth - frameWidthPx) / 2.0, (viewportHeight - frameH) / 2.0);
        BackgroundLift.Apply(image, adjustments.BackgroundBrightness, oval);
        return image;
    }

    /// <summary>Head oval in output pixels from the size's guides.</summary>
    public static RectD HeadOval(IdSize size, int width, int height)
    {
        var top = size.CrownFraction * height;
        var head = size.HeadFraction * height;
        var ovalW = head * 0.74;
        return new RectD((width - ovalW) / 2, top, ovalW, head);
    }

    /// <summary>
    /// Picks the image of <paramref name="levels"/> (each half the size of the previous) whose scale to the preview is
    /// at most 1.4, so a fast preview never shrinks much (no aliasing) and never processes needless pixels.
    /// Returns the image and its scale relative to <paramref name="levels"/>[0].
    /// </summary>
    public static (MagickImage Image, double Scale) PreviewLevel(IReadOnlyList<MagickImage> levels, double previewScaleOfFirst)
    {
        var i = 0;
        var factor = 1.0;
        while (i + 1 < levels.Count && previewScaleOfFirst / (factor / 2) <= 1.4)
        {
            i++;
            factor /= 2;
        }

        return (levels[i], factor);
    }

    private static MagickImage Frame(MagickImage source, IdFraming framing, double scale, int width, int height, double newX, double newY, bool fast = false)
    {
        var image = (MagickImage)source.Clone();
        try
        {
            if (fast)
            {
                // A point filter makes Distort use direct interpolated lookups instead of area resampling.
                image.FilterType = FilterType.Point;
                image.Interpolate = PixelInterpolateMethod.Bilinear;
            }

            image.BackgroundColor = MagickColors.White;
            image.VirtualPixelMethod = VirtualPixelMethod.Background;
            image.SetArtifact("distort:viewport", $"{width}x{height}+0+0");
            // Rotating by −RotationDeg makes the (tilted) frame upright.
            image.Distort(DistortMethod.ScaleRotateTranslate, framing.CenterX, framing.CenterY, scale, -framing.RotationDeg, newX, newY);
            image.RemoveArtifact("distort:viewport");
            image.ResetPage();
            return image;
        }
        catch
        {
            image.Dispose();
            throw;
        }
    }
}
