using System.Collections.ObjectModel;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ImageMagick;
using PhotoX.App.Localization;
using PhotoX.App.Services;
using PhotoX.Core.Formats;
using PhotoX.Core.Printing;
using PhotoX.Imaging;
using PhotoX.Imaging.Printing;
using PhotoX.Printing;
using Serilog;

namespace PhotoX.App.ViewModels;

/// <summary>One page of the print preview.</summary>
public sealed partial class PrintPreviewItem(string name) : ViewModelBase
{
    public string Name { get; } = name;

    [ObservableProperty]
    public partial Bitmap? Page { get; set; }
}

/// <summary>A paper offered by the driver, labelled with its size.</summary>
public sealed record PaperChoice(PrinterPaper Paper, string Label)
{
    public override string ToString() => Label;
}

/// <summary>
/// The Printing workspace (prompt_06 §7, replaces the print dialog of prompt_05 §3): what Print in Photos, ID Photos
/// or Collages sent here, the printer, print size (10×15 or 15×20), paper and copies, a preview, and Print; next to
/// it the queue, the history and the statistics. The loaded paper cannot be detected, so the operator confirms the
/// size; the driver's paper of that size is chosen automatically, or must be chosen when the driver has none (then
/// the print is centred at true size).
/// </summary>
public sealed partial class PrintViewModel : ViewModelBase
{
    private const int PreviewLimit = 20;
    private readonly AppServices _services;
    private IReadOnlyList<PrintItem> _items = [];
    private CancellationTokenSource _previewCts = new();

    public PrintViewModel(AppServices services)
    {
        _services = services;
        Queue = new PrintQueueViewModel(services.Printing);
        History = new PrintHistoryViewModel(services.PrintLog, Queue);
        SelectedSize = PrintFormats.P10x15;
        Localizer.Instance.LanguageChanged += (_, _) => RaiseTexts();
    }

    /// <summary>Jobs of this session that are still in progress (prompt_06 §7).</summary>
    public PrintQueueViewModel Queue { get; }

    /// <summary>Earlier and finished jobs, and the statistics.</summary>
    public PrintHistoryViewModel History { get; }

    /// <summary>Raised by Back with nothing: the shell returns to the workspace that sent the print.</summary>
    public event Action? BackRequested;

    /// <summary>Set by the view: the window handle for the driver's own dialog.</summary>
    public Func<IntPtr>? OwnerHandle { get; set; }

    private IPrintBackend Backend => _services.Printing.Backend;

    public ObservableCollection<string> Printers { get; } = [];

    public ObservableCollection<PaperChoice> Papers { get; } = [];

    public ObservableCollection<PrintPreviewItem> Previews { get; } = [];

    public IReadOnlyList<PrintFormat> Sizes => PrintFormats.PrintSizes;

    [ObservableProperty]
    public partial string? SelectedPrinter { get; set; }

    [ObservableProperty]
    public partial PrintFormat SelectedSize { get; set; }

    [ObservableProperty]
    public partial PaperChoice? SelectedPaper { get; set; }

    [ObservableProperty]
    public partial int Copies { get; set; } = 1;

    /// <summary>ID sheets and collages print at their own size.</summary>
    [ObservableProperty]
    public partial bool IsSizeFixed { get; set; }

    /// <summary>The workspace that sent the print ("Photos", "ID Photos", "Collages"), for Back.</summary>
    [ObservableProperty]
    public partial string? OriginName { get; set; }

    /// <summary>What happened last ("3 jobs sent to the queue"), shown when nothing is waiting to be printed.</summary>
    [ObservableProperty]
    public partial string? StatusMessage { get; set; }

    public bool HasItems => _items.Count > 0;

    public bool HasOrigin => OriginName is not null;

    public bool HasPrinters => Printers.Count > 0;

    public bool IsSupported => Backend.IsSupported;

    public int PhotoCount => _items.Count;

    public bool HasMorePreviews => _items.Count > PreviewLimit;

    public string MorePreviewsText => Localizer.Instance.Format("PrintMore", _items.Count - PreviewLimit);

    public string BackText => Localizer.Instance.Format("BackTo", OriginName ?? string.Empty);

    /// <summary>The driver offers a paper of the print size; otherwise the operator chooses one and the print is centred.</summary>
    public bool PaperMatches => SelectedPaper is { } p && PrintLayout.PaperMatches(p.Paper.WidthMm, p.Paper.HeightMm, SelectedSize.WidthMm, SelectedSize.HeightMm);

    public bool PaperTooSmall => SelectedPaper is { } p && (Math.Min(p.Paper.WidthMm, p.Paper.HeightMm) < SelectedSize.WidthMm - 1 || Math.Max(p.Paper.WidthMm, p.Paper.HeightMm) < SelectedSize.HeightMm - 1);

    public string? PaperNote => SelectedPaper is null ? null
        : PaperTooSmall ? Localizer.Instance.Format("PaperTooSmall", SizeName(SelectedSize))
        : PaperMatches ? null
        : Localizer.Instance.Format("PaperCentred", SizeName(SelectedSize));

    public bool CanPrint => HasItems && HasPrinters && SelectedPrinter is not null && SelectedPaper is not null && !PaperTooSmall && Copies >= 1;

    public string SummaryText => HasItems ? Localizer.Instance.Format("PrintSummary", _items.Count, Copies, _items.Count * Copies, SizeName(SelectedSize)) : Localizer.Instance["NothingToPrint"];

    public string PrintButtonText => Localizer.Instance.Format("PrintOn", SizeName(SelectedSize));

    public string LoadedPaperText => Localizer.Instance.Format("LoadedPaper", SizeName(SelectedSize));

    /// <summary>Preview page size (portrait) on screen: one large page, or a grid of smaller ones.</summary>
    public double PreviewWidth => _items.Count == 1 ? 380 : 170;

    public double PreviewHeight => PreviewWidth * SelectedSize.HeightMm / SelectedSize.WidthMm;

    public static string SizeName(PrintFormat f) => $"{f.Name} cm";

    /// <summary>
    /// Shows what to print (from Print in another workspace). It replaces what was waiting here; nothing is sent
    /// before the operator presses Print.
    /// </summary>
    public void Load(IReadOnlyList<PrintItem> items, PrintFormat size, bool sizeFixed, string? origin)
    {
        _items = items;
        OriginName = origin;
        IsSizeFixed = sizeFixed;
        Copies = 1;
        StatusMessage = null;
        Previews.Clear();
        foreach (var item in items.Take(PreviewLimit)) Previews.Add(new PrintPreviewItem(item.Name));
        RefreshPrinters();
        if (SelectedSize == size) _ = RenderPreviewsAsync();
        else SelectedSize = size;
        RaiseItems();
    }

    /// <summary>Called when the workspace is shown: printers may have been added or removed meanwhile.</summary>
    public void Activate()
    {
        RefreshPrinters();
        History.Refresh();
    }

    partial void OnSelectedPrinterChanged(string? value) => LoadPapers();

    partial void OnSelectedSizeChanged(PrintFormat value)
    {
        LoadPapers();
        OnPropertyChanged(nameof(PreviewHeight));
        _ = RenderPreviewsAsync();
        RaiseTexts();
    }

    partial void OnSelectedPaperChanged(PaperChoice? value) => RaiseTexts();

    partial void OnOriginNameChanged(string? value)
    {
        OnPropertyChanged(nameof(HasOrigin));
        OnPropertyChanged(nameof(BackText));
    }

    partial void OnCopiesChanged(int value)
    {
        if (value < 1) Copies = 1;
        else if (value > 99) Copies = 99;
        RaiseTexts();
    }

    [RelayCommand]
    private void SetSize(PrintFormat size)
    {
        if (!IsSizeFixed) SelectedSize = size;
    }

    [RelayCommand]
    private void CopiesMinus() => Copies = Math.Max(1, Copies - 1);

    [RelayCommand]
    private void CopiesPlus() => Copies = Math.Min(99, Copies + 1);

    [RelayCommand]
    private void Back() => BackRequested?.Invoke();

    /// <summary>The driver's own settings dialog (quality, glossy/matte, cutting …), remembered per printer.</summary>
    [RelayCommand]
    private void PrinterSettings()
    {
        if (SelectedPrinter is not { } printer || !IsSupported) return;
        try
        {
            var current = _services.Settings.PrinterSettings.TryGetValue(printer, out var b64) ? Convert.FromBase64String(b64) : null;
            var updated = Backend.EditSettings(printer, current, OwnerHandle?.Invoke() ?? IntPtr.Zero);
            if (updated is null) return;
            _services.Settings.PrinterSettings[printer] = Convert.ToBase64String(updated);
            _services.SaveSettings();
        }
        catch (Exception ex) when (ex is PrintException or FormatException)
        {
            Log.Warning(ex, "Printer settings for {Printer}", printer);
        }
    }

    /// <summary>Sends every item to the queue with the chosen copies, then clears the list (a second Print needs a new choice).</summary>
    [RelayCommand]
    private void Print()
    {
        if (!CanPrint) return;
        var printer = SelectedPrinter!;
        var settings = _services.Settings.PrinterSettings.TryGetValue(printer, out var b64) ? TryDecode(b64) : null;
        var requests = _items.Select(i => new PrintRequest(i with { Copies = Copies }, printer, SelectedPaper!.Paper, SelectedSize, settings)).ToList();
        _services.Printing.Enqueue(requests);
        _services.Settings.LastPrinter = printer;
        _services.SaveSettings();
        Log.Information("Print: {Count} item(s) × {Copies} on {Printer}, {Size}, paper {Paper}", requests.Count, Copies, printer, SelectedSize.Id, SelectedPaper!.Paper.Name);
        Clear();
        StatusMessage = Localizer.Instance.Plural("JobsSent", requests.Count);
    }

    /// <summary>
    /// The calibration sheet (FR-071, prompt_06): printed through the same path as photos, so measuring it checks
    /// PhotoX's direct printing. The sheet follows the chosen size (10×15 or 15×20).
    /// </summary>
    [RelayCommand]
    private void LoadCalibration() => Load(
        [new SizedPrintItem(Localizer.Instance["CalibrationJob"], (_, size) => size.Id == PrintFormats.P15x20.Id
            ? Imaging.Id.CalibrationSheet.FifteenByTwenty()
            : Imaging.Id.CalibrationSheet.TenByFifteen(), 1)],
        SelectedSize, sizeFixed: false, OriginName);

    /// <summary>Removes what waits to be printed; nothing was sent.</summary>
    [RelayCommand]
    private void Clear()
    {
        _previewCts.Cancel();
        _items = [];
        Previews.Clear();
        StatusMessage = null;
        RaiseItems();
    }

    private static byte[]? TryDecode(string b64)
    {
        try
        {
            return Convert.FromBase64String(b64);
        }
        catch (FormatException)
        {
            return null;
        }
    }

    /// <summary>The printers, keeping the choice; the last one used, else a Citizen, else Windows' default.</summary>
    private void RefreshPrinters()
    {
        IReadOnlyList<PrinterInfo> printers;
        try
        {
            printers = Backend.GetPrinters();
        }
        catch (PrintException ex)
        {
            Log.Warning(ex, "Printers");
            printers = [];
        }

        var keep = SelectedPrinter;
        if (!printers.Select(p => p.Name).SequenceEqual(Printers))
        {
            Printers.Clear();
            foreach (var p in printers) Printers.Add(p.Name);
        }

        SelectedPrinter = Printers.FirstOrDefault(p => p == keep)
            ?? Printers.FirstOrDefault(p => p == _services.Settings.LastPrinter)
            ?? Printers.FirstOrDefault(p => p.Contains("CY-02", StringComparison.OrdinalIgnoreCase) || p.Contains("Citizen", StringComparison.OrdinalIgnoreCase))
            ?? printers.FirstOrDefault(p => p.IsDefault)?.Name
            ?? Printers.FirstOrDefault();
        OnPropertyChanged(nameof(HasPrinters));
        RaiseTexts();
    }

    private void LoadPapers()
    {
        Papers.Clear();
        if (SelectedPrinter is null)
        {
            SelectedPaper = null;
            return;
        }

        IReadOnlyList<PrinterPaper> papers;
        try
        {
            papers = Backend.GetPapers(SelectedPrinter);
        }
        catch (PrintException ex)
        {
            Log.Warning(ex, "Papers of {Printer}", SelectedPrinter);
            papers = [];
        }

        foreach (var p in papers) Papers.Add(new PaperChoice(p, string.Format(Localizer.Instance.Culture, "{0}  ({1:0.#} × {2:0.#} mm)", p.Name, p.WidthMm, p.HeightMm)));
        // The driver's paper of the print size, preferring a plain one over a two-up or cut variant of the same size.
        SelectedPaper = Papers
            .Where(c => PrintLayout.PaperMatches(c.Paper.WidthMm, c.Paper.HeightMm, SelectedSize.WidthMm, SelectedSize.HeightMm))
            .OrderBy(c => IsVariant(c.Paper.Name) ? 1 : 0)
            .FirstOrDefault();
        RaiseTexts();
    }

    private static bool IsVariant(string name) =>
        name.Contains("2up", StringComparison.OrdinalIgnoreCase) || name.Contains("2-up", StringComparison.OrdinalIgnoreCase) || name.Contains("x2", StringComparison.OrdinalIgnoreCase);

    /// <summary>Renders the preview pages in the background with the same layout as the print.</summary>
    private async Task RenderPreviewsAsync()
    {
        _previewCts.Cancel();
        _previewCts = new CancellationTokenSource();
        var ct = _previewCts.Token;
        var size = SelectedSize;
        var single = _items.Count == 1;
        foreach (var (item, preview) in _items.Zip(Previews).ToList())
        {
            try
            {
                var data = await Task.Run(() =>
                {
                    var (content, fill) = PrintContent.Load(item, preview: true, size);
                    using (content)
                    {
                        var w = single ? 760 : 340;
                        var h = (int)Math.Round(w * size.HeightMm / size.WidthMm);
                        var placement = PrintLayout.Place(content.Width, content.Height, size.WidthMm, size.HeightMm, fill);
                        using var page = PrintRenderer.RenderPage(content, placement, w, h, size.WidthMm, size.HeightMm);
                        return PixelData.From(page);
                    }
                }, ct);
                if (ct.IsCancellationRequested) return;
                var old = preview.Page;
                preview.Page = BitmapFactory.Create(data);
                (old as IDisposable)?.Dispose();
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception ex) when (ex is ImageLoadException or MagickException or IOException)
            {
                Log.Warning(ex, "Print preview of {Name}", item.Name);
            }
        }
    }

    private void RaiseItems()
    {
        OnPropertyChanged(nameof(HasItems));
        OnPropertyChanged(nameof(PhotoCount));
        OnPropertyChanged(nameof(HasMorePreviews));
        OnPropertyChanged(nameof(MorePreviewsText));
        OnPropertyChanged(nameof(PreviewWidth));
        OnPropertyChanged(nameof(PreviewHeight));
        RaiseTexts();
    }

    private void RaiseTexts()
    {
        OnPropertyChanged(nameof(PaperMatches));
        OnPropertyChanged(nameof(PaperTooSmall));
        OnPropertyChanged(nameof(PaperNote));
        OnPropertyChanged(nameof(CanPrint));
        OnPropertyChanged(nameof(SummaryText));
        OnPropertyChanged(nameof(PrintButtonText));
        OnPropertyChanged(nameof(LoadedPaperText));
        OnPropertyChanged(nameof(BackText));
    }
}
