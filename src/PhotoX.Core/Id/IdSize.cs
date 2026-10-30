using System.Text.Json.Serialization;

namespace PhotoX.Core.Id;

/// <summary>
/// A geometric ID photo size (FR-061). Not a statement of compliance with any official document.
/// Guide fractions are relative to the photo height: crown position from the top and head height (crown to chin).
/// <paramref name="Guides"/> is false for sizes framed by hand without face guides or Auto Align (9×12, prompt_04 §8).
/// <paramref name="SheetMarginMm"/> and <paramref name="SheetSpacingMm"/> override the sheet defaults (3 mm, 2 mm).
/// </summary>
public sealed record IdSize(
    string Id,
    string Label,
    double WidthMm,
    double HeightMm,
    double StripBelowMm,
    int[] FamiliarCounts,
    double CrownFraction,
    double HeadFraction,
    string? Approximation = null,
    bool Guides = true,
    double? SheetMarginMm = null,
    double? SheetSpacingMm = null)
{
    public const double Ppi = 300;

    /// <summary>Sizes added by the operator (+ New size); they can be removed again.</summary>
    [JsonIgnore]
    public bool IsCustom => Id.StartsWith(IdSizes.CustomPrefix, StringComparison.Ordinal);

    [JsonIgnore]
    public int WidthPx => Units.MmToPx(WidthMm, Ppi);

    [JsonIgnore]
    public int HeightPx => Units.MmToPx(HeightMm, Ppi);

    /// <summary>Height of one sheet cell: photo plus the white strip below it (3×4: 40 + 7 mm, FR-074).</summary>
    [JsonIgnore]
    public double CellHeightMm => HeightMm + StripBelowMm;

    [JsonIgnore]
    public double Ratio => WidthMm / HeightMm;

    /// <summary>Eye line, about 48 % down the head from the crown.</summary>
    [JsonIgnore]
    public double EyeFraction => CrownFraction + HeadFraction * 0.48;

    [JsonIgnore]
    public double ChinFraction => CrownFraction + HeadFraction;
}

public static class IdSizes
{
    public const string CustomPrefix = "custom";

    /// <summary>Romanian 3×4: crown line at 10 %, head 70 % of the height (prompt_04 §8: slightly smaller than 75 %).</summary>
    public static IdSize S3x4 { get; } = new("3x4", "3 × 4 cm", 30, 40, 7, [4, 6, 8], 0.10, 0.70);

    public static IdSize S35x45 { get; } = new("35x45", "3.5 × 4.5 cm", 35, 45, 0, [2, 4, 6], 0.10, 0.75);

    /// <summary>
    /// US visa 2×2 in. Two photos side by side are exactly 101.6 mm, the short edge of the 4×6 in page, so four fit
    /// only edge to edge: the sheet defaults have no margin and no spacing (prompt_04 §8, DEC-052).
    /// </summary>
    public static IdSize S2x2in { get; } = new("2x2in", "2 × 2 in", 50.8, 50.8, 0, [1, 2, 4], 0.12, 0.60, "≈ 5.1 × 5.1 cm", SheetMarginMm: 0, SheetSpacingMm: 0);

    public static IdSize S4x6cm { get; } = new("4x6cm", "4 × 6 cm", 40, 60, 0, [2, 4], 0.12, 0.55);

    public static IdSize S5x7cm { get; } = new("5x7cm", "5 × 7 cm", 50, 70, 0, [1, 2], 0.12, 0.55);

    /// <summary>Police photo: framed by hand, without face guides (prompt_04 §8).</summary>
    public static IdSize S9x12 { get; } = new("9x12", "9 × 12 cm", 90, 120, 0, [1], 0.12, 0.55, Guides: false);

    /// <summary>The planned sizes (FR-061), as written to a new configuration file.</summary>
    public static IReadOnlyList<IdSize> Defaults { get; } = [S3x4, S35x45, S2x2in, S4x6cm, S5x7cm, S9x12];

    /// <summary>
    /// Defaults written by earlier versions. A configuration entry still equal to one of them was never edited, so it
    /// is replaced by the current default; edited entries are kept.
    /// </summary>
    public static IReadOnlyList<IdSize> Superseded { get; } =
    [
        new("3x4", "3 × 4 cm", 30, 40, 7, [4, 6, 8], 0.10, 0.75),
        new("2x2in", "2 × 2 in", 50.8, 50.8, 0, [1, 2], 0.12, 0.60, "≈ 5.1 × 5.1 cm"),
        new("9x12", "9 × 12 cm", 90, 120, 0, [1], 0.12, 0.55),
    ];

    /// <summary>Replaces unedited entries of earlier defaults (<see cref="Superseded"/>); returns the list and whether anything changed.</summary>
    public static (IReadOnlyList<IdSize> Sizes, bool Changed) UpgradeDefaults(IReadOnlyList<IdSize> sizes)
    {
        var changed = false;
        var result = sizes.Select(s =>
        {
            if (s is null || !Superseded.Any(old => SameValues(old, s))) return s!;
            var current = Defaults.FirstOrDefault(d => d.Id == s.Id);
            if (current is null) return s;
            changed = true;
            return current;
        }).ToList();
        return (result, changed);
    }

    /// <summary>Value equality including the copy counts (records compare arrays by reference).</summary>
    public static bool SameValues(IdSize a, IdSize b) =>
        a with { FamiliarCounts = [] } == b with { FamiliarCounts = [] } && a.FamiliarCounts.SequenceEqual(b.FamiliarCounts);

    /// <summary>The sizes offered in the ID Photo Creator: the configuration data, or the defaults.</summary>
    public static IReadOnlyList<IdSize> BuiltIn { get; private set; } = Defaults;

    /// <summary>
    /// Uses the configured sizes (FR-061: sizes are data). Implausible entries are skipped; if none remain, the
    /// defaults stay. Returns the number of sizes in use. Called once at startup.
    /// </summary>
    public static int Configure(IEnumerable<IdSize> sizes)
    {
        var valid = sizes.Where(IsPlausible).GroupBy(s => s.Id).Select(g => g.First()).ToList();
        if (valid.Count > 0) BuiltIn = valid;
        return BuiltIn.Count;
    }

    private static bool IsPlausible(IdSize s) =>
        s is not null && !string.IsNullOrWhiteSpace(s.Id) && !s.IsCustom && !string.IsNullOrWhiteSpace(s.Label)
        && s.WidthMm is >= 10 and <= 140 && s.HeightMm is >= 10 and <= 140 && s.StripBelowMm is >= 0 and <= 20
        && s.CrownFraction is > 0 and < 0.5 && s.HeadFraction is > 0.2 and < 0.95 && s.CrownFraction + s.HeadFraction < 1
        && s.FamiliarCounts is { Length: > 0 } && s.FamiliarCounts.All(c => c > 0)
        && s.SheetMarginMm is null or (>= 0 and <= 15) && s.SheetSpacingMm is null or (>= 0 and <= 10);

    /// <summary>An operator-defined size (+ New size), labelled in centimetres (prompt_04 §8).</summary>
    public static IdSize Custom(double widthMm, double heightMm)
    {
        var inv = System.Globalization.CultureInfo.InvariantCulture;
        var id = string.Format(inv, "{0}-{1:0.#}x{2:0.#}", CustomPrefix, widthMm, heightMm);
        var label = string.Format(inv, "{0:0.##} × {1:0.##} cm", widthMm / 10, heightMm / 10);
        return new IdSize(id, label, widthMm, heightMm, 0, [1, 2, 4], 0.10, 0.70);
    }

    /// <summary>Plausible dimensions for a custom size: at least 1 cm, and one copy fits on the 4×6 in page.</summary>
    public static bool IsValidCustom(double widthMm, double heightMm) =>
        Math.Min(widthMm, heightMm) >= 10 && Math.Min(widthMm, heightMm) <= SheetOptions.PageShortMm && Math.Max(widthMm, heightMm) <= SheetOptions.PageLongMm;
}

public static class Units
{
    public static int MmToPx(double mm, double ppi) => (int)Math.Round(mm * ppi / 25.4, MidpointRounding.AwayFromZero);
}
