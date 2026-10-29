using PhotoX.Core.Editing;
using PhotoX.Core.Geometry;

namespace PhotoX.Imaging;

/// <summary>
/// Histogram bins and neutral-tone means from 8-bit BGRA preview pixels (FR-043, FR-045). This small loop is
/// custom because Magick.NET's histogram returns a colour table, not channel bins (DEC-026).
/// </summary>
public static class ImageStatistics
{
    public static Histogram Histogram(PixelData data, PixelRect? region = null)
    {
        var h = new Histogram();
        var r = Clip(region, data);
        var bgra = data.Bgra;
        for (var y = r.Y; y < r.Bottom; y++)
        {
            var i = (y * data.Width + r.X) * 4;
            for (var x = r.X; x < r.Right; x++, i += 4)
            {
                int b = bgra[i], g = bgra[i + 1], rr = bgra[i + 2];
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
