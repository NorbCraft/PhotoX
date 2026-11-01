namespace PhotoX.Core.Printing;

/// <summary>A paper as the printer driver reports it: its name and size in millimetres (either orientation).</summary>
public sealed record DriverPaper(string Name, double WidthMm, double HeightMm);

/// <summary>
/// Chooses the driver's paper for a print size (prompt_07 §3.3). Drivers name the same paper differently ("4x6",
/// "(6x4)", "10x15cm", "North America 4 x 6"), so only the reported dimensions count: a paper matches when both sides
/// are within <see cref="ToleranceMm"/> of the print size in either orientation. Among matches, plain papers come
/// before two-up or split variants of the same size, then the closest size. No match means no paper is chosen, so a
/// wrong size is never used silently.
/// </summary>
public static partial class PaperMatcher
{
    /// <summary>Drivers round inch papers to whole millimetres (4×6 in = 101.6 × 152.4 mm, often 102 × 152).</summary>
    public const double ToleranceMm = 2.5;

    public static int? Best(IReadOnlyList<DriverPaper> papers, double widthMm, double heightMm)
    {
        var (w, h) = (Math.Min(widthMm, heightMm), Math.Max(widthMm, heightMm));
        int? best = null;
        (int Variant, double Distance) bestKey = (int.MaxValue, double.MaxValue);
        for (var i = 0; i < papers.Count; i++)
        {
            var p = papers[i];
            var (pw, ph) = (Math.Min(p.WidthMm, p.HeightMm), Math.Max(p.WidthMm, p.HeightMm));
            var (dw, dh) = (Math.Abs(pw - w), Math.Abs(ph - h));
            if (dw > ToleranceMm || dh > ToleranceMm) continue;
            var key = (IsVariant(p.Name) ? 1 : 0, dw + dh);
            if (key.CompareTo(bestKey) < 0)
            {
                (best, bestKey) = (i, key);
            }
        }

        return best;
    }

    /// <summary>Papers that put two prints on one sheet or cut it (the Citizen's "(6x8) 2-up" for two 10×15).</summary>
    public static bool IsVariant(string name) => VariantName().IsMatch(name);

    // "2up", "2-up", "2 up", "2in1", "split", or a trailing "x2" — but not the "x2" inside "152x203".
    [System.Text.RegularExpressions.GeneratedRegex(@"2\s*-?\s*up|2\s*in\s*1|split|\bx\s*2\b", System.Text.RegularExpressions.RegexOptions.IgnoreCase)]
    private static partial System.Text.RegularExpressions.Regex VariantName();
}
