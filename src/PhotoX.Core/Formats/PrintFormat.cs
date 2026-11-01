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
/// The ordinary print formats (plan §8.1). The four presets are fixed; their physical dimensions come from the
/// configuration data file when present (FR-010, FR-011, FR-040), so they can be corrected after print tests.
/// The print sheets (10×15, 15×20, 20×30) are the papers that custom sizes and collages are placed on.
/// </summary>
public static class PrintFormats
{
    public const string DefaultId = "10x15";
    public const string CustomId = "custom";

    public static PrintFormat P10x15 { get; private set; } = new("10x15", "10 × 15", 101.6, 152.4);

    public static PrintFormat P15x20 { get; private set; } = new("15x20", "15 × 20", 152.4, 203.2);

    public static PrintFormat P13x18 { get; private set; } = new("13x18", "13 × 18", 130, 180, 152.4, 203.2);

    /// <summary>9×13 cm centred on a white 10×15 sheet, like 13×18 on 15×20 (prompt_07 §1.1).</summary>
    public static PrintFormat P9x13 { get; private set; } = new("9x13", "9 × 13", 90, 130, 101.6, 152.4);

    /// <summary>20×30 cm sheet, 8×12 in like the other inch-based sheets (prompt_04 §5, DEC-051).</summary>
    public static PrintFormat P20x30 { get; private set; } = new("20x30", "20 × 30", 203.2, 304.8);

    /// <summary>The researched defaults (OQ-001), as written to a new configuration file.</summary>
    public static IReadOnlyList<PrintFormat> Defaults { get; } = [P10x15, P15x20, P13x18, P9x13, P20x30];

    /// <summary>The crop presets of the Photos editor.</summary>
    public static IReadOnlyList<PrintFormat> BuiltIn => [P10x15, P15x20, P9x13, P13x18];

    /// <summary>The sizes PhotoX prints and lays out collages on (prompt_05: 10×15 and 15×20).</summary>
    public static IReadOnlyList<PrintFormat> PrintSizes => [P10x15, P15x20];

    public static PrintFormat? FindPrintSize(string id) => PrintSizes.FirstOrDefault(f => f.Id == id);

    /// <summary>Print sheets for custom sizes in the editor, smallest first.</summary>
    public static IReadOnlyList<PrintFormat> Sheets => [P10x15, P15x20, P20x30];

    public static PrintFormat? Find(string id) => BuiltIn.FirstOrDefault(f => f.Id == id);

    public static PrintFormat? FindSheet(string id) => Sheets.FirstOrDefault(f => f.Id == id);

    /// <summary>
    /// A custom print size (prompt_04 §7): the photo keeps exactly <paramref name="widthMm"/> × <paramref name="heightMm"/>
    /// and is centred on the smallest print sheet it fits on (9×13 on 10×15, 14×17 on 15×20), like 13×18 on 15×20.
    /// A size larger than every sheet gets no canvas. Dimensions are ordered to portrait.
    /// </summary>
    public static PrintFormat Custom(double widthMm, double heightMm)
    {
        var (w, h) = (Math.Min(widthMm, heightMm), Math.Max(widthMm, heightMm));
        var name = string.Format(System.Globalization.CultureInfo.InvariantCulture, "{0:0.#} × {1:0.#}", w / 10, h / 10);
        var sheet = Sheets.FirstOrDefault(s => w <= s.WidthMm + 1e-6 && h <= s.HeightMm + 1e-6);
        return sheet is null ? new PrintFormat(CustomId, name, w, h) : new PrintFormat(CustomId, name, w, h, sheet.WidthMm, sheet.HeightMm);
    }

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
                case "9x13" when f.HasCanvas: P9x13 = P9x13 with { WidthMm = f.WidthMm, HeightMm = f.HeightMm, CanvasWidthMm = f.CanvasWidthMm, CanvasHeightMm = f.CanvasHeightMm }; break;
                case "20x30" when !f.HasCanvas: P20x30 = P20x30 with { WidthMm = f.WidthMm, HeightMm = f.HeightMm }; break;
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
