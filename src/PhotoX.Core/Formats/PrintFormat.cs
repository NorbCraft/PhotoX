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
    public double PortraitRatio => WidthMm / HeightMm;

    public bool HasCanvas => CanvasWidthMm is not null && CanvasHeightMm is not null;

    /// <summary>Physical long edge of what is printed (the canvas when present) in inches.</summary>
    public double PrintedLongEdgeInches => Math.Max(CanvasWidthMm ?? WidthMm, CanvasHeightMm ?? HeightMm) / 25.4;
}

/// <summary>Built-in formats (plan §8.1). Physical dimensions can be refined in configuration data.</summary>
public static class PrintFormats
{
    public const string DefaultId = "10x15";
    public const string CustomId = "custom";

    public static PrintFormat P10x15 { get; } = new("10x15", "10 × 15", 101.6, 152.4);
    public static PrintFormat P15x20 { get; } = new("15x20", "15 × 20", 152.4, 203.2);
    public static PrintFormat P13x18 { get; } = new("13x18", "13 × 18", 130, 180, 152.4, 203.2);

    public static IReadOnlyList<PrintFormat> BuiltIn { get; } = [P10x15, P15x20, P13x18];

    public static PrintFormat? Find(string id) => BuiltIn.FirstOrDefault(f => f.Id == id);
}
