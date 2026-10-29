using System.Text.Json.Serialization;

namespace PhotoX.Core.Id;

/// <summary>
/// A geometric ID photo size (FR-061). Not a statement of compliance with any official document.
/// Guide fractions are relative to the photo height: crown position from the top and head height (crown to chin).
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
    string? Approximation = null)
{
    public const double Ppi = 300;

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
    public static IdSize S3x4 { get; } = new("3x4", "3 × 4 cm", 30, 40, 7, [4, 6, 8], 0.10, 0.75);

    public static IdSize S35x45 { get; } = new("35x45", "3.5 × 4.5 cm", 35, 45, 0, [2, 4, 6], 0.10, 0.75);

    public static IdSize S2x2in { get; } = new("2x2in", "2 × 2 in", 50.8, 50.8, 0, [1, 2], 0.12, 0.60, "≈ 5.1 × 5.1 cm");

    public static IdSize S4x6cm { get; } = new("4x6cm", "4 × 6 cm", 40, 60, 0, [2, 4], 0.12, 0.55);

    public static IdSize S5x7cm { get; } = new("5x7cm", "5 × 7 cm", 50, 70, 0, [1, 2], 0.12, 0.55);

    public static IdSize S9x12 { get; } = new("9x12", "9 × 12 cm", 90, 120, 0, [1], 0.12, 0.55);

    /// <summary>The planned sizes (FR-061), as written to a new configuration file.</summary>
    public static IReadOnlyList<IdSize> Defaults { get; } = [S3x4, S35x45, S2x2in, S4x6cm, S5x7cm, S9x12];

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
        !string.IsNullOrWhiteSpace(s.Id) && s.Id != "custom" && !string.IsNullOrWhiteSpace(s.Label)
        && s.WidthMm is >= 10 and <= 140 && s.HeightMm is >= 10 and <= 140 && s.StripBelowMm is >= 0 and <= 20
        && s.CrownFraction is > 0 and < 0.5 && s.HeadFraction is > 0.2 and < 0.95 && s.CrownFraction + s.HeadFraction < 1
        && s.FamiliarCounts is { Length: > 0 } && s.FamiliarCounts.All(c => c > 0);

    public static IdSize Custom(double widthMm, double heightMm) =>
        new("custom", $"{widthMm:0.#} × {heightMm:0.#} mm", widthMm, heightMm, 0, [1, 2, 4], 0.10, 0.70);
}

public static class Units
{
    public static int MmToPx(double mm, double ppi) => (int)Math.Round(mm * ppi / 25.4, MidpointRounding.AwayFromZero);
}
