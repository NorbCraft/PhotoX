using System.Text.Json.Serialization;

namespace PhotoX.Core.Formats;

/// <summary>
/// An ordinary print format (FR-010, FR-011). Dimensions are for portrait orientation in millimetres.
/// When <see cref="CanvasWidthMm"/> is set, the photo is centred on a larger white canvas (13×18 on 15×20, FR-073).
/// </summary>
public sealed record PrintFormat(
    string Id,
    string Name,
    double WidthMm,
    double HeightMm,
    double? CanvasWidthMm = null,
    double? CanvasHeightMm = null)
{
    /// <summary>Portrait crop ratio (width / height).</summary>
    [JsonIgnore]
    public double PortraitRatio => WidthMm / HeightMm;

    [JsonIgnore]
    public bool HasCanvas => CanvasWidthMm is not null && CanvasHeightMm is not null;

    /// <summary>Physical long edge of what is printed (the canvas when present) in inches.</summary>
    [JsonIgnore]
    public double PrintedLongEdgeInches => Math.Max(CanvasWidthMm ?? WidthMm, CanvasHeightMm ?? HeightMm) / 25.4;
}

/// <summary>
/// The ordinary print formats (plan §8.1). The three presets are fixed; their physical dimensions come from the
/// configuration data file when present (FR-010, FR-011, FR-040), so they can be corrected after print tests.
/// </summary>
public static class PrintFormats
{
    public const string DefaultId = "10x15";
    public const string CustomId = "custom";

    public static PrintFormat P10x15 { get; private set; } = new("10x15", "10 × 15", 101.6, 152.4);

    public static PrintFormat P15x20 { get; private set; } = new("15x20", "15 × 20", 152.4, 203.2);

    public static PrintFormat P13x18 { get; private set; } = new("13x18", "13 × 18", 130, 180, 152.4, 203.2);

    /// <summary>The researched defaults (OQ-001), as written to a new configuration file.</summary>
    public static IReadOnlyList<PrintFormat> Defaults { get; } = [P10x15, P15x20, P13x18];

    public static IReadOnlyList<PrintFormat> BuiltIn => [P10x15, P15x20, P13x18];

    public static PrintFormat? Find(string id) => BuiltIn.FirstOrDefault(f => f.Id == id);

    /// <summary>
    /// Applies configured dimensions to the presets with the same id. Unknown ids and implausible sizes are ignored;
    /// returns the ids that were applied. Called once at startup.
    /// </summary>
    public static IReadOnlyList<string> Configure(IEnumerable<PrintFormat> formats)
    {
        var applied = new List<string>();
        foreach (var f in formats)
        {
            if (!IsPlausible(f)) continue;
            switch (f.Id)
            {
                case "10x15" when !f.HasCanvas: P10x15 = P10x15 with { WidthMm = f.WidthMm, HeightMm = f.HeightMm }; break;
                case "15x20" when !f.HasCanvas: P15x20 = P15x20 with { WidthMm = f.WidthMm, HeightMm = f.HeightMm }; break;
                case "13x18" when f.HasCanvas: P13x18 = P13x18 with { WidthMm = f.WidthMm, HeightMm = f.HeightMm, CanvasWidthMm = f.CanvasWidthMm, CanvasHeightMm = f.CanvasHeightMm }; break;
                default: continue;
            }

            applied.Add(f.Id);
        }

        return applied;
    }

    private static bool IsPlausible(PrintFormat f)
    {
        static bool Ok(double? mm) => mm is null || (mm >= 20 && mm <= 1000);
        return f.WidthMm > 0 && f.HeightMm > 0 && f.WidthMm <= f.HeightMm && Ok(f.WidthMm) && Ok(f.HeightMm) && Ok(f.CanvasWidthMm) && Ok(f.CanvasHeightMm)
            && (!f.HasCanvas || (f.CanvasWidthMm >= f.WidthMm && f.CanvasHeightMm >= f.HeightMm));
    }
}
