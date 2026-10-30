using PhotoX.Core.Geometry;

namespace PhotoX.Core.Printing;

/// <summary>
/// Where content goes on a print page: turned a quarter (clockwise) when its orientation differs from the page's,
/// the part of the (turned) content that is used, and the target rectangle on the page in millimetres.
/// </summary>
public sealed record PrintPlacement(bool Rotate, NormalizedRect SourceCrop, RectD TargetMm);

/// <summary>
/// Print layout (prompt_05 §§2–3), shared by the print preview and the print itself. Content is never stretched.
/// <list type="bullet">
/// <item>Unedited photos fill the paper with the largest centred crop of the paper's ratio (the editor's default crop).</item>
/// <item>Edited photos, ID sheets and collages keep all their pixels: when their ratio matches the paper within 2 % they
/// fill it (at most 2 % trimmed); otherwise they fit inside it, centred on white.</item>
/// </list>
/// </summary>
public static class PrintLayout
{
    public const double FillTolerance = 0.02;

    public static PrintPlacement Place(double contentWidth, double contentHeight, double pageWidthMm, double pageHeightMm, bool cropToFill)
    {
        var rotate = contentWidth > contentHeight != pageWidthMm > pageHeightMm && Math.Abs(contentWidth - contentHeight) > 1e-9;
        var (w, h) = rotate ? (contentHeight, contentWidth) : (contentWidth, contentHeight);
        var contentRatio = w / h;
        var pageRatio = pageWidthMm / pageHeightMm;
        var page = RectD.FromSize(pageWidthMm, pageHeightMm);
        if (cropToFill || Math.Abs(contentRatio / pageRatio - 1) <= FillTolerance)
        {
            var crop = CropMath.LargestCentered(RectD.FromSize(w, h), pageRatio);
            return new PrintPlacement(rotate, NormalizedRect.FromPixels(crop, w, h), page);
        }

        return new PrintPlacement(rotate, NormalizedRect.Full, CropMath.LargestCentered(page, contentRatio));
    }

    /// <summary>True when a printer paper of <paramref name="widthMm"/> × <paramref name="heightMm"/> is the print size (either orientation, ±2 mm).</summary>
    public static bool PaperMatches(double widthMm, double heightMm, double printWidthMm, double printHeightMm)
    {
        static bool Near(double a, double b) => Math.Abs(a - b) <= 2;
        return (Near(widthMm, printWidthMm) && Near(heightMm, printHeightMm)) || (Near(widthMm, printHeightMm) && Near(heightMm, printWidthMm));
    }
}
