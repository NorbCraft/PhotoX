using Avalonia.Media;

namespace PhotoX.App.Services;

/// <summary>Stroke icons (24 × 24 design grid) for toolbar buttons.</summary>
public static class Icons
{
    public static readonly Geometry FolderBack = Geometry.Parse(
        "M3 7.5A1.5 1.5 0 0 1 4.5 6H9l2 2h8.5A1.5 1.5 0 0 1 21 9.5v8a1.5 1.5 0 0 1-1.5 1.5h-15A1.5 1.5 0 0 1 3 17.5z M15.5 13.5h-7 M11 11 8.5 13.5 11 16");

    public static readonly Geometry RotateLeft = Geometry.Parse("M4.5 9.5A8 8 0 1 1 6 17 M4 4.5v5h5");

    public static readonly Geometry RotateRight = Geometry.Parse("M19.5 9.5A8 8 0 1 0 18 17 M20 4.5v5h-5");

    public static readonly Geometry Crop = Geometry.Parse("M6.5 2.5v15h15 M2.5 6.5h15v15");

    /// <summary>Corner marks: reset the crop to the largest frame.</summary>
    public static readonly Geometry ResetCrop = Geometry.Parse("M4 9V4h5 M15 4h5v5 M20 15v5h-5 M9 20H4v-5 M9.5 12h5 M12 9.5v5");

    /// <summary>A tilted horizon over a level line.</summary>
    public static readonly Geometry Straighten = Geometry.Parse("M3 19h18 M4 14.5 20 9.5 M17 19a5 5 0 0 0-.6-2.4");

    /// <summary>Half-filled circle: before / after.</summary>
    public static readonly Geometry BeforeAfter = Geometry.Parse("M12 3a9 9 0 1 0 0 18a9 9 0 1 0 0-18z M12 3v18 M12 7h4 M12 11h6 M12 15h5");
}
