using PhotoX.Core.Formats;
using PhotoX.Core.Geometry;

namespace PhotoX.Core.Editing;

public enum Composition
{
    /// <summary>Crop to fill the print ratio (default).</summary>
    Fill,

    /// <summary>Keep the whole photo; extend the canvas with white borders to the print ratio (FR-049).</summary>
    WholeWithBorder,
}

/// <summary>
/// Complete description of an ordinary-photo edit (DEC-007). Immutable; preview and save render the same recipe.
/// The crop is normalized to the image after quarter turns and straightening.
/// </summary>
public sealed record EditRecipe
{
    public string FormatId { get; init; } = PrintFormats.DefaultId;

    /// <summary>Custom ratio (portrait width / height) when <see cref="FormatId"/> is "custom".</summary>
    public double CustomRatio { get; init; } = 2.0 / 3.0;

    public CropOrientation Orientation { get; init; } = CropOrientation.Landscape;

    public Composition Composition { get; init; } = Composition.Fill;

    public NormalizedRect Crop { get; init; } = NormalizedRect.Full;

    /// <summary>Clockwise quarter turns, 0..3.</summary>
    public int QuarterTurns { get; init; }

    /// <summary>Straightening angle in degrees, −10..10 (positive = clockwise).</summary>
    public double Straighten { get; init; }

    /// <summary>Exposure in EV, −2..2.</summary>
    public double Exposure { get; init; }

    public double Contrast { get; init; }

    public double Saturation { get; init; }

    public double Temperature { get; init; }

    public double Tint { get; init; }

    /// <summary>Levels black point, 0..1.</summary>
    public double LevelsBlack { get; init; }

    /// <summary>Levels midtone gamma, 0.3..3 (1 = neutral).</summary>
    public double LevelsMid { get; init; } = 1.0;

    /// <summary>Levels white point, 0..1.</summary>
    public double LevelsWhite { get; init; } = 1.0;

    public bool HasToneAdjustments =>
        Math.Abs(Exposure) > 1e-6 || Math.Abs(Contrast) > 1e-6 || Math.Abs(Saturation) > 1e-6 ||
        Math.Abs(Temperature) > 1e-6 || Math.Abs(Tint) > 1e-6 ||
        LevelsBlack > 1e-6 || Math.Abs(LevelsMid - 1) > 1e-6 || LevelsWhite < 1 - 1e-6;

    /// <summary>Same recipe with all tone adjustments reset (used for before/after and Reset).</summary>
    public EditRecipe WithoutToneAdjustments() => this with
    {
        Exposure = 0, Contrast = 0, Saturation = 0, Temperature = 0, Tint = 0,
        LevelsBlack = 0, LevelsMid = 1, LevelsWhite = 1,
    };

    /// <summary>Portrait ratio of the selected format.</summary>
    public double PortraitRatio =>
        FormatId == PrintFormats.CustomId ? CustomRatio : (PrintFormats.Find(FormatId) ?? PrintFormats.P10x15).PortraitRatio;

    /// <summary>Crop ratio (width / height) for the selected orientation.</summary>
    public double CropRatio => CropMath.OrientedRatio(PortraitRatio, Orientation);
}
