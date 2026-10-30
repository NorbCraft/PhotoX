using PhotoX.Core.Editing;
using PhotoX.Core.Geometry;

namespace PhotoX.Imaging;

/// <summary>
/// Histogram bins and neutral-tone means from 8-bit BGRA preview pixels (FR-043, FR-045). This small loop is
/// custom because Magick.NET's histogram returns a colour table, not channel bins (DEC-026).
/// </summary>
public static class ImageStatistics
{
    public static Histogram Histogram(PixelData data, PixelRect? region = null) => Histogram(data, region, null);

    /// <summary>
    /// Histogram of the tones the levels handles act on (FR-043, FR-044, prompt_04 §7): the unadjusted pixels after
    /// white balance, tint and exposure, which come before levels in the render order (ARCHITECTURE §5.2), and before
    /// levels, contrast and saturation. Drawn under the levels triangles, each handle then sits on the tones it moves,
    /// and dragging a handle no longer reshapes the histogram under it.
    /// </summary>
    public static Histogram LevelsInput(PixelData unadjusted, PixelRect? region, EditRecipe recipe)
    {
        var before = new EditRecipe { Exposure = recipe.Exposure, Temperature = recipe.Temperature, Tint = recipe.Tint };
        if (!before.HasToneAdjustments) return Histogram(unadjusted, region, null);
        var curve = ToneCurve.For(before, exact: false);
        return Histogram(unadjusted, region, (Lut8(curve.Red), Lut8(curve.Green), Lut8(curve.Blue)));
    }

    private static byte[] Lut8(ushort[] table)
    {
        var lut = new byte[256];
        for (var i = 0; i < 256; i++) lut[i] = (byte)((table[i * 257] + 128) / 257);
        return lut;
    }

    private static Histogram Histogram(PixelData data, PixelRect? region, (byte[] R, byte[] G, byte[] B)? map)
    {
        var h = new Histogram();
        var r = Clip(region, data);
        var bgra = data.Bgra;
        var (mr, mg, mb) = map ?? (Identity, Identity, Identity);
        for (var y = r.Y; y < r.Bottom; y++)
        {
            var i = (y * data.Width + r.X) * 4;
            for (var x = r.X; x < r.Right; x++, i += 4)
            {
                int b = mb[bgra[i]], g = mg[bgra[i + 1]], rr = mr[bgra[i + 2]];
                h.Blue[b]++;
                h.Green[g]++;
                h.Red[rr]++;
                h.Luma[(2126 * rr + 7152 * g + 722 * b + 5000) / 10000]++;
            }
        }

        h.Count = (long)r.Width * r.Height;
        return h;
    }

    /// <summary>Mean linear RGB of pixels that are mid-tone, low in saturation and outside the skin-hue band.</summary>
    public static NeutralMeans NeutralMeans(PixelData data, PixelRect? region = null)
    {
        var r = Clip(region, data);
        var lin = new double[256];
        for (var i = 0; i < 256; i++) lin[i] = AutoCorrect.ToLinear(i / 255.0);
        double sr = 0, sg = 0, sb = 0;
        long n = 0;
        var bgra = data.Bgra;
        for (var y = r.Y; y < r.Bottom; y++)
        {
            var i = (y * data.Width + r.X) * 4;
            for (var x = r.X; x < r.Right; x++, i += 4)
            {
                int b = bgra[i], g = bgra[i + 1], rr = bgra[i + 2];
                int max = Math.Max(rr, Math.Max(g, b)), min = Math.Min(rr, Math.Min(g, b));
                var luma = (2126 * rr + 7152 * g + 722 * b) / 10000;
                if (luma < 40 || luma > 220) continue;
                if (max - min > 0.25 * max) continue; // not neutral enough
                if (rr > g && g > b && max - min > 0.10 * max) continue; // warm skin-like hues
                sr += lin[rr];
                sg += lin[g];
                sb += lin[b];
                n++;
            }
        }

        var total = Math.Max(1L, (long)r.Width * r.Height);
        return n == 0 ? new NeutralMeans(0, 0, 0, 0) : new NeutralMeans(sr / n, sg / n, sb / n, (double)n / total);
    }

    private static readonly byte[] Identity = Enumerable.Range(0, 256).Select(i => (byte)i).ToArray();

    private static PixelRect Clip(PixelRect? region, PixelData d)
    {
        var r = region ?? new PixelRect(0, 0, d.Width, d.Height);
        var x0 = Math.Clamp(r.X, 0, d.Width);
        var y0 = Math.Clamp(r.Y, 0, d.Height);
        var x1 = Math.Clamp(r.Right, x0, d.Width);
        var y1 = Math.Clamp(r.Bottom, y0, d.Height);
        return new PixelRect(x0, y0, x1 - x0, y1 - y0);
    }
}
