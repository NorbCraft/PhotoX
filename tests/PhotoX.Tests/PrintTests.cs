using ImageMagick;
using PhotoX.Core.Editing;
using PhotoX.Core.Formats;
using PhotoX.Core.Geometry;
using PhotoX.Core.Id;
using PhotoX.Core.Printing;
using PhotoX.Imaging;
using PhotoX.Imaging.Id;
using PhotoX.Printing;
using PhotoX.Storage;

namespace PhotoX.Tests;

/// <summary>prompt_05 §§2–6 without the Citizen printer: layout, page geometry, content, queue and counters (TS-20).</summary>
public class PrintTests
{
    private static readonly PrinterPaper Paper4x6 = new(1, "(6x4)", 152.4, 101.6);
    private static readonly PrinterPaper A4 = new(9, "A4", 210, 297);

    [Fact]
    public void An_untouched_landscape_photo_is_turned_and_fills_the_paper()
    {
        var p = PrintLayout.Place(4000, 3000, 101.6, 152.4, cropToFill: true);
        Assert.True(p.Rotate);
        Assert.Equal(new RectD(0, 0, 101.6, 152.4), p.TargetMm);
        // Turned, the photo is 3000 × 4000 (3:4); the 2:3 paper needs 2667 × 4000 of it, centred.
        Assert.Equal(1, p.SourceCrop.Height, 6);
        Assert.Equal(4000 * 2.0 / 3 / 3000, p.SourceCrop.Width, 4);
        Assert.Equal((1 - 4000 * 2.0 / 3 / 3000) / 2, p.SourceCrop.X, 4);
    }

    [Fact]
    public void Edited_photos_keep_every_pixel_unless_the_ratio_matches()
    {
        // A 15×20 (3:4) edit on 10×15 paper: fitted, white above and below.
        var fit = PrintLayout.Place(3000, 4000, 101.6, 152.4, cropToFill: false);
        Assert.False(fit.Rotate);
        Assert.Equal(NormalizedRect.Full, fit.SourceCrop);
        Assert.Equal(101.6, fit.TargetMm.Width, 6);
        Assert.Equal(101.6 * 4 / 3, fit.TargetMm.Height, 6);
        Assert.Equal((152.4 - 101.6 * 4 / 3) / 2, fit.TargetMm.Y, 6);

        // A 10×15 edit (a pixel off 2:3) fills the paper.
        var fill = PrintLayout.Place(2001, 3000, 101.6, 152.4, cropToFill: false);
        Assert.Equal(new RectD(0, 0, 101.6, 152.4), fill.TargetMm);
        Assert.True(fill.SourceCrop.Width > 0.999);
    }

    [Fact]
    public void The_page_covers_the_matching_paper_or_is_centred_at_true_size()
    {
        var size = PrintFormats.P10x15;
        var citizen = new PageGeometry(300, 300, 1240, 1844, 10, 12);   // a driver page with a little bleed
        var on4x6 = PrintService.PageFor(citizen, new PrintRequest(null!, "c", Paper4x6, size));
        Assert.Equal((1240, 1844, -10, -12), (on4x6.W, on4x6.H, on4x6.X, on4x6.Y));

        var pdf = new PageGeometry(600, 600, 4960, 7016, 0, 0);          // A4 at 600 dpi
        var onA4 = PrintService.PageFor(pdf, new PrintRequest(null!, "p", A4, size));
        Assert.Equal((2400, 3600), (onA4.W, onA4.H));                   // 101.6 × 152.4 mm at 600 dpi
        Assert.Equal(((4960 - 2400) / 2, (7016 - 3600) / 2), (onA4.X, onA4.Y));
        Assert.Throws<PrintException>(() => PrintService.PageFor(new PageGeometry(300, 300, 1000, 1000, 0, 0), new PrintRequest(null!, "x", new PrinterPaper(5, "small", 85, 85), size)));
    }

    [Fact]
    public void An_unsaved_edit_prints_like_a_save_and_nothing_is_written()
    {
        using var dir = new TempDir();
        var photo = Fixtures.WriteJpeg(dir.Path, "P1.jpg", 900, 600);
        Directory.CreateDirectory(dir.File("copy"));
        var copy = Fixtures.WriteJpeg(dir.File("copy"), "P1.jpg", 900, 600);
        var recipe = new EditRecipe { Orientation = CropOrientation.Landscape, Crop = new NormalizedRect(0.1, 0.2, 0.6, 0.6), Exposure = 0.5, Saturation = 30 };
        var before = Fixtures.Sha(photo);

        var (content, fit) = PrintContent.Load(new PhotoPrintItem("P1.jpg", photo, recipe, IsEdited: false, 1), preview: false);
        using (content)
        {
            Assert.Equal(PrintFit.Borderless, fit);
            Assert.True(new PhotoSaver().Save(copy, recipe, FileStamp.Of(copy)!.Value).Succeeded);
            using var saved = new MagickImage(copy);
            Assert.Equal((saved.Width, saved.Height), (content.Width, content.Height));
            Assert.True(saved.Compare(content, ErrorMetric.MeanAbsolute) < 0.01); // JPEG q95 apart
        }

        Assert.Equal(before, Fixtures.Sha(photo));
        Assert.Equal(["P1.jpg"], Directory.GetFileSystemEntries(dir.Path).Select(Path.GetFileName).Where(n => n != "copy"));
    }

    [Fact]
    public void An_id_sheet_prints_pixel_for_pixel_on_4x6_paper()
    {
        using var sheet = new MagickImage(MagickColors.White, 1800, 1200);
        using (var dot = new MagickImage(MagickColors.Red, 354, 472)) sheet.Composite(dot, 100, 50, CompositeOperator.Over);
        var item = new ImagePrintItem("ID 3x4", _ => (MagickImage)sheet.Clone(), 2);
        var backend = new FakePrintBackend();
        using var service = new PrintService(backend, new PrintHistory(Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".json")), startWorker: false);

        service.Enqueue([new PrintRequest(item, "Citizen CY-02", Paper4x6, PrintFormats.P10x15)]);
        service.ProcessPending();

        var s = Assert.Single(backend.Submitted);
        Assert.Equal((1200, 1800, 2), (s.Page.Width, s.Page.Height, s.Copies)); // turned upright, 300 dpi, no scaling
        // The 354 × 472 photo is still 354 × 472 device pixels (turned): count red pixels in the page.
        var red = 0;
        for (var i = 0; i < s.Page.Height; i++)
        {
            for (var x = 0; x < s.Page.Width; x++)
            {
                var o = i * s.Page.Stride + x * 3;
                if (s.Page.Bgr[o + 2] > 200 && s.Page.Bgr[o + 1] < 60) red++;
            }
        }

        Assert.InRange(red, 354 * 472 - 400, 354 * 472 + 400);
        Assert.Equal(PrintJobState.InQueue, service.Jobs.Single().State);
    }

    [Fact]
    public void Counters_change_only_for_jobs_windows_accepted_and_are_taken_back_when_cancelled()
    {
        using var dir = new TempDir();
        var photos = Enumerable.Range(1, 4).Select(i => Fixtures.WriteJpeg(dir.Path, $"P{i}.jpg", 600, 400)).ToArray();
        var hashes = photos.Select(Fixtures.Sha).ToArray();
        var backend = new FakePrintBackend();
        var historyFile = dir.File("print-history.json");
        var history = new PrintHistory(historyFile);
        using var service = new PrintService(backend, history, startWorker: false);
        PrintRequest Req(int i, int copies) => new(new PhotoPrintItem($"P{i}.jpg", photos[i - 1], null, false, copies), "Citizen CY-02", Paper4x6, PrintFormats.P10x15);

        var jobs = service.Enqueue([Req(1, 2), Req(2, 1), Req(3, 3), Req(4, 1)]);
        service.Cancel(jobs[3]);                                         // cancelled before it reached Windows
        Assert.Equal(0, history.CountFor(photos[0]));                    // pressing Print does not count
        service.ProcessPending();
        Assert.Equal([2, 1, 3, 0], photos.Select(history.CountFor));

        backend.States[jobs[0].WindowsJobId!.Value] = new SpoolState(SpoolStatus.Printing);
        backend.States[jobs[1].WindowsJobId!.Value] = new SpoolState(SpoolStatus.Attention, "paper-out");
        service.Poll();
        Assert.Equal(PrintJobState.InQueue, jobs[0].State);
        Assert.Equal((PrintJobState.Attention, "paper-out"), (jobs[1].State, jobs[1].Detail));

        service.Cancel(jobs[1]);                                         // deleted in the Windows queue
        Assert.Equal(PrintJobState.Canceled, jobs[1].State);
        backend.States[jobs[2].WindowsJobId!.Value] = new SpoolState(SpoolStatus.Deleting);
        service.Poll();
        backend.States.Remove(jobs[2].WindowsJobId!.Value);               // removed outside PhotoX
        backend.States.Remove(jobs[0].WindowsJobId!.Value);               // left the queue normally
        service.Poll();

        Assert.Equal(PrintJobState.Finished, jobs[0].State);
        Assert.Equal(PrintJobState.Removed, jobs[2].State);
        Assert.Equal(PrintJobState.Canceled, jobs[3].State);
        Assert.Equal([2, 0, 0, 0], photos.Select(history.CountFor));
        Assert.Equal(2, new PrintHistory(historyFile).CountFor(photos[0])); // kept for the next session
        Assert.Equal(hashes, photos.Select(Fixtures.Sha));                  // printing never touches the photos
        Assert.False(Directory.Exists(Path.Combine(dir.Path, "Original")));
        Assert.Equal(["P1.jpg", "P2.jpg", "P3.jpg", "P4.jpg", "print-history.json"], Directory.GetFiles(dir.Path).Select(Path.GetFileName).Order());
    }

    [Fact]
    public void A_refused_job_fails_without_counting_and_is_never_resubmitted()
    {
        using var dir = new TempDir();
        var photo = Fixtures.WriteJpeg(dir.Path, "P1.jpg", 600, 400);
        var backend = new FakePrintBackend { FailWith = "The printer is offline." };
        var history = new PrintHistory(dir.File("h.json"));
        using var service = new PrintService(backend, history, startWorker: false);
        var job = service.Enqueue([new PrintRequest(new PhotoPrintItem("P1.jpg", photo, null, false, 1), "Citizen CY-02", Paper4x6, PrintFormats.P10x15)]).Single();

        service.ProcessPending();
        backend.FailWith = null;
        service.ProcessPending();
        service.Poll();

        Assert.Equal((PrintJobState.Failed, "The printer is offline."), (job.State, job.Detail));
        Assert.Empty(backend.Submitted);
        Assert.Equal(0, history.CountFor(photo));
        Assert.False(job.CanCancel);
    }

    [Fact]
    public void A_cancel_that_windows_refuses_keeps_the_count()
    {
        using var dir = new TempDir();
        var photo = Fixtures.WriteJpeg(dir.Path, "P1.jpg", 600, 400);
        var backend = new FakePrintBackend { CancelSucceeds = false };
        var history = new PrintHistory(dir.File("h.json"));
        using var service = new PrintService(backend, history, startWorker: false);
        var job = service.Enqueue([new PrintRequest(new PhotoPrintItem("P1.jpg", photo, null, false, 2), "Citizen CY-02", Paper4x6, PrintFormats.P10x15)]).Single();
        service.ProcessPending();

        service.Cancel(job);

        Assert.Equal((PrintJobState.InQueue, "cancel-failed"), (job.State, job.Detail));
        Assert.Equal(2, history.CountFor(photo));
    }

    [Fact]
    public void Spooler_status_flags_map_to_queue_states()
    {
        Assert.Equal(SpoolStatus.Queued, SpoolStatusMap.FromJobStatus(0x8, null).Status);         // spooling
        Assert.Equal(SpoolStatus.Printing, SpoolStatusMap.FromJobStatus(0x10, null).Status);
        Assert.Equal(new SpoolState(SpoolStatus.Attention, "paper-out"), SpoolStatusMap.FromJobStatus(0x10 | 0x40, null));
        Assert.Equal(new SpoolState(SpoolStatus.Attention, "Ribbon empty"), SpoolStatusMap.FromJobStatus(0x2, "Ribbon empty"));
        Assert.Equal(SpoolStatus.Deleting, SpoolStatusMap.FromJobStatus(0x4 | 0x2, null).Status);
        Assert.Equal(SpoolStatus.Printed, SpoolStatusMap.FromJobStatus(0x80, null).Status);
    }

    /// <summary>
    /// Windows only, with a virtual printer: the real spooler path (driver papers, geometry, GDI page, job id, queue).
    /// The PDF printer writes to a file given in the job, so no dialog appears.
    /// </summary>
    [Fact]
    public void Windows_virtual_printer_accepts_a_photo_at_true_size()
    {
        Assert.SkipUnless(OperatingSystem.IsWindows(), "Direct printing uses the Windows spooler.");
        if (!OperatingSystem.IsWindows()) return;
        var backend = new WindowsPrintBackend();
        var printers = backend.GetPrinters();
        var pdf = printers.FirstOrDefault(p => p.Name.Contains("PDF", StringComparison.OrdinalIgnoreCase));
        Assert.SkipWhen(pdf is null, "No PDF printer installed; printers: " + string.Join(", ", printers.Select(p => p.Name)));
        var papers = backend.GetPapers(pdf!.Name);
        var paper = papers.FirstOrDefault(p => p.Name.Contains("A4", StringComparison.OrdinalIgnoreCase)) ?? papers.First(p => p.WidthMm >= 110 && p.HeightMm >= 160);

        using var dir = new TempDir();
        var photo = Fixtures.WriteJpeg(dir.Path, "P1.jpg", 900, 600);
        var output = dir.File("out.pdf");
        var history = new PrintHistory(dir.File("h.json"));
        using var service = new PrintService(backend, history, startWorker: false);
        var job = service.Enqueue([new PrintRequest(new PhotoPrintItem("P1.jpg", photo, null, false, 2), pdf.Name, paper, PrintFormats.P10x15, OutputFile: output)]).Single();
        service.ProcessPending();

        Assert.True(job.State == PrintJobState.InQueue, $"{job.State}: {job.Detail}");
        Assert.Equal(2, history.CountFor(photo));
        var until = DateTime.UtcNow.AddSeconds(60);
        while (job.State is PrintJobState.InQueue or PrintJobState.Attention && DateTime.UtcNow < until)
        {
            Thread.Sleep(250);
            service.Poll();
        }

        Assert.Equal(PrintJobState.Finished, job.State);
        Assert.True(File.Exists(output) && new FileInfo(output).Length > 1000, "the PDF was written");
        var pdfText = System.Text.Encoding.Latin1.GetString(File.ReadAllBytes(output));
        Assert.Equal(2, System.Text.RegularExpressions.Regex.Matches(pdfText, @"/Type\s*/Page[^s]").Count); // two copies
    }
}
