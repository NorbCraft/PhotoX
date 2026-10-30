using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace PhotoX.Printing;

/// <summary>
/// Printing through the installed Windows driver (prompt_05 §2, DEC-057): the standard print spooler and GDI, so any
/// printer with a Windows driver works (the Citizen CY-02, or Microsoft Print to PDF for testing), without LinkServer
/// and without the USB protocol.
/// <list type="bullet">
/// <item>Papers come from the driver (<c>DeviceCapabilities</c>); PhotoX selects one by number and sets portrait
/// orientation and one copy in the driver's settings (<c>DEVMODE</c>). Everything else (quality, finish, cutting,
/// colour handling) stays as the driver has it: Windows' printing preferences, or what the operator chose in the
/// driver's own dialog (<see cref="EditSettings"/>).</item>
/// <item>A job is one document: the page bitmap is sent with <c>StretchDIBits</c> at 1:1 in bands, once per copy.
/// <c>StartDoc</c> returns the spooler job id, which <see cref="Query"/> and <see cref="Cancel"/> use.</item>
/// <item>Colours are sent as sRGB without a printer profile from PhotoX, so the driver does the only conversion.</item>
/// </list>
/// </summary>
[SupportedOSPlatform("windows")]
public sealed partial class WindowsPrintBackend : IPrintBackend
{
    private const uint PrinterEnumLocal = 0x2;
    private const uint PrinterEnumConnections = 0x4;
    private const ushort DcPapers = 2;
    private const ushort DcPaperSize = 3;
    private const ushort DcPaperNames = 16;
    private const uint DmOutBuffer = 2;
    private const uint DmInPrompt = 4;
    private const uint DmInBuffer = 8;
    private const int IdOk = 1;
    private const uint DmOrientation = 0x1;
    private const uint DmPaperSizeField = 0x2;
    private const uint DmPaperLength = 0x4;
    private const uint DmPaperWidth = 0x8;
    private const uint DmCopies = 0x100;
    private const uint DmFormName = 0x10000;
    private const int OffsetFields = 72;
    private const int OffsetOrientation = 76;
    private const int OffsetPaperSize = 78;
    private const int OffsetCopies = 86;
    private const int LogPixelsX = 88;
    private const int LogPixelsY = 90;
    private const int PhysicalWidth = 110;
    private const int PhysicalHeight = 111;
    private const int PhysicalOffsetX = 112;
    private const int PhysicalOffsetY = 113;
    private const uint DibRgbColors = 0;
    private const uint SrcCopy = 0x00CC0020;
    private const uint JobControlDelete = 5;
    private const int BandRows = 256;

    public bool IsSupported => true;

    public IReadOnlyList<PrinterInfo> GetPrinters()
    {
        var defaultName = DefaultPrinter();
        var names = new List<string>();
        EnumPrintersW(PrinterEnumLocal | PrinterEnumConnections, null, 4, IntPtr.Zero, 0, out var needed, out _);
        if (needed == 0) return [];
        var buffer = Marshal.AllocHGlobal((int)needed);
        try
        {
            if (!EnumPrintersW(PrinterEnumLocal | PrinterEnumConnections, null, 4, buffer, needed, out _, out var count)) throw Error("EnumPrinters");
            var size = Marshal.SizeOf<PrinterInfo4>();
            for (var i = 0; i < count; i++)
            {
                var info = Marshal.PtrToStructure<PrinterInfo4>(buffer + i * size);
                if (Marshal.PtrToStringUni(info.PrinterName) is { Length: > 0 } name) names.Add(name);
            }
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }

        return names.Distinct().Select(n => new PrinterInfo(n, string.Equals(n, defaultName, StringComparison.OrdinalIgnoreCase))).ToList();
    }

    public IReadOnlyList<PrinterPaper> GetPapers(string printer)
    {
        var count = DeviceCapabilitiesW(printer, null, DcPapers, IntPtr.Zero, IntPtr.Zero);
        if (count <= 0) return [];
        var ids = Marshal.AllocHGlobal(count * 2);
        var sizes = Marshal.AllocHGlobal(count * 8);
        var names = Marshal.AllocHGlobal(count * 64 * 2);
        try
        {
            DeviceCapabilitiesW(printer, null, DcPapers, ids, IntPtr.Zero);
            DeviceCapabilitiesW(printer, null, DcPaperSize, sizes, IntPtr.Zero);
            var hasNames = DeviceCapabilitiesW(printer, null, DcPaperNames, names, IntPtr.Zero) == count;
            var papers = new List<PrinterPaper>(count);
            for (var i = 0; i < count; i++)
            {
                var id = Marshal.ReadInt16(ids, i * 2);
                var w = Marshal.ReadInt32(sizes, i * 8) / 10.0;      // tenths of a millimetre
                var h = Marshal.ReadInt32(sizes, i * 8 + 4) / 10.0;
                var name = hasNames ? Marshal.PtrToStringUni(names + i * 128, 64).TrimEnd('\0') : $"#{id}";
                if (w > 0 && h > 0) papers.Add(new PrinterPaper(id, name, w, h));
            }

            return papers;
        }
        finally
        {
            Marshal.FreeHGlobal(ids);
            Marshal.FreeHGlobal(sizes);
            Marshal.FreeHGlobal(names);
        }
    }

    public PageGeometry GetGeometry(string printer, PrinterPaper paper, byte[]? settings)
    {
        using var devMode = DevMode.For(printer, paper, settings);
        var hdc = CreateDCW(null, printer, null, devMode.Pointer);
        if (hdc == IntPtr.Zero) throw Error("CreateDC");
        try
        {
            return new PageGeometry(
                GetDeviceCaps(hdc, LogPixelsX), GetDeviceCaps(hdc, LogPixelsY),
                GetDeviceCaps(hdc, PhysicalWidth), GetDeviceCaps(hdc, PhysicalHeight),
                GetDeviceCaps(hdc, PhysicalOffsetX), GetDeviceCaps(hdc, PhysicalOffsetY));
        }
        finally
        {
            DeleteDC(hdc);
        }
    }

    public int Submit(PrintSubmission s)
    {
        using var devMode = DevMode.For(s.Printer, s.Paper, s.Settings);
        var hdc = CreateDCW(null, s.Printer, null, devMode.Pointer);
        if (hdc == IntPtr.Zero) throw Error("CreateDC");
        var docName = Marshal.StringToHGlobalUni(s.DocumentName);
        var output = s.OutputFile is null ? IntPtr.Zero : Marshal.StringToHGlobalUni(s.OutputFile);
        var bits = GCHandle.Alloc(s.Page.Bgr, GCHandleType.Pinned);
        var header = Marshal.AllocHGlobal(40);
        var started = false;
        try
        {
            var doc = new DocInfo { Size = Marshal.SizeOf<DocInfo>(), DocName = docName, Output = output };
            var jobId = StartDocW(hdc, ref doc);
            if (jobId <= 0) throw Error("StartDoc");
            started = true;
            for (var copy = 0; copy < s.Copies; copy++)
            {
                if (StartPage(hdc) <= 0) throw Error("StartPage");
                for (var y0 = 0; y0 < s.Page.Height; y0 += BandRows)
                {
                    var rows = Math.Min(BandRows, s.Page.Height - y0);
                    WriteHeader(header, s.Page.Width, rows);
                    var ptr = bits.AddrOfPinnedObject() + y0 * s.Page.Stride;
                    if (StretchDIBits(hdc, s.X, s.Y + y0, s.Page.Width, rows, 0, 0, s.Page.Width, rows, ptr, header, DibRgbColors, SrcCopy) == 0) throw Error("StretchDIBits");
                }

                if (EndPage(hdc) <= 0) throw Error("EndPage");
            }

            if (EndDoc(hdc) <= 0) throw Error("EndDoc");
            started = false;
            return jobId;
        }
        finally
        {
            if (started) AbortDoc(hdc);
            Marshal.FreeHGlobal(header);
            bits.Free();
            Marshal.FreeHGlobal(docName);
            if (output != IntPtr.Zero) Marshal.FreeHGlobal(output);
            DeleteDC(hdc);
        }
    }

    public SpoolState Query(string printer, int jobId)
    {
        if (!OpenPrinterW(printer, out var h, IntPtr.Zero)) return new SpoolState(SpoolStatus.Gone);
        try
        {
            GetJobW(h, (uint)jobId, 1, IntPtr.Zero, 0, out var needed);
            if (needed == 0) return new SpoolState(SpoolStatus.Gone); // the job no longer exists
            var buffer = Marshal.AllocHGlobal((int)needed);
            try
            {
                if (!GetJobW(h, (uint)jobId, 1, buffer, needed, out _)) return new SpoolState(SpoolStatus.Gone);
                var info = Marshal.PtrToStructure<JobInfo1>(buffer);
                return SpoolStatusMap.FromJobStatus(info.Status, Marshal.PtrToStringUni(info.StatusText));
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
            }
        }
        finally
        {
            ClosePrinter(h);
        }
    }

    public bool Cancel(string printer, int jobId)
    {
        if (!OpenPrinterW(printer, out var h, IntPtr.Zero)) return false;
        try
        {
            return SetJobW(h, (uint)jobId, 0, IntPtr.Zero, JobControlDelete);
        }
        finally
        {
            ClosePrinter(h);
        }
    }

    public byte[]? EditSettings(string printer, byte[]? settings, IntPtr owner)
    {
        if (!OpenPrinterW(printer, out var h, IntPtr.Zero)) throw Error("OpenPrinter");
        try
        {
            var size = DocumentPropertiesW(IntPtr.Zero, h, printer, IntPtr.Zero, IntPtr.Zero, 0);
            if (size <= 0) throw Error("DocumentProperties");
            var input = Marshal.AllocHGlobal(size);
            var output = Marshal.AllocHGlobal(size);
            try
            {
                if (settings is { Length: > 0 } && settings.Length == size) Marshal.Copy(settings, 0, input, size);
                else if (DocumentPropertiesW(IntPtr.Zero, h, printer, input, IntPtr.Zero, DmOutBuffer) < 0) throw Error("DocumentProperties");
                var result = DocumentPropertiesW(owner, h, printer, output, input, DmInBuffer | DmInPrompt | DmOutBuffer);
                if (result != IdOk) return null;
                var bytes = new byte[size];
                Marshal.Copy(output, bytes, 0, size);
                return bytes;
            }
            finally
            {
                Marshal.FreeHGlobal(input);
                Marshal.FreeHGlobal(output);
            }
        }
        finally
        {
            ClosePrinter(h);
        }
    }

    private static string? DefaultPrinter()
    {
        uint chars = 0;
        GetDefaultPrinterW(IntPtr.Zero, ref chars);
        if (chars == 0) return null;
        var buffer = Marshal.AllocHGlobal((int)chars * 2);
        try
        {
            return GetDefaultPrinterW(buffer, ref chars) ? Marshal.PtrToStringUni(buffer) : null;
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    /// <summary>A top-down 24-bit <c>BITMAPINFOHEADER</c> for one band.</summary>
    private static void WriteHeader(IntPtr header, int width, int rows)
    {
        Marshal.WriteInt32(header, 0, 40);
        Marshal.WriteInt32(header, 4, width);
        Marshal.WriteInt32(header, 8, -rows);
        Marshal.WriteInt16(header, 12, 1);
        Marshal.WriteInt16(header, 14, 24);
        for (var o = 16; o < 40; o += 4) Marshal.WriteInt32(header, o, 0);
    }

    private static PrintException Error(string call)
    {
        var code = Marshal.GetLastPInvokeError();
        return new PrintException(code == 0 ? $"{call} failed." : $"{new Win32Exception(code).Message} ({call})");
    }

    /// <summary>
    /// The driver's settings for a job: the given settings (from the driver dialog) or the printer's defaults, with the
    /// paper, portrait orientation and one copy set and then validated by the driver.
    /// </summary>
    private sealed class DevMode : IDisposable
    {
        private DevMode(IntPtr pointer) => Pointer = pointer;

        public IntPtr Pointer { get; }

        public static DevMode For(string printer, PrinterPaper paper, byte[]? settings)
        {
            if (!OpenPrinterW(printer, out var h, IntPtr.Zero)) throw Error("OpenPrinter");
            try
            {
                var size = DocumentPropertiesW(IntPtr.Zero, h, printer, IntPtr.Zero, IntPtr.Zero, 0);
                if (size <= 0) throw Error("DocumentProperties");
                var buffer = Marshal.AllocHGlobal(size);
                if (settings is { Length: > 0 } && settings.Length == size) Marshal.Copy(settings, 0, buffer, size);
                else if (DocumentPropertiesW(IntPtr.Zero, h, printer, buffer, IntPtr.Zero, DmOutBuffer) < 0)
                {
                    Marshal.FreeHGlobal(buffer);
                    throw Error("DocumentProperties");
                }

                var fields = (uint)Marshal.ReadInt32(buffer, OffsetFields);
                fields = (fields | DmOrientation | DmPaperSizeField | DmCopies) & ~(DmPaperLength | DmPaperWidth | DmFormName);
                Marshal.WriteInt32(buffer, OffsetFields, (int)fields);
                Marshal.WriteInt16(buffer, OffsetOrientation, 1); // DMORIENT_PORTRAIT
                Marshal.WriteInt16(buffer, OffsetPaperSize, paper.Id);
                Marshal.WriteInt16(buffer, OffsetCopies, 1);      // copies are sent as pages
                DocumentPropertiesW(IntPtr.Zero, h, printer, buffer, buffer, DmInBuffer | DmOutBuffer);
                return new DevMode(buffer);
            }
            finally
            {
                ClosePrinter(h);
            }
        }

        public void Dispose() => Marshal.FreeHGlobal(Pointer);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct PrinterInfo4
    {
        public IntPtr PrinterName;
        public IntPtr ServerName;
        public uint Attributes;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct DocInfo
    {
        public int Size;
        public IntPtr DocName;
        public IntPtr Output;
        public IntPtr DataType;
        public uint Type;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct JobInfo1
    {
        public uint JobId;
        public IntPtr PrinterName;
        public IntPtr MachineName;
        public IntPtr UserName;
        public IntPtr Document;
        public IntPtr DataType;
        public IntPtr StatusText;
        public uint Status;
        public uint Priority;
        public uint Position;
        public uint TotalPages;
        public uint PagesPrinted;
        public ushort Year, Month, DayOfWeek, Day, Hour, Minute, Second, Milliseconds;
    }

    [LibraryImport("winspool.drv", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool EnumPrintersW(uint flags, string? name, uint level, IntPtr buffer, uint size, out uint needed, out uint returned);

    [LibraryImport("winspool.drv", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GetDefaultPrinterW(IntPtr buffer, ref uint chars);

    [LibraryImport("winspool.drv", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool OpenPrinterW(string name, out IntPtr printer, IntPtr defaults);

    [LibraryImport("winspool.drv", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool ClosePrinter(IntPtr printer);

    [LibraryImport("winspool.drv", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
    private static partial int DocumentPropertiesW(IntPtr window, IntPtr printer, string deviceName, IntPtr output, IntPtr input, uint mode);

    [LibraryImport("winspool.drv", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
    private static partial int DeviceCapabilitiesW(string device, string? port, ushort capability, IntPtr output, IntPtr devMode);

    [LibraryImport("winspool.drv", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GetJobW(IntPtr printer, uint jobId, uint level, IntPtr buffer, uint size, out uint needed);

    [LibraryImport("winspool.drv", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool SetJobW(IntPtr printer, uint jobId, uint level, IntPtr job, uint command);

    [LibraryImport("gdi32.dll", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
    private static partial IntPtr CreateDCW(string? driver, string device, string? port, IntPtr devMode);

    [LibraryImport("gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool DeleteDC(IntPtr hdc);

    [LibraryImport("gdi32.dll")]
    private static partial int GetDeviceCaps(IntPtr hdc, int index);

    [LibraryImport("gdi32.dll", SetLastError = true)]
    private static partial int StartDocW(IntPtr hdc, ref DocInfo doc);

    [LibraryImport("gdi32.dll", SetLastError = true)]
    private static partial int StartPage(IntPtr hdc);

    [LibraryImport("gdi32.dll", SetLastError = true)]
    private static partial int EndPage(IntPtr hdc);

    [LibraryImport("gdi32.dll", SetLastError = true)]
    private static partial int EndDoc(IntPtr hdc);

    [LibraryImport("gdi32.dll")]
    private static partial int AbortDoc(IntPtr hdc);

    [LibraryImport("gdi32.dll", SetLastError = true)]
    private static partial int StretchDIBits(IntPtr hdc, int xDest, int yDest, int destWidth, int destHeight, int xSrc, int ySrc, int srcWidth, int srcHeight, IntPtr bits, IntPtr bitmapInfo, uint usage, uint rop);
}
