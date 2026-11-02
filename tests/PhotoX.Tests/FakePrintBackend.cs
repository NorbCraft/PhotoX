using PhotoX.Printing;

namespace PhotoX.Tests;

/// <summary>A printing system for tests: records submitted pages, and job states are set by the test.</summary>
public sealed class FakePrintBackend : IPrintBackend
{
    private int _nextJob = 100;

    public int Dpi { get; set; } = 300;

    /// <summary>Non-printable margin reported by the "driver", in device pixels.</summary>
    public int Offset { get; set; }

    public string? FailWith { get; set; }

    public bool CancelSucceeds { get; set; } = true;

    public List<PrintSubmission> Submitted { get; } = [];

    public Dictionary<int, SpoolState> States { get; } = [];

    public bool IsSupported => true;

    public IReadOnlyList<PrinterInfo> GetPrinters() => [new("Citizen CY-02", true), new("Microsoft Print to PDF", false)];

    /// <summary>
    /// The papers the owner's Citizen CY-02 driver lists (prompt_11 §1), in its order, with the borderless pages and
    /// their bleed; otherwise a simplified driver that reports the nominal sizes.
    /// </summary>
    public bool CitizenDriverSizes { get; set; }

    /// <summary>The Citizen CY-02 driver's papers as listed in Windows (tenths of a millimetre, as DeviceCapabilities gives them).</summary>
    public static IReadOnlyList<PrinterPaper> CitizenPapers { get; } =
    [
        new(1, "PR (3.5x5)", 92.1, 131), new(2, "PR (3.5x5) x 2", 92.1, 131), new(3, "PR (4x6)", 104.9, 156.1), new(4, "PR (4x6) x 2", 104.9, 156.1),
        new(5, "(5x3.5)", 131, 92.1), new(6, "(6x4)", 156.1, 104.9), new(7, "(5x7)", 131, 181), new(8, "(6x8)", 156.1, 206.2),
        new(9, "(6x4) x 2", 156.1, 104.9), new(10, "(5x3.5) x 2", 131, 92.1), new(11, "(5x5)", 131, 130.3), new(12, "(6x6)", 156.1, 155.4),
    ];

    public IReadOnlyList<PrinterPaper> GetPapers(string printer) => printer.Contains("PDF")
        ? [new(9, "A4", 210, 297), new(1, "Letter", 215.9, 279.4)]
        : CitizenDriverSizes ? CitizenPapers
        : [new(1, "(6x4)", 152.4, 101.6), new(2, "(6x8)", 152.4, 203.2), new(3, "(6x8) 2-up", 152.4, 203.2)];

    public PageGeometry GetGeometry(string printer, PrinterPaper paper, byte[]? settings)
    {
        // The Citizen's pages in dots at 300 dpi, as its manual lists them: (6x4) is 1844 × 1240, (6x8) 1844 × 2436,
        // in the paper's own orientation ((6x4) wide, "PR (4x6)" tall); the simplified driver gives portrait pages.
        if (CitizenDriverSizes && !printer.Contains("PDF"))
        {
            var (cw, ch) = (CitizenDots(paper.WidthMm), CitizenDots(paper.HeightMm));
            return new PageGeometry(300, 300, cw, ch, Offset, Offset);
        }

        var (w, h) = (Math.Min(paper.WidthMm, paper.HeightMm), Math.Max(paper.WidthMm, paper.HeightMm)); // portrait
        return new PageGeometry(Dpi, Dpi, (int)Math.Round(w * Dpi / 25.4), (int)Math.Round(h * Dpi / 25.4), Offset, Offset);
    }

    /// <summary>The Citizen manual's dot counts for its page sides (156.1 mm = 1844, 104.9/105.0 mm = 1240, 206.2 mm = 2436 …).</summary>
    private static int CitizenDots(double mm) => mm switch
    {
        > 156 and < 156.2 => 1844,
        > 104.8 and < 105.1 => 1240,
        > 206.1 and < 206.3 => 2436,
        > 130.9 and < 131.2 => 1548,
        > 92 and < 92.2 => 1088,
        > 180.9 and < 181.1 => 2138,
        > 155.3 and < 155.5 => 1836,
        > 130.2 and < 130.5 => 1540,
        _ => (int)Math.Round(mm * 300 / 25.4),
    };

    public int Submit(PrintSubmission submission)
    {
        lock (Submitted)
        {
            if (FailWith is not null) throw new PrintException(FailWith);
            Submitted.Add(submission);
            var id = ++_nextJob;
            States[id] = new SpoolState(SpoolStatus.Queued);
            return id;
        }
    }

    public SpoolState Query(string printer, int jobId)
    {
        lock (Submitted) return States.GetValueOrDefault(jobId, new SpoolState(SpoolStatus.Gone));
    }

    public bool Cancel(string printer, int jobId)
    {
        lock (Submitted)
        {
            if (!CancelSucceeds) return false;
            States[jobId] = new SpoolState(SpoolStatus.Gone);
            return true;
        }
    }

    public byte[]? EditSettings(string printer, byte[]? settings, IntPtr owner) => null;
}
