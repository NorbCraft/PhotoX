using ImageMagick;
using PhotoX.Core.Editing;

namespace PhotoX.Imaging;

/// <summary>
/// Applies a recipe's tone adjustments in one parallel pass (DEC-044): the <see cref="ToneCurve"/> tables computed by
/// Magick.NET, then HCL saturation exactly as Magick's modulate does it. Produces the pixels of
/// <see cref="RecipeRenderer.ApplyTone"/> (verified by tests) in a fraction of the time, because the bundled
/// Magick.NET native library runs its operations on a single thread.
/// </summary>
public static class ToneProcessor
{
    private const double Epsilon = 1e-12;
    private const double LumaR = 0.298839;
    private const double LumaG = 0.586811;
    private const double LumaB = 0.114350;

    /// <summary>Applies the tone adjustments to <paramref name="image"/> in place.</summary>
    public static void Apply(MagickImage image, EditRecipe recipe, bool exact = true)
    {
        if (!recipe.HasToneAdjustments) return;
        var curve = ToneCurve.For(recipe, exact);
        var saturate = Math.Abs(recipe.Saturation) > 1e-6;
        var factor = Math.Max(0, 1 + recipe.Saturation / 100);
        var gray = !image.Channels.Contains(PixelChannel.Green);
        if (gray && !curve.IsNeutral)
        {
            // Colour gains need three channels; converting grey to sRGB copies the grey value into each.
            image.ColorType = image.HasAlpha ? ColorType.TrueColorAlpha : ColorType.TrueColor;
            image.ColorSpace = ColorSpace.sRGB;
            gray = false;
        }

        if (curve.IsIdentity && (gray || !saturate)) return;

        OwnPixels(image);
        using var pixels = image.GetPixelsUnsafe();
        var channels = (int)pixels.Channels;
        int Index(PixelChannel channel) => pixels.GetChannelIndex(channel) is uint i ? (int)i : throw new InvalidOperationException($"No {channel} channel.");
        var ri = Index(PixelChannel.Red);
        var gi = gray ? ri : Index(PixelChannel.Green);
        var bi = gray ? ri : Index(PixelChannel.Blue);
        var width = (int)image.Width;
        var height = (int)image.Height;
        var stripRows = Math.Max(1, 8_000_000 / (width * channels));
        for (var y0 = 0; y0 < height; y0 += stripRows)
        {
            var rows = Math.Min(stripRows, height - y0);
            var data = pixels.GetArea(0, y0, (uint)width, (uint)rows) ?? throw new InvalidOperationException("No pixel data.");
            Parallel.For(0, rows, y =>
            {
                var end = (y + 1) * width * channels;
                for (var i = y * width * channels; i < end; i += channels)
                {
                    if (gray)
                    {
                        data[i + ri] = curve.Red[data[i + ri]];
                        continue;
                    }

                    double r = curve.Red[data[i + ri]], g = curve.Green[data[i + gi]], b = curve.Blue[data[i + bi]];
                    if (saturate) Saturate(ref r, ref g, ref b, factor);
                    data[i + ri] = ToQuantum(r);
                    data[i + gi] = ToQuantum(g);
                    data[i + bi] = ToQuantum(b);
                }
            });
            pixels.SetArea(0, y0, (uint)width, (uint)rows, data);
        }
    }

    /// <summary>
    /// Gives <paramref name="image"/> its own copy of its pixels before they are written in strips (prompt_07 §1.6).
    /// A clone shares its pixels with the original until the first write. The strips are written with queued
    /// (write-only) access, and for that ImageMagick gives a shared image new pixel memory without copying the
    /// pixels, so every strip after the first was read as zeros and saved black: a photo saved without a crop (the
    /// full frame) but with a tone change came out black below its first strip, about 1/8 of a 22 MP photo.
    /// Compositing a fully transparent pixel changes nothing, but writes through ordinary access, which copies first.
    /// </summary>
    internal static void OwnPixels(MagickImage image)
    {
        using var dot = new MagickImage(MagickColors.Transparent, 1, 1);
        image.Composite(dot, 0, 0, CompositeOperator.Over);
    }

    /// <summary>
    /// Renders preview pixels: 8-bit BGRA <paramref name="source"/> to 8-bit BGRA <paramref name="target"/> (alpha copied).
    /// </summary>
    public static void ApplyToPreview(byte[] source, byte[] target, ToneCurve curve, double saturation)
    {
        var saturate = Math.Abs(saturation) > 1e-6;
        var factor = Math.Max(0, 1 + saturation / 100);
        var r16 = new ushort[256];
        var g16 = new ushort[256];
        var b16 = new ushort[256];
        var r8 = new byte[256];
        var g8 = new byte[256];
        var b8 = new byte[256];
        for (var v = 0; v < 256; v++)
        {
            r16[v] = curve.Red[v * 257];
            g16[v] = curve.Green[v * 257];
            b16[v] = curve.Blue[v * 257];
            r8[v] = ToByte(r16[v]);
            g8[v] = ToByte(g16[v]);
            b8[v] = ToByte(b16[v]);
        }

        var count = source.Length / 4;
        const int chunk = 16384;
        Parallel.For(0, (count + chunk - 1) / chunk, c =>
        {
            var end = Math.Min(count, (c + 1) * chunk) * 4;
            for (var i = c * chunk * 4; i < end; i += 4)
            {
                if (!saturate)
                {
                    target[i] = b8[source[i]];
                    target[i + 1] = g8[source[i + 1]];
                    target[i + 2] = r8[source[i + 2]];
                }
                else
                {
                    double r = r16[source[i + 2]], g = g16[source[i + 1]], b = b16[source[i]];
                    Saturate(ref r, ref g, ref b, factor);
                    target[i] = ToByte(ToQuantum(b));
                    target[i + 1] = ToByte(ToQuantum(g));
                    target[i + 2] = ToByte(ToQuantum(r));
                }

                target[i + 3] = source[i + 3];
            }
        });
    }

    /// <summary>
    /// Scales the HCL chroma of a pixel (values in quantum units), as ImageMagick's ModulateImage does with
    /// <c>modulate:colorspace=HCL</c> (ConvertRGBToHCL, chroma × factor, ConvertHCLToRGB; hue and luma unchanged).
    /// </summary>
    internal static void Saturate(ref double r, ref double g, ref double b, double factor)
    {
        var max = Math.Max(r, Math.Max(g, b));
        var c = max - Math.Min(r, Math.Min(g, b));
        double h;
        if (Math.Abs(c) < Epsilon) h = 0;
        else if (Math.Abs(r - max) < Epsilon) h = ((g - b) / c + 6.0) % 6.0;
        else if (Math.Abs(g - max) < Epsilon) h = (b - r) / c + 2.0;
        else h = (r - g) / c + 4.0;

        var luma = LumaR * r + LumaG * g + LumaB * b;
        var chroma = c * factor;
        var x = chroma * (1.0 - Math.Abs(h % 2.0 - 1.0));
        double rr = 0, gg = 0, bb = 0;
        if (h < 1) (rr, gg) = (chroma, x);
        else if (h < 2) (rr, gg) = (x, chroma);
        else if (h < 3) (gg, bb) = (chroma, x);
        else if (h < 4) (gg, bb) = (x, chroma);
        else if (h < 5) (rr, bb) = (x, chroma);
        else if (h < 6) (rr, bb) = (chroma, x);
        var m = luma - (LumaR * rr + LumaG * gg + LumaB * bb);
        r = rr + m;
        g = gg + m;
        b = bb + m;
    }

    private static ushort ToQuantum(double v) => v <= 0 ? (ushort)0 : v >= 65535 ? (ushort)65535 : (ushort)(v + 0.5);

    /// <summary>ImageMagick's ScaleQuantumToChar for 16-bit quanta.</summary>
    private static byte ToByte(ushort q) => (byte)((q + 128 - ((q + 128) >> 8)) >> 8);
}
