using ImageMagick;
using PhotoX.Core.Editing;
using PhotoX.Core.Geometry;
using PhotoX.Core.Id;

namespace PhotoX.Imaging.Id;

/// <summary>ID photo adjustments (FR-064). Background brightness is a manual, mask-based lift of light neutral areas.</summary>
public sealed record IdAdjustments(double Exposure = 0, double Contrast = 0, double Temperature = 0, double Tint = 0, double BackgroundBrightness = 0)
{
    public EditRecipe ToRecipe() => new() { Exposure = Exposure, Contrast = Contrast, Temperature = Temperature, Tint = Tint };
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
        RecipeRenderer.ApplyTone(image, adjustments.ToRecipe());
        BackgroundLift.Apply(image, adjustments.BackgroundBrightness, HeadOval(size, w, h));
        return image;
    }

    /// <summary>
    /// Preview with surroundings: the frame (<paramref name="frameWidthPx"/> wide) centred in a larger viewport so the
    /// operator sees what lies outside it. <paramref name="sourceScale"/> converts framing coordinates to the proxy.
    /// </summary>
    public static MagickImage RenderPreview(MagickImage proxy, double sourceScale, IdFraming framing, IdSize size, IdAdjustments adjustments, int frameWidthPx, int viewportWidth, int viewportHeight)
    {
        var scaled = framing with
        {
            CenterX = framing.CenterX * sourceScale,
            CenterY = framing.CenterY * sourceScale,
            FrameWidthSource = framing.FrameWidthSource * sourceScale,
        };
        var image = Frame(proxy, scaled, frameWidthPx / scaled.FrameWidthSource, viewportWidth, viewportHeight, viewportWidth / 2.0, viewportHeight / 2.0);
        RecipeRenderer.ApplyTone(image, adjustments.ToRecipe());
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

    private static MagickImage Frame(MagickImage source, IdFraming framing, double scale, int width, int height, double newX, double newY)
    {
        var image = (MagickImage)source.Clone();
        try
        {
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
