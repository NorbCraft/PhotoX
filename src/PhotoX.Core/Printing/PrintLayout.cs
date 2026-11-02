using PhotoX.Core.Geometry;

namespace PhotoX.Core.Printing;

/// <summary>
/// How content goes on a print page (prompt_11 §1). Photo printers such as the Citizen CY-02 print borderless on a page
/// a little larger than the finished print (bleed, cut off by the printer): (6x4) is 156.1 × 105.0 mm for a 152.4 ×
/// 101.6 mm (6 × 4 in) print. The finished print is that nominal size, centred on the page.
/// </summary>
public enum PrintFit
{
    /// <summary>An untouched photo in Crop: covers the whole page, bleed included, with the largest centred crop.</summary>
    Crop,

    /// <summary>
    /// A photo edited without borders (Fill, no canvas): covers the whole page like Crop when its ratio is the print
    /// size's (within 2 %), otherwise placed like <see cref="Fit"/>.
    /// </summary>
    Borderless,

    /// <summary>
    /// Content with an exact physical size or intended white borders (Whole, the 9×13, 13×18 and custom canvases,
    /// photos with borders, ID sheets, collages, the calibration sheet): every pixel inside the finished print, never
    /// enlarged into the bleed; at true size when its ratio is the print size's.
    /// </summary>
    Fit,
}

/// <summary>
/// Where content goes on a print page: turned a quarter (clockwise) when its orientation differs from the page's,
/// the part of the (turned) content that is used, the target rectangle on the page in millimetres, and the finished
/// print within the page (the whole page when the paper has no bleed).
/// </summary>
public sealed record PrintPlacement(bool Rotate, NormalizedRect SourceCrop, RectD TargetMm, RectD FinishedMm)
{
    /// <summary>The content covers the whole page, so there is no edge to carry into the bleed.</summary>
    public bool CoversPage(double pageWidthMm, double pageHeightMm) =>
        TargetMm.X <= 1e-6 && TargetMm.Y <= 1e-6 && TargetMm.Right >= pageWidthMm - 1e-6 && TargetMm.Bottom >= pageHeightMm - 1e-6;
}

/// <summary>
/// Print layout (prompt_05 §§2–3, prompt_11 §1), shared by the print preview and the print itself. Content is never
/// stretched. See <see cref="PrintFit"/> for what fills the page with its bleed and what keeps its true size.
/// </summary>
public static class PrintLayout
{
    public const double FillTolerance = 0.02;

    /// <summary>A page without bleed: the finished print is the page (a PDF printer, a driver that reports the nominal size).</summary>
    public static PrintPlacement Place(double contentWidth, double contentHeight, double pageWidthMm, double pageHeightMm, bool cropToFill) =>
        Place(contentWidth, contentHeight, pageWidthMm, pageHeightMm, RectD.FromSize(pageWidthMm, pageHeightMm), cropToFill ? PrintFit.Crop : PrintFit.Fit);

    public static PrintPlacement Place(double contentWidth, double contentHeight, double pageWidthMm, double pageHeightMm, RectD finishedMm, PrintFit fit)
    {
        var rotate = contentWidth > contentHeight != pageWidthMm > pageHeightMm && Math.Abs(contentWidth - contentHeight) > 1e-9;
        var (w, h) = rotate ? (contentHeight, contentWidth) : (contentWidth, contentHeight);
        var contentRatio = w / h;
        var finishedRatio = finishedMm.Width / finishedMm.Height;
        var sameRatio = Math.Abs(contentRatio / finishedRatio - 1) <= FillTolerance;
        if (fit == PrintFit.Crop || (fit == PrintFit.Borderless && sameRatio))
        {
            // Borderless: the page with its bleed is covered, so a cut a little off centre shows the photo, never paper.
            var crop = CropMath.LargestCentered(RectD.FromSize(w, h), pageWidthMm / pageHeightMm);
            return new PrintPlacement(rotate, NormalizedRect.FromPixels(crop, w, h), RectD.FromSize(pageWidthMm, pageHeightMm), finishedMm);
        }

        if (sameRatio)
        {
            // The finished print exactly (at most 2 % trimmed): true size for sheets and canvases made for this print size.
            var crop = CropMath.LargestCentered(RectD.FromSize(w, h), finishedRatio);
            return new PrintPlacement(rotate, NormalizedRect.FromPixels(crop, w, h), finishedMm, finishedMm);
        }

        var target = CropMath.LargestCentered(RectD.FromSize(finishedMm.Width, finishedMm.Height), contentRatio);
        return new PrintPlacement(rotate, NormalizedRect.Full, new RectD(finishedMm.X + target.X, finishedMm.Y + target.Y, target.Width, target.Height), finishedMm);
    }

    /// <summary>
    /// The finished print of <paramref name="printWidthMm"/> × <paramref name="printHeightMm"/> on a page, turned like the
    /// page and centred on it; never larger than the page (a driver may round a side down).
    /// </summary>
    public static RectD Finished(double pageWidthMm, double pageHeightMm, double printWidthMm, double printHeightMm)
    {
        var (w, h) = pageWidthMm > pageHeightMm == printWidthMm > printHeightMm ? (printWidthMm, printHeightMm) : (printHeightMm, printWidthMm);
        (w, h) = (Math.Min(w, pageWidthMm), Math.Min(h, pageHeightMm));
        return new RectD((pageWidthMm - w) / 2, (pageHeightMm - h) / 2, w, h);
    }

    /// <summary>
    /// True when a printer paper of <paramref name="widthMm"/> × <paramref name="heightMm"/> is the paper of the print
    /// size (either orientation): each side at most <see cref="PaperMatcher.ToleranceMm"/> smaller (rounding) or
    /// <see cref="PaperMatcher.MaxBleedMm"/> larger (a borderless page with bleed, prompt_11 §1).
    /// </summary>
    public static bool PaperMatches(double widthMm, double heightMm, double printWidthMm, double printHeightMm)
    {
        var (pw, ph) = (Math.Min(widthMm, heightMm), Math.Max(widthMm, heightMm));
        var (w, h) = (Math.Min(printWidthMm, printHeightMm), Math.Max(printWidthMm, printHeightMm));
        return PaperMatcher.Fits(pw, w) && PaperMatcher.Fits(ph, h);
    }
}
