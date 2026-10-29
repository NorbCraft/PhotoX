using ImageMagick;
using ImageMagick.Drawing;
using PhotoX.Core.Geometry;

namespace PhotoX.Imaging.Id;

/// <summary>
/// Manual background brightening (FR-064): lifts light, low-saturation pixels towards white through a soft mask,
/// excluding an enlarged head oval. Uses Magick.NET operations only (DEC-026). Not a cut-out.
/// </summary>
public static class BackgroundLift
{
    public static void Apply(MagickImage image, double amount, RectD headOval)
    {
        if (amount <= 0.5) return;
        using var mask = BuildMask(image, headOval);
        using var bright = (MagickImage)image.Clone();
        var white = Math.Clamp(100 - amount * 0.45, 40, 100); // amount 100 → light greys become white
        bright.Level(new Percentage(0), new Percentage(white));
        bright.Alpha(AlphaOption.Set);
        bright.Composite(mask, CompositeOperator.CopyAlpha);
        image.Composite(bright, CompositeOperator.Over);
    }

    public static MagickImage BuildMask(MagickImage image, RectD headOval)
    {
        using var hsl = (MagickImage)image.Clone();
        hsl.Alpha(AlphaOption.Off);
        hsl.ColorSpace = ColorSpace.HSL;
        var list = hsl.Separate(Channels.RGB);
        var saturation = (MagickImage)list[1].Clone();
        var mask = (MagickImage)list[2].Clone(); // lightness
        foreach (var c in list) c.Dispose();
        mask.ColorSpace = ColorSpace.Gray;
        saturation.ColorSpace = ColorSpace.Gray;
        mask.Level(new Percentage(45), new Percentage(75));       // light areas → 1
        saturation.Level(new Percentage(8), new Percentage(35));
        saturation.Negate(Channels.All);                          // low saturation → 1
        mask.Composite(saturation, CompositeOperator.Multiply);
        saturation.Dispose();

        // Protect the face and hair: enlarged head oval, softly feathered.
        var pad = headOval.Width * 0.18;
        new Drawables()
            .FillColor(MagickColors.Black)
            .StrokeColor(MagickColors.Black)
            .Ellipse(headOval.CenterX, headOval.CenterY + pad * 0.6, headOval.Width / 2 + pad, headOval.Height / 2 + pad * 1.2, 0, 360)
            .Draw(mask);
        mask.Blur(0, Math.Max(1.5, image.Width * 0.012));
        mask.Alpha(AlphaOption.Off); // CopyAlpha must use the grey values, not an opaque alpha channel
        return mask;
    }
}
