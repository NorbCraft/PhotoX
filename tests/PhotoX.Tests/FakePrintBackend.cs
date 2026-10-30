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

    public IReadOnlyList<PrinterPaper> GetPapers(string printer) => printer.Contains("PDF")
        ? [new(9, "A4", 210, 297), new(1, "Letter", 215.9, 279.4)]
        : [new(1, "(6x4)", 152.4, 101.6), new(2, "(6x8)", 152.4, 203.2), new(3, "(6x8) 2-up", 152.4, 203.2)];

    public PageGeometry GetGeometry(string printer, PrinterPaper paper, byte[]? settings)
    {
        var (w, h) = (Math.Min(paper.WidthMm, paper.HeightMm), Math.Max(paper.WidthMm, paper.HeightMm)); // portrait
        return new PageGeometry(Dpi, Dpi, (int)Math.Round(w * Dpi / 25.4), (int)Math.Round(h * Dpi / 25.4), Offset, Offset);
    }

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
