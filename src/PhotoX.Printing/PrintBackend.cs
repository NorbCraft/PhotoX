using PhotoX.Imaging.Printing;

namespace PhotoX.Printing;

public sealed record PrinterInfo(string Name, bool IsDefault);

/// <summary>A paper the printer driver offers, with its size in millimetres (as the driver reports it).</summary>
public sealed record PrinterPaper(short Id, string Name, double WidthMm, double HeightMm);

/// <summary>Page geometry of a printer and paper in device pixels (Windows: <c>GetDeviceCaps</c>).</summary>
public sealed record PageGeometry(int DpiX, int DpiY, int PhysicalWidthPx, int PhysicalHeightPx, int OffsetXPx, int OffsetYPx);

/// <summary>One job handed to the printing system: a page printed <see cref="Copies"/> times at a device position.</summary>
public sealed record PrintSubmission(string Printer, PrinterPaper Paper, byte[]? Settings, string DocumentName, PageBitmap Page, int X, int Y, int Copies, string? OutputFile = null);

public enum SpoolStatus
{
    /// <summary>Waiting or being spooled in the Windows queue.</summary>
    Queued,

    Printing,

    /// <summary>Windows reports a problem: error, offline, paper out, paused or user intervention.</summary>
    Attention,

    /// <summary>Windows marked the job printed; it leaves the queue soon.</summary>
    Printed,

    /// <summary>The job is being deleted (cancelled in Windows or by PhotoX).</summary>
    Deleting,

    /// <summary>The job is no longer in the Windows queue.</summary>
    Gone,
}

public sealed record SpoolState(SpoolStatus Status, string? Detail = null);

/// <summary>Maps the Windows spooler's <c>JOB_INFO_1.Status</c> flags to <see cref="SpoolStatus"/>.</summary>
public static class SpoolStatusMap
{
    public static SpoolState FromJobStatus(uint status, string? text)
    {
        const uint paused = 0x1, error = 0x2, deleting = 0x4, printing = 0x10, offline = 0x20, paperOut = 0x40, printed = 0x80;
        const uint deleted = 0x100, blocked = 0x200, userIntervention = 0x400, complete = 0x1000;
        if ((status & (deleting | deleted)) != 0) return new SpoolState(SpoolStatus.Deleting, text);
        if ((status & (error | offline | paperOut | blocked | userIntervention | paused)) != 0)
        {
            var reason = (status & paperOut) != 0 ? "paper-out" : (status & offline) != 0 ? "offline" : (status & paused) != 0 ? "paused"
                : (status & userIntervention) != 0 ? "intervention" : "error";
            return new SpoolState(SpoolStatus.Attention, string.IsNullOrWhiteSpace(text) ? reason : text);
        }

        if ((status & (printed | complete)) != 0) return new SpoolState(SpoolStatus.Printed);
        return new SpoolState((status & printing) != 0 ? SpoolStatus.Printing : SpoolStatus.Queued);
    }
}

/// <summary>The printing system could not take a job; <see cref="Exception.Message"/> is the system's reason.</summary>
public sealed class PrintException(string message) : Exception(message);

/// <summary>
/// The operating system's printing (prompt_05 §2). Windows uses the installed driver through the standard print
/// spooler (<see cref="WindowsPrintBackend"/>); other platforms have no direct printing. Tests use a fake.
/// </summary>
public interface IPrintBackend
{
    bool IsSupported { get; }

    IReadOnlyList<PrinterInfo> GetPrinters();

    IReadOnlyList<PrinterPaper> GetPapers(string printer);

    PageGeometry GetGeometry(string printer, PrinterPaper paper, byte[]? settings);

    /// <summary>Hands the job to the printing system and returns its job id once accepted. Throws <see cref="PrintException"/>.</summary>
    int Submit(PrintSubmission submission);

    SpoolState Query(string printer, int jobId);

    bool Cancel(string printer, int jobId);

    /// <summary>Shows the driver's own settings (quality, finish, cutting …); returns the new settings, or null if cancelled.</summary>
    byte[]? EditSettings(string printer, byte[]? settings, IntPtr owner);
}

/// <summary>Platforms without direct printing (Linux development): no printers.</summary>
public sealed class UnavailablePrintBackend : IPrintBackend
{
    public bool IsSupported => false;

    public IReadOnlyList<PrinterInfo> GetPrinters() => [];

    public IReadOnlyList<PrinterPaper> GetPapers(string printer) => [];

    public PageGeometry GetGeometry(string printer, PrinterPaper paper, byte[]? settings) => throw new PrintException("Direct printing is available on Windows.");

    public int Submit(PrintSubmission submission) => throw new PrintException("Direct printing is available on Windows.");

    public SpoolState Query(string printer, int jobId) => new(SpoolStatus.Gone);

    public bool Cancel(string printer, int jobId) => false;

    public byte[]? EditSettings(string printer, byte[]? settings, IntPtr owner) => null;
}
