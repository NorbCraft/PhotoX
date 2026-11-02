using PhotoX.Core.Printing;
using PhotoX.Printing;
using PhotoX.Storage;

namespace PhotoX.Tests;

/// <summary>prompt_07: paper matching, Crop | Whole for untouched photos, all-time start (TS-22).</summary>
public class Prompt07CoreTests
{
    private static DriverPaper P(string name, double w, double h) => new(name, w, h);

    [Fact]
    public void Papers_are_matched_by_their_size_whatever_the_driver_calls_them()
    {
        // Citizen-like names: the plain 6x8 wins over the two-up variant even when listed first.
        var citizen = new[] { P("(6x8) 2-up", 152.4, 203.2), P("(6x4)", 152.4, 101.6), P("(6x8)", 152.4, 203.2), P("(5x7)", 127, 177.8) };
        Assert.Equal(1, PaperMatcher.Best(citizen, 101.6, 152.4));
        Assert.Equal(2, PaperMatcher.Best(citizen, 152.4, 203.2));

        // Other spellings, whole-millimetre rounding, and a name with "x2" inside a size.
        var other = new[] { P("Letter", 215.9, 279.4), P("10x15cm", 102, 152), P("152x203mm", 152, 203), P("4 x 6 in", 101.6, 152.4) };
        Assert.Equal(3, PaperMatcher.Best(other, 101.6, 152.4));            // the exact size before the rounded one
        Assert.Equal(2, PaperMatcher.Best(other, 152.4, 203.2));
        Assert.False(PaperMatcher.IsVariant("152x203mm"));
        Assert.True(PaperMatcher.IsVariant("6x8 x2"));
        Assert.True(PaperMatcher.IsVariant("PC 6x8 2up"));

        // A PDF printer: 4×6 in is the 10×15 paper; there is no 6×8 in, so 15×20 gets nothing (no silent A4).
        var pdf = new[] { P("Letter", 215.9, 279.4), P("A4", 210, 297), P("4\" x 6\"", 101.6, 152.4), P("5\" x 7\"", 127, 177.8) };
        Assert.Equal(2, PaperMatcher.Best(pdf, 101.6, 152.4));
        Assert.Null(PaperMatcher.Best(pdf, 152.4, 203.2));
        Assert.Null(PaperMatcher.Best([P("A6", 105, 148)], 101.6, 152.4)); // close, but 3.4 mm off: not the same paper
    }

    [Fact]
    public void Untouched_photos_crop_or_show_whole_but_edits_keep_their_crop()
    {
        using var dir = new TempDir();
        var photo = Fixtures.WriteJpeg(dir.Path, "P1.jpg", 600, 400);
        Assert.Equal(PrintFit.Crop, PrintContent.Load(new PhotoPrintItem("P1.jpg", photo, null, false, 1), preview: true).Fit);
        Assert.Equal(PrintFit.Fit, PrintContent.Load(new PhotoPrintItem("P1.jpg", photo, null, false, 1, Whole: true), preview: true).Fit);
        Assert.Equal(PrintFit.Borderless, PrintContent.Load(new PhotoPrintItem("P1.jpg", photo, null, true, 1), preview: true).Fit);
        Assert.Equal(PrintFit.Borderless, PrintContent.Load(new PhotoPrintItem("P1.jpg", photo, new Core.Editing.EditRecipe { Exposure = 0.2 }, false, 1), preview: true).Fit);
    }

    [Fact]
    public void All_time_totals_know_their_first_job()
    {
        var t0 = new DateTime(2026, 11, 1, 9, 0, 0, DateTimeKind.Utc);
        var totals = PrintTotals.Of([
            new PrintLogEntry("a", t0.AddDays(2), "x", "10x15", 1, "P", PrintOutcome.Finished),
            new PrintLogEntry("b", t0, "y", "10x15", 1, "P", PrintOutcome.Canceled)]);
        Assert.Equal(t0, totals.FirstUtc);
        Assert.Null(PrintTotals.Of([]).FirstUtc);
    }
}
