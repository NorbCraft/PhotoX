using ImageMagick;
using PhotoX.Core.Collage;
using PhotoX.Core.Editing;
using PhotoX.Core.Formats;
using PhotoX.Core.Geometry;
using PhotoX.Core.Printing;
using PhotoX.Imaging.Collage;
using PhotoX.Imaging.Printing;
using PhotoX.Printing;
using PhotoX.Storage;

namespace PhotoX.Tests;

/// <summary>
/// prompt_11 §1 against the papers and dot counts of the Citizen CY-02 driver (its manual: (6x4) is 1844 × 1240 dots,
/// 156.1 × 105.0 mm, for a 6 × 4 in print): paper choice, the finished print on the page, Crop covering the bleed,
/// Whole and true-size content (ID sheet, collage, 9×13 canvas) never enlarged (TS-26). Software only: the physical
/// cut is not simulated.
/// </summary>
public class PrintBleedTests
{
    private static PrinterPaper Paper(string name) => FakePrintBackend.CitizenPapers.Single(p => p.Name == name);

    private static (int X, int Y, int W, int H) Px(RectD mm) =>
        ((int)Math.Round(mm.X * 300 / 25.4), (int)Math.Round(mm.Y * 300 / 25.4), (int)Math.Round(mm.Width * 300 / 25.4), (int)Math.Round(mm.Height * 300 / 25.4));

    private static (byte R, byte G, byte B) At(PageBitmap page, int x, int y)
    {
        var o = y * page.Stride + x * 3;
        return (page.Bgr[o + 2], page.Bgr[o + 1], page.Bgr[o]);
    }

    private static bool IsRed((byte R, byte G, byte B) c) => c.R > 200 && c.G < 70 && c.B < 70;

    private static bool IsBlue((byte R, byte G, byte B) c) => c.B > 200 && c.R < 70 && c.G < 70;

    private static bool IsWhite((byte R, byte G, byte B) c) => c.R > 245 && c.G > 245 && c.B > 245;

    /// <summary>The bounding box of the red pixels on the page.</summary>
    private static (int X, int Y, int W, int H) RedBox(PageBitmap page)
    {
        var (x0, y0, x1, y1) = (int.MaxValue, int.MaxValue, -1, -1);
        for (var y = 0; y < page.Height; y++)
        {
            for (var x = 0; x < page.Width; x++)
            {
                if (!IsRed(At(page, x, y))) continue;
                (x0, y0, x1, y1) = (Math.Min(x0, x), Math.Min(y0, y), Math.Max(x1, x), Math.Max(y1, y));
            }
        }

        return (x0, y0, x1 - x0 + 1, y1 - y0 + 1);
    }

    /// <summary>Prints one item through the queue on the Citizen-like driver and returns the page Windows would get.</summary>
    private static PageBitmap PrintOne(TempDir dir, PrintItem item, string paper, PrintFormat size)
    {
        var backend = new FakePrintBackend { CitizenDriverSizes = true };
        using var service = new PrintService(backend, new PrintHistory(dir.File("h.json")), startWorker: false);
        service.Enqueue([new PrintRequest(item, "Citizen CY-02", Paper(paper), size)]);
        service.ProcessPending();
        Assert.Equal(PrintJobState.InQueue, service.Jobs.Single().State);
        return backend.Submitted.Single().Page;
    }

    private static string Solid(TempDir dir, string name, int width, int height, MagickColor colour, MagickColor? frame = null)
    {
        using var image = new MagickImage(colour, (uint)width, (uint)height);
        if (frame is not null)
        {
            image.Settings.FillColor = MagickColors.None;
            image.Settings.StrokeColor = frame;
            image.Settings.StrokeWidth = 20;
            image.Draw(new ImageMagick.Drawing.DrawableRectangle(0, 0, width - 1, height - 1));
        }

        var path = dir.File(name);
        image.Quality = 95;
        image.Write(path, MagickFormat.Jpeg);
        return path;
    }

    [Fact]
    public void The_citizen_papers_are_recognised_with_their_bleed_and_the_plain_one_is_chosen()
    {
        var papers = FakePrintBackend.CitizenPapers.Select(p => new DriverPaper(p.Name, p.WidthMm, p.HeightMm)).ToList();
        Assert.Equal("(6x4)", papers[PaperMatcher.Best(papers, 101.6, 152.4)!.Value].Name);   // not "PR (4x6)", not "x 2"
        Assert.Equal("(6x8)", papers[PaperMatcher.Best(papers, 152.4, 203.2)!.Value].Name);
        Assert.True(PaperMatcher.IsTurned("PR (4x6)"));
        Assert.False(PaperMatcher.IsTurned("(6x4)"));
        Assert.True(PaperMatcher.IsVariant("PR (4x6) x 2"));
        Assert.True(PaperMatcher.IsVariant("(6×4) × 2"));
        Assert.True(PrintLayout.PaperMatches(104.9, 156.1, 101.6, 152.4));                     // 3.3 and 3.7 mm of bleed
        Assert.False(PrintLayout.PaperMatches(131, 181, 152.4, 203.2));                        // (5x7) is not 15×20
        Assert.False(PrintLayout.PaperMatches(156.1, 155.4, 101.6, 152.4));                    // (6x6)
        Assert.False(PrintLayout.PaperMatches(105, 148, 101.6, 152.4));                        // A6: shorter
        Assert.False(PrintLayout.PaperMatches(110, 160, 101.6, 152.4));                        // far larger: another paper
    }

    [Fact]
    public void The_finished_print_is_centred_on_the_bleed_page_in_whole_dots()
    {
        // (6x4) as the driver gives it in portrait: a wide page of 1844 × 1240 dots for a 1800 × 1200 print.
        var wide = PrintService.PageFor(new PageGeometry(300, 300, 1844, 1240, 0, 0), new PrintRequest(null!, "c", Paper("(6x4)"), PrintFormats.P10x15));
        Assert.Equal((1844, 1240, 0, 0), (wide.W, wide.H, wide.X, wide.Y));
        Assert.Equal((22, 20, 1800, 1200), Px(wide.Finished));
        var tall = PrintService.PageFor(new PageGeometry(300, 300, 1240, 1844, 0, 0), new PrintRequest(null!, "c", Paper("PR (4x6)"), PrintFormats.P10x15));
        Assert.Equal((20, 22, 1200, 1800), Px(tall.Finished));
        var big = PrintService.PageFor(new PageGeometry(300, 300, 1844, 2436, 0, 0), new PrintRequest(null!, "c", Paper("(6x8)"), PrintFormats.P15x20));
        Assert.Equal((22, 18, 1800, 2400), Px(big.Finished));
    }

    [Fact]
    public void Crop_fills_the_whole_driver_page_with_its_bleed_in_both_orientations()
    {
        using var dir = new TempDir();
        var landscape = Solid(dir, "L.jpg", 3000, 2000, MagickColors.Red);
        var portrait = Solid(dir, "P.jpg", 2000, 3000, MagickColors.Red);
        foreach (var (photo, paper) in new[] { (landscape, "(6x4)"), (portrait, "(6x4)"), (landscape, "PR (4x6)"), (portrait, "(6x8)") })
        {
            var size = paper == "(6x8)" ? PrintFormats.P15x20 : PrintFormats.P10x15;
            var page = PrintOne(dir, new PhotoPrintItem("P", photo, null, false, 1), paper, size);
            Assert.Equal(RedBox(page), (0, 0, page.Width, page.Height));                      // no white anywhere, edges included
        }
    }

    [Fact]
    public void Whole_keeps_the_entire_photo_inside_the_finished_print_and_white_only_where_needed()
    {
        using var dir = new TempDir();
        // A 4:3 photo with a blue frame: shown whole on a 3:2 print, white beside it, nothing cut off.
        var photo = Solid(dir, "W.jpg", 1600, 1200, MagickColors.Red, MagickColors.Blue);
        var page = PrintOne(dir, new PhotoPrintItem("W", photo, null, false, 1, Whole: true), "(6x4)", PrintFormats.P10x15);
        Assert.Equal((1844, 1240), (page.Width, page.Height));
        // The finished print is 1800 × 1200 at (22, 20); the photo fills its height, 1600 wide, centred: x 122 … 1721.
        Assert.True(IsBlue(At(page, 124, 620)) && IsBlue(At(page, 1719, 620)));               // both side frames are there
        Assert.True(IsBlue(At(page, 922, 22)) && IsBlue(At(page, 922, 1217)));                 // top and bottom frames too
        Assert.True(IsWhite(At(page, 70, 620)) && IsWhite(At(page, 1780, 620)));               // white beside it, inside the print
        Assert.True(IsBlue(At(page, 922, 3)) && IsBlue(At(page, 922, 1236)));                  // the edge goes on into the bleed
        Assert.True(IsWhite(At(page, 5, 620)) && IsWhite(At(page, 1838, 620)));                // and the white side stays white
    }

    [Fact]
    public void An_id_sheet_keeps_every_photo_at_its_exact_size_on_the_bleed_page()
    {
        using var sheet = new MagickImage(MagickColors.White, 1800, 1200);
        using (var dot = new MagickImage(MagickColors.Red, 354, 472)) sheet.Composite(dot, 100, 50, CompositeOperator.Over);
        using var dir = new TempDir();
        var item = new ImagePrintItem("ID 3x4", _ => (MagickImage)sheet.Clone(), 1);

        var wide = PrintOne(dir, item, "(6x4)", PrintFormats.P10x15);
        Assert.Equal((22 + 100, 20 + 50, 354, 472), RedBox(wide));                            // 1:1, inside the finished print
        var tall = PrintOne(dir, item, "PR (4x6)", PrintFormats.P10x15);
        var turned = RedBox(tall);
        Assert.Equal((472, 354), (turned.W, turned.H));                                        // turned onto the tall page, same size
    }

    [Fact]
    public void A_collage_prints_at_its_true_size_with_its_margins()
    {
        using var dir = new TempDir();
        var red = Solid(dir, "R.jpg", 1200, 1200, MagickColors.Red);
        var size = PrintFormats.P10x15;
        var slots = CollageLayout.Slots(CollageTemplates.Default, size.WidthMm, size.HeightMm, 3);
        var photos = slots.Select(_ => (CollagePhoto?)new CollagePhoto(red, NormalizedRect.Full)).ToList();
        var item = new ImagePrintItem("Collage", preview => CollageRenderer.Render(size.WidthMm, size.HeightMm, slots, photos), 1);

        var page = PrintOne(dir, item, "PR (4x6)", size);                                     // a tall page: no turning
        var first = Px(slots[0]);
        var box = RedBox(page);
        Assert.Equal((20 + first.X, 22 + first.Y), (box.X, box.Y));                            // the 3 mm margin is kept
        var run = 0;
        while (IsRed(At(page, box.X + run, box.Y + first.H / 2))) run++;
        Assert.InRange(run, first.W - 1, first.W + 1);                                         // the first slot at its size
        Assert.True(IsWhite(At(page, 5, 900)) && IsWhite(At(page, 900, 5)));                   // margins and bleed white
    }

    [Fact]
    public void A_9x13_on_its_10x15_canvas_keeps_its_size_and_a_10x15_edit_prints_borderless()
    {
        using var dir = new TempDir();
        var nine = Solid(dir, "N.jpg", 900, 1300, MagickColors.Red);
        var ten = Solid(dir, "T.jpg", 1000, 1500, MagickColors.Red);
        var saver = new PhotoSaver();
        Assert.True(saver.Save(nine, new EditRecipe { FormatId = "9x13", Orientation = CropOrientation.Portrait }, FileStamp.Of(nine)!.Value).Succeeded);
        Assert.True(saver.Save(ten, new EditRecipe { FormatId = "10x15", Orientation = CropOrientation.Portrait }, FileStamp.Of(ten)!.Value).Succeeded);

        var canvas = PrintOne(dir, new PhotoPrintItem("N", nine, null, IsEdited: true, 1), "PR (4x6)", PrintFormats.P10x15);
        var box = RedBox(canvas);
        Assert.InRange(box.W, 1063 - 3, 1063 + 3);                                             // 90 mm at 300 dpi, not enlarged
        Assert.InRange(box.H, 1535 - 3, 1535 + 3);                                             // 130 mm
        Assert.InRange(box.X, 20 + 68 - 3, 20 + 68 + 3);                                       // centred: the white borders stay

        var borderless = PrintOne(dir, new PhotoPrintItem("T", ten, null, IsEdited: true, 1), "PR (4x6)", PrintFormats.P10x15);
        Assert.Equal(RedBox(borderless), (0, 0, borderless.Width, borderless.Height));
    }

    [Fact]
    public void Bleed_extension_repeats_the_finished_edge_outwards()
    {
        using var page = new MagickImage(MagickColors.White, 100, 60);
        using (var inner = new MagickImage(MagickColors.Red, 80, 40)) page.Composite(inner, 10, 10, CompositeOperator.Over);
        PrintRenderer.ExtendIntoBleed(page, 10, 10, 80, 40);
        using var pixels = page.GetPixels();
        foreach (var (x, y) in new[] { (0, 0), (99, 0), (0, 59), (99, 59), (50, 2), (2, 30) }) Assert.True(pixels.GetPixel(x, y).ToColor()!.R > 60000 && pixels.GetPixel(x, y).ToColor()!.G < 3000);
    }
}
