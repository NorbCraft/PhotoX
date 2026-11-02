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

    /// <summary>Printer: the print queue (prompt_05 §4).</summary>
    public static readonly Geometry Printer = Geometry.Parse(
        "M7 9V4h10v5 M6.5 17H4.5A1.5 1.5 0 0 1 3 15.5v-5A1.5 1.5 0 0 1 4.5 9h15a1.5 1.5 0 0 1 1.5 1.5v5a1.5 1.5 0 0 1-1.5 1.5h-2 M7 13.5h10V20H7z");

    /// <summary>Workspace Photos (prompt_06 §1): a picture with a mountain and the sun.</summary>
    public static readonly Geometry Photos = Geometry.Parse(
        "M5.5 4h13A1.5 1.5 0 0 1 20 5.5v13a1.5 1.5 0 0 1-1.5 1.5h-13A1.5 1.5 0 0 1 4 18.5v-13A1.5 1.5 0 0 1 5.5 4z M4 16.5l4.5-4.5 4 4 2.5-2.5 5 5 M15.5 7.5a1.6 1.6 0 1 1 0 3.2a1.6 1.6 0 1 1 0-3.2z");

    /// <summary>Workspace ID Photos: a portrait card.</summary>
    public static readonly Geometry IdPhoto = Geometry.Parse(
        "M6.5 3.5h11A1.5 1.5 0 0 1 19 5v14a1.5 1.5 0 0 1-1.5 1.5h-11A1.5 1.5 0 0 1 5 19V5a1.5 1.5 0 0 1 1.5-1.5z M12 7.2a2.6 2.6 0 1 1 0 5.2a2.6 2.6 0 1 1 0-5.2z M7.8 17.6c.7-2 2.3-3 4.2-3s3.5 1 4.2 3");

    /// <summary>Workspace Collages: four frames of different sizes.</summary>
    public static readonly Geometry Collage = Geometry.Parse(
        "M5 4h5.5a1 1 0 0 1 1 1v8a1 1 0 0 1-1 1H5a1 1 0 0 1-1-1V5a1 1 0 0 1 1-1z M14.5 4H19a1 1 0 0 1 1 1v4a1 1 0 0 1-1 1h-4.5a1 1 0 0 1-1-1V5a1 1 0 0 1 1-1z M14.5 12.5H19a1 1 0 0 1 1 1V19a1 1 0 0 1-1 1h-4.5a1 1 0 0 1-1-1v-5.5a1 1 0 0 1 1-1z M5 16.5h5.5a1 1 0 0 1 1 1V19a1 1 0 0 1-1 1H5a1 1 0 0 1-1-1v-1.5a1 1 0 0 1 1-1z");

    /// <summary>Magnifier with a plus: zoom.</summary>
    public static readonly Geometry Zoom = Geometry.Parse("M10.5 4a6.5 6.5 0 1 1 0 13a6.5 6.5 0 1 1 0-13z M15.2 15.2 20 20 M10.5 8v5 M8 10.5h5");

    /// <summary>A face in focus brackets: Auto Align.</summary>
    public static readonly Geometry AutoAlign = Geometry.Parse(
        "M4 8V5.5A1.5 1.5 0 0 1 5.5 4H8 M16 4h2.5A1.5 1.5 0 0 1 20 5.5V8 M20 16v2.5a1.5 1.5 0 0 1-1.5 1.5H16 M8 20H5.5A1.5 1.5 0 0 1 4 18.5V16 M12 7.5a3 3.6 0 1 1 0 7.2a3 3.6 0 1 1 0-7.2z M9.5 11h5");

    /// <summary>A sun: background brightness.</summary>
    public static readonly Geometry Brightness = Geometry.Parse(
        "M12 8.5a3.5 3.5 0 1 1 0 7a3.5 3.5 0 1 1 0-7z M12 3v2 M12 19v2 M3 12h2 M19 12h2 M5.6 5.6l1.4 1.4 M17 17l1.4 1.4 M5.6 18.4 7 17 M17 7l1.4-1.4");

    /// <summary>Magnifier with a minus and with a plus: smaller and larger thumbnails (prompt_10 §8).</summary>
    public static readonly Geometry ThumbSmaller = Geometry.Parse("M10.5 4a6.5 6.5 0 1 1 0 13a6.5 6.5 0 1 1 0-13z M15.2 15.2 20 20 M8 10.5h5");

    public static readonly Geometry ThumbLarger = Zoom;

    /// <summary>Gear (settings), drawn as one clean outline with a centre hole (prompt_04 §3).</summary>
    public static readonly Geometry Settings = Geometry.Parse(GearPath());

    /// <summary>A tilted horizon over a level line.</summary>
    public static readonly Geometry Straighten = Geometry.Parse("M3 17h18 M4 12.5 20 7.5 M17 17a5 5 0 0 0-.6-2.4");

    /// <summary>Half-filled circle: before / after.</summary>
    public static readonly Geometry BeforeAfter = Geometry.Parse("M12 3a9 9 0 1 0 0 18a9 9 0 1 0 0-18z M12 3v18 M12 7h4 M12 11h6 M12 15h5");

    // Icons beside button labels (owner's choice B of the prompt_07 mockups, docs/mockups/buttons).

    /// <summary>A cross: Close, Clear.</summary>
    public static readonly Geometry Close = Geometry.Parse("M6.5 6.5l11 11 M17.5 6.5l-11 11");

    /// <summary>A disk: Save.</summary>
    public static readonly Geometry Save = Geometry.Parse("M5.5 4h10.5l3.5 3.5v11a1.5 1.5 0 0 1-1.5 1.5h-12A1.5 1.5 0 0 1 4.5 18.5v-13A1.5 1.5 0 0 1 5.5 4z M8 4v4.5h7V4 M7.5 20v-6h9v6");

    /// <summary>An arrow to a bar: Save &amp; Next, Continue.</summary>
    public static readonly Geometry SaveNext = Geometry.Parse("M4 12h12 M11.5 7.5 16 12l-4.5 4.5 M20 5.5v13");

    /// <summary>A circular arrow: Reset.</summary>
    public static readonly Geometry Reset = Geometry.Parse("M5 12a7 7 0 1 0 2.1-5 M4.5 4.5v4h4");

    /// <summary>A pencil: Edit this photo.</summary>
    public static readonly Geometry Edit = Geometry.Parse("M4.5 19.5h4l10-10a2.1 2.1 0 0 0-3-3l-10 10v3z M14 8l3 3");

    /// <summary>A bin: Discard originals, Clear cache (things that are deleted).</summary>
    public static readonly Geometry Delete = Geometry.Parse("M4.5 7h15 M9.5 7V4.5h5V7 M6.5 7l.9 12a1.5 1.5 0 0 0 1.5 1.5h6.2a1.5 1.5 0 0 0 1.5-1.5l.9-12 M10 11v5.5 M14 11v5.5");

    /// <summary>A clock turning back: Restore original.</summary>
    public static readonly Geometry Restore = Geometry.Parse("M4.6 13a7.5 7.5 0 1 0 1.7-6.4 M4.5 3.8v4h4 M12 8.5v4l2.6 1.6");

    /// <summary>A wand with sparkles: Auto Adjust.</summary>
    public static readonly Geometry AutoAdjust = Geometry.Parse("M4.5 19.5 14 10 M12.5 8.5l3 3 M17.5 3.5v3 M16 5h3 M8.5 3.5v2 M7.5 4.5h2 M19.5 12v2 M18.5 13h2");

    /// <summary>Two sheets: Duplicate image.</summary>
    public static readonly Geometry Duplicate = Geometry.Parse("M9 8.5h9.5A1.5 1.5 0 0 1 20 10v8.5a1.5 1.5 0 0 1-1.5 1.5H9A1.5 1.5 0 0 1 7.5 18.5V10A1.5 1.5 0 0 1 9 8.5z M16 8.5V5.5A1.5 1.5 0 0 0 14.5 4h-9A1.5 1.5 0 0 0 4 5.5v9A1.5 1.5 0 0 0 5.5 16h2");

    /// <summary>A frame with a plus or a minus: place a photo in a collage frame, remove it.</summary>
    public static readonly Geometry FrameAdd = Geometry.Parse("M5.5 4h13A1.5 1.5 0 0 1 20 5.5v13a1.5 1.5 0 0 1-1.5 1.5h-13A1.5 1.5 0 0 1 4 18.5v-13A1.5 1.5 0 0 1 5.5 4z M12 8v8 M8 12h8");

    public static readonly Geometry FrameRemove = Geometry.Parse("M5.5 4h13A1.5 1.5 0 0 1 20 5.5v13a1.5 1.5 0 0 1-1.5 1.5h-13A1.5 1.5 0 0 1 4 18.5v-13A1.5 1.5 0 0 1 5.5 4z M8 12h8");

    /// <summary>A folder: choose the output folder.</summary>
    public static readonly Geometry Folder = Geometry.Parse("M4.5 6h4.5l2 2h8.5A1.5 1.5 0 0 1 21 9.5v8a1.5 1.5 0 0 1-1.5 1.5h-15A1.5 1.5 0 0 1 3 17.5v-10A1.5 1.5 0 0 1 4.5 6z");

    /// <summary>A window with a side bar: show or hide the folder tree (prompt_08 §4).</summary>
    public static readonly Geometry Sidebar = Geometry.Parse("M4.5 4.5h15A1.5 1.5 0 0 1 21 6v12a1.5 1.5 0 0 1-1.5 1.5h-15A1.5 1.5 0 0 1 3 18V6a1.5 1.5 0 0 1 1.5-1.5z M9.5 4.5v15 M5.5 8.5h2 M5.5 11.5h2 M5.5 14.5h2");

    /// <summary>A folder with an arrow up: the parent folder (prompt_08 §1).</summary>
    public static readonly Geometry FolderUp = Geometry.Parse("M3 7.5A1.5 1.5 0 0 1 4.5 6H9l2 2h8.5A1.5 1.5 0 0 1 21 9.5v8a1.5 1.5 0 0 1-1.5 1.5h-15A1.5 1.5 0 0 1 3 17.5z M12 16.5v-6 M9.5 13 12 10.5l2.5 2.5");

    /// <summary>Chevrons: fold a side panel away and bring it back (prompt_08 §3).</summary>
    public static readonly Geometry ChevronLeft = Geometry.Parse("M14.5 6 8.5 12l6 6");

    public static readonly Geometry ChevronRight = Geometry.Parse("M9.5 6l6 6-6 6");

    /// <summary>An arrow down to a tray: download and install an update (prompt_08 §6).</summary>
    public static readonly Geometry Download = Geometry.Parse("M12 4v11 M7.5 10.5 12 15l4.5-4.5 M5 19.5h14");

    public static readonly Geometry Undo = Geometry.Parse("M9 6.5 4.5 11 9 15.5 M5 11h9.5a4.5 4.5 0 0 1 0 9H12");

    public static readonly Geometry Redo = Geometry.Parse("M15 6.5 19.5 11 15 15.5 M19 11H9.5a4.5 4.5 0 0 0 0 9H12");

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
