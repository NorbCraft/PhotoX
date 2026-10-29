using ImageMagick;
using PhotoX.Core.Editing;

namespace PhotoX.Imaging;

/// <summary>
/// The per-channel tone curves of a recipe (<see cref="RecipeRenderer.ApplyCurves"/>) as lookup tables from 16-bit
/// input to 16-bit output (DEC-044). Magick.NET computes them by running its operations on a gradient that holds
/// every input value, so the tables reproduce those operations exactly.
/// </summary>
public sealed class ToneCurve
{
    private const int Entries = 65536;

    private ToneCurve(ushort[] red, ushort[] green, ushort[] blue, bool isIdentity)
    {
        Red = red;
        Green = green;
        Blue = blue;
        IsIdentity = isIdentity;
    }

    public ushort[] Red { get; }

    public ushort[] Green { get; }

    public ushort[] Blue { get; }

    public bool IsIdentity { get; }

    /// <summary>True when the three channels use the same table (no white balance or tint).</summary>
    public bool IsNeutral => ReferenceEquals(Red, Green) && ReferenceEquals(Green, Blue);

    /// <summary>
    /// Computes the curves. <paramref name="exact"/> evaluates all 65,536 input values (for saving); otherwise 4,096
    /// values are evaluated and interpolated, which is enough for an 8-bit preview and about ten times faster.
    /// </summary>
    public static ToneCurve For(EditRecipe recipe, bool exact = true)
    {
        var curves = recipe with { Saturation = 0 };
        if (!curves.HasToneAdjustments)
        {
            var identity = new ushort[Entries];
            for (var i = 0; i < Entries; i++) identity[i] = (ushort)i;
            return new ToneCurve(identity, identity, identity, true);
        }

        MagickSetup.EnsureInitialized();
        var side = exact ? 256 : 64;
        var n = side * side;
        var input = new ushort[n * 3];
        for (var i = 0; i < n; i++)
        {
            var v = (ushort)Math.Round(i * 65535.0 / (n - 1));
            input[3 * i] = input[3 * i + 1] = input[3 * i + 2] = v;
        }

        ushort[] output;
        using (var gradient = new MagickImage(MagickColors.Black, (uint)side, (uint)side))
        {
            gradient.ColorType = ColorType.TrueColor;
            using (var pixels = gradient.GetPixelsUnsafe())
            {
                pixels.SetArea(0, 0, (uint)side, (uint)side, input);
            }

            RecipeRenderer.ApplyCurves(gradient, curves);
            using var result = gradient.GetPixelsUnsafe();
            output = result.ToShortArray(0, 0, (uint)side, (uint)side, "RGB") ?? throw new InvalidOperationException("No curve data.");
        }

        var red = Expand(output, 0, n);
        var (gr, gg, gb) = RecipeRenderer.ChannelGains(curves);
        if (gr == gg && gg == gb) return new ToneCurve(red, red, red, false);
        return new ToneCurve(red, Expand(output, 1, n), Expand(output, 2, n), false);
    }

    /// <summary>Spreads <paramref name="n"/> evaluated points of one channel over all 65,536 inputs.</summary>
    private static ushort[] Expand(ushort[] rgb, int channel, int n)
    {
        var table = new ushort[Entries];
        if (n == Entries)
        {
            for (var i = 0; i < Entries; i++) table[i] = rgb[3 * i + channel];
            return table;
        }

        for (var i = 0; i < Entries; i++)
        {
            var pos = i * (n - 1) / 65535.0;
            var k = Math.Min((int)pos, n - 2);
            var f = pos - k;
            table[i] = (ushort)Math.Round(rgb[3 * k + channel] * (1 - f) + rgb[3 * (k + 1) + channel] * f);
        }

        return table;
    }
}
