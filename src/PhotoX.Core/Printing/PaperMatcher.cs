namespace PhotoX.Core.Printing;

/// <summary>A paper as the printer driver reports it: its name and size in millimetres (either orientation).</summary>
public sealed record DriverPaper(string Name, double WidthMm, double HeightMm);

/// <summary>
/// Chooses the driver's paper for a print size (prompt_07 §3.3, prompt_11 §1). Drivers name the same paper differently
/// ("4x6", "(6x4)", "10x15cm", "North America 4 x 6"), so only the reported dimensions count. Photo printers report the
/// page they print borderless, with bleed: the Citizen CY-02's (6x4) is 156.1 × 105.0 mm (1844 × 1240 dots at 300 dpi)
/// for a 6 × 4 in print. A paper matches when each side is at most <see cref="ToleranceMm"/> smaller or
/// <see cref="MaxBleedMm"/> larger than the print size, in either orientation. Among matches, plain papers come
/// first, then the "PR" papers (the same paper turned, which Citizen offers only for applications that cannot set the
/// orientation), then two-up or split variants, each by the closest size. No match means no paper is chosen, so a
/// wrong size is never used silently.
/// </summary>
public static partial class PaperMatcher
{
    /// <summary>Drivers round inch papers to whole millimetres (4×6 in = 101.6 × 152.4 mm, often 102 × 152).</summary>
    public const double ToleranceMm = 2.5;

    /// <summary>Bleed a borderless driver page may add to a side (Citizen CY-02: 3.4 mm and 3.7 mm).</summary>
    public const double MaxBleedMm = 6;

    /// <summary>A paper side of <paramref name="paperMm"/> belongs to a print side of <paramref name="printMm"/>.</summary>
    public static bool Fits(double paperMm, double printMm) => paperMm >= printMm - ToleranceMm && paperMm <= printMm + MaxBleedMm;

    public static int? Best(IReadOnlyList<DriverPaper> papers, double widthMm, double heightMm)
    {
        var (w, h) = (Math.Min(widthMm, heightMm), Math.Max(widthMm, heightMm));
        int? best = null;
        (int Variant, double Distance) bestKey = (int.MaxValue, double.MaxValue);
        for (var i = 0; i < papers.Count; i++)
        {
            var p = papers[i];
            var (pw, ph) = (Math.Min(p.WidthMm, p.HeightMm), Math.Max(p.WidthMm, p.HeightMm));
            if (!Fits(pw, w) || !Fits(ph, h)) continue;
            var key = (IsVariant(p.Name) ? 2 : IsTurned(p.Name) ? 1 : 0, Math.Abs(pw - w) + Math.Abs(ph - h));
            if (key.CompareTo(bestKey) < 0)
            {
                (best, bestKey) = (i, key);
            }
        }

        return best;
    }

    /// <summary>Papers that put two prints on one sheet or cut it (the Citizen's "(6x4) x 2" for two 10×15 on 6×8 media).</summary>
    public static bool IsVariant(string name) => VariantName().IsMatch(name);

    /// <summary>The Citizen's "PR" papers ("PR (4x6)"): the same paper as "(6x4)", turned.</summary>
    public static bool IsTurned(string name) => TurnedName().IsMatch(name);

    // "2up", "2-up", "2 up", "2in1", "split", or a trailing "x2" / "× 2" — but not the "x2" inside "152x203".
    [System.Text.RegularExpressions.GeneratedRegex(@"2\s*-?\s*up|2\s*in\s*1|split|\bx\s*2\b|×\s*2\b", System.Text.RegularExpressions.RegexOptions.IgnoreCase)]
    private static partial System.Text.RegularExpressions.Regex VariantName();

    [System.Text.RegularExpressions.GeneratedRegex(@"^\s*PR\b", System.Text.RegularExpressions.RegexOptions.IgnoreCase)]
    private static partial System.Text.RegularExpressions.Regex TurnedName();
}
