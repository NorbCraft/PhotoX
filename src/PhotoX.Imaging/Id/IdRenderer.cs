using ImageMagick;
using PhotoX.Core.Editing;
using PhotoX.Core.Geometry;
using PhotoX.Core.Id;

namespace PhotoX.Imaging.Id;

/// <summary>
/// ID photo adjustments (FR-064): the tone adjustments of the Photos editor (only the tone fields of
/// <paramref name="Tone"/> are used). The background brightness lift was removed in prompt_07 §2.1.
/// </summary>
public sealed record IdAdjustments(EditRecipe Tone)
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
        var image = FramePreview(proxy, sourceScale, framing, frameWidthPx, viewportWidth, viewportHeight);
        AdjustPreview(image, size, adjustments, frameWidthPx);
        return image;
    }

    /// <summary>The geometry step of <see cref="RenderPreview"/>: the framed viewport without adjustments.</summary>
    public static MagickImage FramePreview(MagickImage proxy, double sourceScale, IdFraming framing, int frameWidthPx, int viewportWidth, int viewportHeight)
    {
        var scaled = framing with
        {
            CenterX = framing.CenterX * sourceScale,
            CenterY = framing.CenterY * sourceScale,
            FrameWidthSource = framing.FrameWidthSource * sourceScale,
        };
        return Frame(proxy, scaled, frameWidthPx / scaled.FrameWidthSource, viewportWidth, viewportHeight, viewportWidth / 2.0, viewportHeight / 2.0, fast: true);
    }

    /// <summary>The adjustment step of <see cref="RenderPreview"/>, applied in place to a framed viewport.</summary>
    public static void AdjustPreview(MagickImage viewport, IdSize size, IdAdjustments adjustments, int frameWidthPx) =>
        ToneProcessor.Apply(viewport, adjustments.ToRecipe(), exact: false);

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
            // Rotating by −Angle makes the (tilted or turned) frame upright.
            image.Distort(DistortMethod.ScaleRotateTranslate, framing.CenterX, framing.CenterY, scale, -framing.Angle, newX, newY);
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
