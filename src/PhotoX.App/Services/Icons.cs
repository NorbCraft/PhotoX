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

    /// <summary>Four arrows pointing to the corners: reset the crop to the largest frame (prompt_04 §7).</summary>
    public static readonly Geometry ResetCrop = Geometry.Parse("M9.5 9.5 4 4 M4 9V4h5 M14.5 9.5 20 4 M15 4h5v5 M14.5 14.5 20 20 M20 15v5h-5 M9.5 14.5 4 20 M9 20H4v-5");

    /// <summary>Gear (settings), drawn as one clean outline with a centre hole (prompt_04 §3).</summary>
    public static readonly Geometry Settings = Geometry.Parse(GearPath());

    /// <summary>A tilted horizon over a level line.</summary>
    public static readonly Geometry Straighten = Geometry.Parse("M3 17h18 M4 12.5 20 7.5 M17 17a5 5 0 0 0-.6-2.4");

    /// <summary>Half-filled circle: before / after.</summary>
    public static readonly Geometry BeforeAfter = Geometry.Parse("M12 3a9 9 0 1 0 0 18a9 9 0 1 0 0-18z M12 3v18 M12 7h4 M12 11h6 M12 15h5");

    /// <summary>An eight-tooth gear on the 24 × 24 grid: trapezoid teeth on a root circle, and a round hole.</summary>
    private static string GearPath()
    {
        const int teeth = 8;
        const double cx = 12, cy = 12, tip = 10, root = 7.6, hole = 3.2;
        var inv = System.Globalization.CultureInfo.InvariantCulture;
        string P(double r, double deg) => string.Format(inv, "{0:0.###} {1:0.###}", cx + r * Math.Cos(deg * Math.PI / 180), cy + r * Math.Sin(deg * Math.PI / 180));
        var sb = new System.Text.StringBuilder();
        for (var i = 0; i < teeth; i++)
        {
            var a = i * 360.0 / teeth - 90;
            sb.Append(i == 0 ? "M" : "L").Append(P(root, a - 15));
            sb.Append(" L").Append(P(tip, a - 9)).Append(" L").Append(P(tip, a + 9)).Append(" L").Append(P(root, a + 15));
            sb.Append(string.Format(inv, " A{0} {0} 0 0 1 ", root)).Append(P(root, a + 360.0 / teeth - 15)).Append(' ');
        }

        sb.Append("Z M").Append(P(hole, 0)).Append(string.Format(inv, " A{0} {0} 0 1 1 ", hole)).Append(P(hole, 180));
        sb.Append(string.Format(inv, " A{0} {0} 0 1 1 ", hole)).Append(P(hole, 0)).Append(" Z");
        return sb.ToString();
    }
}
