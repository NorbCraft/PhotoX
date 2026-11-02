using PhotoX.App.Localization;
using PhotoX.Storage;

namespace PhotoX.App.ViewModels;

/// <summary>
/// The Copies control [− n +] for the selected photos (prompt_09 §4), shared by the Photos sidebar and the Printing
/// workspace (prompt_10 §6): one value when the photos agree, "–" and Mixed (a–b) when they differ; − and + start from
/// the lowest value and set every selected photo to the result.
/// </summary>
public static class CopiesControl
{
    public static int? Value(IReadOnlyCollection<int> copies) => copies.Count > 0 && copies.Distinct().Count() == 1 ? copies.First() : null;

    public static bool IsMixed(IReadOnlyCollection<int> copies) => copies.Count > 1 && Value(copies) is null;

    public static string ValueText(IReadOnlyCollection<int> copies) => Value(copies)?.ToString(Localizer.Instance.Culture) ?? "–";

    /// <summary>Below the control with several photos selected: for how many, and the range when they differ.</summary>
    public static string? Note(IReadOnlyCollection<int> copies)
    {
        if (copies.Count < 2) return null;
        var count = Localizer.Instance.Plural("QuantityForPhotos", copies.Count);
        return IsMixed(copies) ? Localizer.Instance.Format("QuantityMixed", copies.Min(), copies.Max()) + " · " + count : count;
    }

    public static int Minus(IReadOnlyCollection<int> copies) => Math.Clamp(copies.Min() - 1, 0, PrintQuantities.Max);

    public static int Plus(IReadOnlyCollection<int> copies) => Math.Clamp(copies.Min() + 1, 0, PrintQuantities.Max);
}
