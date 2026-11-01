using ImageMagick;
using PhotoX.Core.Formats;
using PhotoX.Core.Geometry;
using PhotoX.Core.Id;
using PhotoX.Imaging.Collage;
using PhotoX.Imaging.Id;
using PhotoX.Printing;
using PhotoX.Storage;

namespace PhotoX.Tests;

/// <summary>prompt_06: print history and statistics, ID 90° rotation, collage rotation, settings upgrade (TS-21).</summary>
public class Prompt06CoreTests
{
    private static readonly PrinterPaper Paper4x6 = new(1, "(6x4)", 152.4, 101.6);

    [Fact]
    public void The_print_history_keeps_every_outcome_and_counts_only_accepted_copies()
    {
        using var dir = new TempDir();
        var photos = Enumerable.Range(1, 4).Select(i => Fixtures.WriteJpeg(dir.Path, $"P{i}.jpg", 600, 400)).ToArray();
        var backend = new FakePrintBackend();
        var log = new PrintLog(dir.File("log"));
        using var service = new PrintService(backend, new PrintHistory(dir.File("counters.json")), log, startWorker: false);
        PrintRequest Req(int i, int copies, PrintFormat size) => new(new PhotoPrintItem($"P{i}.jpg", photos[i - 1], null, false, copies), "Citizen CY-02",
            size == PrintFormats.P10x15 ? Paper4x6 : new PrinterPaper(2, "(6x8)", 152.4, 203.2), size);

        var jobs = service.Enqueue([Req(1, 2, PrintFormats.P10x15), Req(2, 3, PrintFormats.P15x20), Req(3, 1, PrintFormats.P10x15), Req(4, 5, PrintFormats.P10x15)]);
        service.Cancel(jobs[3]);                                          // cancelled before Windows saw it
        service.ProcessPending();
        backend.States.Remove(jobs[0].WindowsJobId!.Value);               // finished normally
        service.Cancel(jobs[2]);                                          // cancelled in the Windows queue
        service.Poll();

        var now = DateTime.Now;
        var entries = log.Read(now.Date, now.Date.AddDays(1));
        Assert.Equal(4, entries.Count);
        Assert.Equal(PrintOutcome.Finished, entries.Single(e => e.Name == "P1.jpg").Outcome);
        Assert.Equal(PrintOutcome.Submitted, entries.Single(e => e.Name == "P2.jpg").Outcome);   // still in the Windows queue
        Assert.Equal(PrintOutcome.Canceled, entries.Single(e => e.Name == "P3.jpg").Outcome);
        Assert.Equal(PrintOutcome.Canceled, entries.Single(e => e.Name == "P4.jpg").Outcome);
        Assert.Equal(photos[0], entries.Single(e => e.Name == "P1.jpg").Path);

        var stats = log.Statistics(now);
        Assert.Equal(5, stats.Today.Prints);                               // 2 finished + 3 accepted; cancelled jobs do not count
        Assert.Equal(2, stats.Today.PrintsOf("10x15"));
        Assert.Equal(3, stats.Today.PrintsOf("15x20"));
        Assert.Equal(2, stats.Today.CanceledJobs);
        Assert.Equal(5, stats.Week.Prints);
        Assert.Equal(5, stats.Month.Prints);
        Assert.Equal(5, stats.All.Prints);

        // A new session reads the same history; a damaged last line (power loss) is skipped.
        File.AppendAllText(Directory.GetFiles(log.Directory).Single(), "{\"jobId\":\"x\",\"ti");
        Assert.Equal(5, new PrintLog(log.Directory).Statistics(now).Today.Prints);
    }

    [Fact]
    public void A_failed_job_is_in_the_history_but_not_counted()
    {
        using var dir = new TempDir();
        var photo = Fixtures.WriteJpeg(dir.Path, "P1.jpg", 600, 400);
        var backend = new FakePrintBackend { FailWith = "The printer is not available." };
        var log = new PrintLog(dir.File("log"));
        using var service = new PrintService(backend, new PrintHistory(dir.File("counters.json")), log, startWorker: false);
        service.Enqueue([new PrintRequest(new PhotoPrintItem("P1.jpg", photo, null, false, 1), "Citizen CY-02", Paper4x6, PrintFormats.P10x15)]);
        service.ProcessPending();
        var entry = log.ReadAll().Single();
        Assert.Equal((PrintOutcome.Failed, "The printer is not available."), (entry.Outcome, entry.Detail));
        Assert.Equal(1, log.Statistics(DateTime.Now).Today.FailedJobs);
        Assert.Equal(0, log.Statistics(DateTime.Now).All.Prints);
    }

    [Fact]
    public void Weeks_start_on_monday_and_periods_use_local_time()
    {
        Assert.Equal(new DateTime(2026, 10, 26), PrintLog.WeekStart(new DateTime(2026, 10, 30, 15, 0, 0)));   // Friday
        Assert.Equal(new DateTime(2026, 10, 26), PrintLog.WeekStart(new DateTime(2026, 11, 1, 23, 0, 0)));    // Sunday
        Assert.Equal(new DateTime(2026, 11, 2), PrintLog.WeekStart(new DateTime(2026, 11, 2, 0, 5, 0)));      // Monday

        using var dir = new TempDir();
        var log = new PrintLog(dir.Path);
        var monday = new DateTime(2026, 10, 26, 9, 0, 0, DateTimeKind.Local);
        log.Record(new PrintLogEntry("a", monday.AddDays(-1).ToUniversalTime(), "Sunday", "10x15", 1, "P", PrintOutcome.Finished));
        log.Record(new PrintLogEntry("b", monday.ToUniversalTime(), "Monday", "10x15", 2, "P", PrintOutcome.Finished));
        log.Record(new PrintLogEntry("c", monday.AddDays(4).ToUniversalTime(), "Friday", "15x20", 4, "P", PrintOutcome.Submitted));
        var stats = log.Statistics(monday.AddDays(4).AddHours(5));
        Assert.Equal((4, 6, 7, 7), (stats.Today.Prints, stats.Week.Prints, stats.Month.Prints, stats.All.Prints));
        Assert.Equal(["Friday", "Monday", "Sunday"], log.Read(new DateTime(2026, 10, 1), new DateTime(2026, 11, 1)).Select(e => e.Name));
    }

    [Fact]
    public void Print_counters_move_out_of_the_installation_folder_once()
    {
        using var dir = new TempDir();
        var paths = new AppPaths(dir.Path);
        Directory.CreateDirectory(paths.DataDir);
        File.WriteAllText(paths.LegacyPrintHistoryFile, "{}");
        Assert.NotEqual(paths.DataDir, paths.UserDataDir);
        paths.MigratePrintHistory();
        Assert.Equal("{}", File.ReadAllText(paths.PrintHistoryFile));
        File.WriteAllText(paths.PrintHistoryFile, "{\"X\":{}}");
        paths.MigratePrintHistory();                                       // never overwrites newer counters
        Assert.Equal("{\"X\":{}}", File.ReadAllText(paths.PrintHistoryFile));
        Assert.StartsWith(paths.UserDataDir, paths.PrintLogDir);
    }

    [Fact]
    public void Old_settings_get_quality_100_but_a_chosen_quality_stays()
    {
        Assert.Equal(100, new Settings().JpegQuality);
        var old = new Settings { Version = 1, JpegQuality = 95 };
        Assert.True(old.Upgrade());
        Assert.Equal((100, Settings.CurrentVersion), (old.JpegQuality, old.Version));
        var chosen = new Settings { Version = 1, JpegQuality = 90 };
        chosen.Upgrade();
        Assert.Equal(90, chosen.JpegQuality);
        var current = new Settings { JpegQuality = 95 };
        Assert.False(current.Upgrade());                                   // chosen in 0.9: kept
        Assert.Equal(95, current.JpegQuality);

        using var dir = new TempDir();
        var paths = new AppPaths(dir.Path);
        Directory.CreateDirectory(paths.ConfigDir);
        File.WriteAllText(paths.SettingsFile, "{\"version\":1,\"jpegQuality\":95,\"language\":\"en\"}");
        var loaded = new SettingsStore(paths).Load();
        Assert.Equal((100, "en", "light"), (loaded.JpegQuality, loaded.Language, loaded.Theme));   // light by default since prompt_07
        Assert.Contains("\"version\": 2", File.ReadAllText(paths.SettingsFile));
    }

    [Fact]
    public void Us_visa_is_shown_as_5x5_cm_and_stays_50_8_mm()
    {
        Assert.Equal("5 × 5 cm", IdSizes.S2x2in.Label);
        Assert.Equal((50.8, 50.8, 600, 600), (IdSizes.S2x2in.WidthMm, IdSizes.S2x2in.HeightMm, IdSizes.S2x2in.WidthPx, IdSizes.S2x2in.HeightPx));
        var older = IdSizes.Superseded.Last();
        Assert.Equal("2 × 2 in", older.Label);
        Assert.Equal("5 × 5 cm", IdSizes.UpgradeDefaults([older]).Sizes[0].Label);   // an unedited data file is updated
        Assert.Equal(8, SheetLayout.CapacityFor(IdSizes.S3x4));                       // eight 3×4 photos with the strip
    }

    [Fact]
    public void A_quarter_turn_frames_the_photo_as_shown()
    {
        // A landscape photo of a portrait subject lying on its side: turned right it becomes portrait.
        var size = IdSizes.S3x4;
        var turned = IdFraming.Initial(1200, 800, size, 1);
        Assert.Equal(1, turned.QuarterTurns);
        Assert.Equal(1200 * 0.8 * size.Ratio, turned.FrameWidthSource, 6);           // 80 % of the shown height (1200)
        Assert.Equal((600.0, 400.0), (turned.CenterX, turned.CenterY));
        Assert.Equal(-90, turned.Angle);
        Assert.Equal(3, IdFraming.Initial(1200, 800, size, -1).QuarterTurns);

        Assert.Equal((10.0, 800 - 30.0), IdFraming.Unturn(30, 10, 1200, 800, 1));   // shown (30, 10) is stored (10, 770)
        Assert.Equal((1200 - 30.0, 800 - 10.0), IdFraming.Unturn(30, 10, 1200, 800, 2));
        Assert.Equal((1200 - 10.0, 30.0), IdFraming.Unturn(30, 10, 1200, 800, 3));

        // Rendering: the left half of the stored photo is red, the right half blue. Turned right (clockwise), the
        // left edge goes to the top, so the top of the ID photo is red and the bottom blue.
        using var photo = new MagickImage(MagickColors.Blue, 1200, 800);
        using (var red = new MagickImage(MagickColors.Red, 600, 800)) photo.Composite(red, 0, 0, CompositeOperator.Over);
        using var output = IdRenderer.Render(photo, turned, size, new IdAdjustments());
        Assert.Equal((354u, 472u), (output.Width, output.Height));
        using var pixels = output.GetPixels();
        var top = pixels.GetPixel(177, 40)!.ToColor()!;
        var bottom = pixels.GetPixel(177, 430)!.ToColor()!;
        Assert.True(top.R > 50000 && top.B < 10000, "top is red");
        Assert.True(bottom.B > 50000 && bottom.R < 10000, "bottom is blue");
    }

    [Fact]
    public void A_turned_collage_photo_is_rendered_turned()
    {
        using var dir = new TempDir();
        var path = dir.File("p.png");
        using (var photo = new MagickImage(MagickColors.Blue, 600, 400))
        {
            using var red = new MagickImage(MagickColors.Red, 300, 400);
            photo.Composite(red, 0, 0, CompositeOperator.Over);
            photo.Write(path);
        }

        // One portrait slot filling a 10×15 page; the photo turned right shows red on top.
        var slots = new List<RectD> { new(0, 0, 101.6, 152.4) };
        using var page = CollageRenderer.Render(101.6, 152.4, slots, [new CollagePhoto(path, new NormalizedRect(0, 0, 1, 1), 1)], 30);
        using var pixels = page.GetPixels();
        Assert.True(pixels.GetPixel(60, 20)!.ToColor()!.R > 50000);
        Assert.True(pixels.GetPixel(60, 160)!.ToColor()!.B > 50000);
    }
}
