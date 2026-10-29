namespace PhotoX.Core.Editing;

/// <summary>Luminance and channel histograms (256 bins, sRGB-encoded values) of an image region.</summary>
public sealed class Histogram
{
    public long[] Red { get; } = new long[256];

    public long[] Green { get; } = new long[256];

    public long[] Blue { get; } = new long[256];

    public long[] Luma { get; } = new long[256];

    public long Count { get; set; }

    /// <summary>Encoded luminance (0..1) below which <paramref name="fraction"/> of the pixels lie.</summary>
    public double Percentile(double fraction)
    {
        if (Count == 0) return 0;
        var target = fraction * Count;
        long acc = 0;
        for (var i = 0; i < 256; i++)
        {
            acc += Luma[i];
            if (acc >= target) return i / 255.0;
        }

        return 1;
    }
}

/// <summary>Mean linear RGB of mid-tone, low-saturation pixels (grey-world estimate input).</summary>
public sealed record NeutralMeans(double R, double G, double B, double Fraction);

/// <summary>
/// Conservative single-photo Auto Correct (FR-045). Produces ordinary slider values, so the result stays fully
/// adjustable and is a single undo step. No histogram equalization.
/// </summary>
public static class AutoCorrect
{
    public const double TargetMedian = 0.46;

    public static EditRecipe Apply(EditRecipe recipe, Histogram histogram, NeutralMeans? neutral)
    {
        if (histogram.Count == 0) return recipe;

        // Exposure: move the median towards the target in linear light (60 %, at most ±1 EV).
        var median = Math.Max(histogram.Percentile(0.5), 0.02);
        var ev = Math.Log2(ToLinear(TargetMedian) / ToLinear(median)) * 0.6;
        ev = Math.Round(Math.Clamp(ev, -1, 1), 2);

        // Levels from the 0.1 / 99.8 percentiles after the exposure change, capped to stay gentle.
        var gain = Math.Pow(2, ev);
        var lo = ToEncoded(ToLinear(histogram.Percentile(0.001)) * gain);
        var hi = ToEncoded(Math.Min(1, ToLinear(histogram.Percentile(0.998)) * gain));
        var black = lo > 0.01 ? Math.Min(lo, 0.06) : 0;
        var white = hi < 0.99 ? Math.Max(hi, 0.88) : 1;

        // Mild colour cast correction from neutral mid-tones (50 %, capped), skipped with too little data.
        double temperature = 0, tint = 0;
        if (neutral is { Fraction: >= 0.05 } n && n.R > 0 && n.B > 0 && n.G > 0)
        {
            var q = n.B / n.R;
            var k = (q - 1) / (q + 1);
            temperature = Math.Clamp(k / 0.30 * 100 * 0.5, -25, 25);
            var rb = (n.R + n.B) / 2;
            tint = Math.Clamp((1 - rb / n.G) / 0.20 * 100 * 0.5, -25, 25);
        }

        return recipe with
        {
            Exposure = ev,
            LevelsBlack = Math.Round(black, 3),
            LevelsWhite = Math.Round(white, 3),
            LevelsMid = 1,
            Temperature = Math.Round(temperature),
            Tint = Math.Round(tint),
        };
    }

    public static double ToLinear(double v) => v <= 0.04045 ? v / 12.92 : Math.Pow((v + 0.055) / 1.055, 2.4);

    public static double ToEncoded(double v) => v <= 0.0031308 ? v * 12.92 : 1.055 * Math.Pow(v, 1 / 2.4) - 0.055;
}
