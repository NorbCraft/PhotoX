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

/// <summary>One item waiting in the Printing workspace: its page preview (the first ones only) and its copies.</summary>
public sealed partial class PrintPreviewItem(PrintItem item, int copies) : ViewModelBase
{
    /// <summary>The item as Print in another workspace sent it.</summary>
    public PrintItem Item { get; } = item;

    public string Name => Item.Name;

    /// <summary>Copies this item prints: a gallery photo's requested quantity (prompt_09 §5), else the Copies setting.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CopiesText))]
    public partial int Copies { get; set; } = copies;

    public string CopiesText => "×" + Copies.ToString(Localizer.Instance.Culture);

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
    /// <summary>What Print sent here, including gallery photos set to 0 copies, which stay hidden until set above 0.</summary>
    private IReadOnlyList<PrintItem> _chosen = [];

    /// <summary>What prints, in order: every chosen item with at least one copy.</summary>
    private List<PrintPreviewItem> _entries = [];
    private CancellationTokenSource _previewCts = new();
    private bool _syncingWhole;

    /// <summary>What was waiting before the calibration sheet was shown, restored afterwards (prompt_07 §3.4).</summary>
    private (IReadOnlyList<PrintItem> Items, PrintFormat Size, bool SizeFixed, int Copies, bool Whole)? _beforeCalibration;

    public PrintViewModel(AppServices services)
    {
        _services = services;
        Queue = new PrintQueueViewModel(services.Printing);
        History = new PrintHistoryViewModel(services.PrintLog, Queue);
        SelectedSize = PrintFormats.P10x15;
        Localizer.Instance.LanguageChanged += (_, _) => RaiseTexts();
        // The copies of waiting photos follow their quantity in Photos (prompt_09 §5).
        services.PrintQuantities.Changed += paths =>
        {
            if (Avalonia.Threading.Dispatcher.UIThread.CheckAccess()) ApplyQuantities(paths);
            else Avalonia.Threading.Dispatcher.UIThread.Post(() => ApplyQuantities(paths));
        };
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

    /// <summary>The first items, with their page as it will print.</summary>
    public ObservableCollection<PrintPreviewItem> Previews { get; } = [];

    /// <summary>The items after the previewed ones: listed by name with their copies, so every quantity can be checked.</summary>
    public ObservableCollection<PrintPreviewItem> MoreItems { get; } = [];

    public IReadOnlyList<PrintFormat> Sizes => PrintFormats.PrintSizes;

    [ObservableProperty]
    public partial string? SelectedPrinter { get; set; }

    [ObservableProperty]
    public partial PrintFormat SelectedSize { get; set; }

    [ObservableProperty]
    public partial PaperChoice? SelectedPaper { get; set; }

    /// <summary>Copies of an ID sheet, a collage or the calibration sheet. Gallery photos print their own quantity instead (prompt_09 §5).</summary>
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

    /// <summary>
    /// Whole instead of Crop for photos without their own crop (prompt_07 §3.5): the whole photo with white borders.
    /// Crop (fill the paper, centred) is the default of every print started from Photos.
    /// </summary>
    [ObservableProperty]
    public partial bool WholePhoto { get; set; }

    /// <summary>The statistics are folded away when Print opens this workspace, so customers do not see them (prompt_07 §3.1).</summary>
    [ObservableProperty]
    public partial bool StatisticsOpen { get; set; }

    /// <summary>The calibration sheet is shown in place of what was waiting (prompt_07 §3.4).</summary>
    public bool IsCalibration => _beforeCalibration is not null;

    /// <summary>Crop | Whole applies to photos from Photos, not to ID sheets or collages, whose layout is fixed.</summary>
    public bool HasPhotos => _chosen.Any(i => i is PhotoPrintItem);

    /// <summary>The printer has no paper of the print size (prompt_07 §3.3).</summary>
    public bool HasNoMatchingPaper => HasPrinters && SelectedPrinter is not null && !Papers.Any(p => PrintLayout.PaperMatches(p.Paper.WidthMm, p.Paper.HeightMm, SelectedSize.WidthMm, SelectedSize.HeightMm));

    public string NoMatchingPaperText => Localizer.Instance.Format("NoMatchingPaper", SelectedPrinter ?? string.Empty, SizeName(SelectedSize), InchName(SelectedSize));

    public string ClearText => Localizer.Instance[IsCalibration ? "CancelCalibration" : "ClearPrint"];

    public bool HasItems => _entries.Count > 0;

    public bool HasOrigin => OriginName is not null || IsCalibration;

    public bool HasPrinters => Printers.Count > 0;

    public bool IsSupported => Backend.IsSupported;

    public int PhotoCount => _entries.Count;

    /// <summary>Copies of everything waiting, as they will be sent to the queue.</summary>
    public int TotalCopies => _entries.Sum(e => e.Copies);

    public bool HasMorePreviews => _entries.Count > PreviewLimit;

    public string MorePreviewsText => Localizer.Instance.Format("PrintMore", _entries.Count - PreviewLimit);

    public string BackText => IsCalibration ? Localizer.Instance["CancelCalibration"] : Localizer.Instance.Format("BackTo", OriginName ?? string.Empty);

    /// <summary>The driver offers a paper of the print size; otherwise the operator chooses one and the print is centred.</summary>
    public bool PaperMatches => SelectedPaper is { } p && PrintLayout.PaperMatches(p.Paper.WidthMm, p.Paper.HeightMm, SelectedSize.WidthMm, SelectedSize.HeightMm);

    public bool PaperTooSmall => SelectedPaper is { } p && (Math.Min(p.Paper.WidthMm, p.Paper.HeightMm) < SelectedSize.WidthMm - 1 || Math.Max(p.Paper.WidthMm, p.Paper.HeightMm) < SelectedSize.HeightMm - 1);

    public string? PaperNote => SelectedPaper is null ? null
        : PaperTooSmall ? Localizer.Instance.Format("PaperTooSmall", SizeName(SelectedSize))
        : PaperMatches ? null
        : Localizer.Instance.Format("PaperCentred", SizeName(SelectedSize));

    public bool CanPrint => HasItems && HasPrinters && SelectedPrinter is not null && SelectedPaper is not null && !PaperTooSmall;

    /// <summary>"4 photos · 7 copies on 10 × 15 cm" for gallery photos (prompt_09 §5); "1 × 2 = 2 prints on …" for a sheet or collage.</summary>
    public string SummaryText => !HasItems ? Localizer.Instance["NothingToPrint"]
        : HasPhotos ? Localizer.Instance.Format("PrintPhotosSummary", Localizer.Instance.Plural("PrintPhotos", PhotoCount), Localizer.Instance.Plural("PrintCopies", TotalCopies), SizeName(SelectedSize))
        : Localizer.Instance.Format("PrintSummary", PhotoCount, Copies, TotalCopies, SizeName(SelectedSize));

    public string PrintButtonText => Localizer.Instance.Format("PrintOn", SizeName(SelectedSize));

    public string LoadedPaperText => Localizer.Instance.Format("LoadedPaper", SizeName(SelectedSize));

    /// <summary>Preview page size (portrait) on screen: one large page, or a grid of smaller ones.</summary>
    public double PreviewWidth => _entries.Count == 1 ? 380 : 170;

    public double PreviewHeight => PreviewWidth * SelectedSize.HeightMm / SelectedSize.WidthMm;

    public static string SizeName(PrintFormat f) => $"{f.Name} cm";

    /// <summary>The size in inches, as drivers name photo papers (10×15 = 4 × 6 in).</summary>
    private static string InchName(PrintFormat f) => string.Format(System.Globalization.CultureInfo.InvariantCulture, "{0:0.#} × {1:0.#} in", f.WidthMm / 25.4, f.HeightMm / 25.4);

    /// <summary>
    /// Shows what to print (from Print in another workspace). It replaces what was waiting here; nothing is sent
    /// before the operator presses Print.
    /// </summary>
    public void Load(IReadOnlyList<PrintItem> items, PrintFormat size, bool sizeFixed, string? origin)
    {
        _beforeCalibration = null;
        StatisticsOpen = false;
        OriginName = origin;
        Show(items, size, sizeFixed, 1, whole: false);
    }

    private void Show(IReadOnlyList<PrintItem> items, PrintFormat size, bool sizeFixed, int copies, bool whole)
    {
        _chosen = items;
        IsSizeFixed = sizeFixed;
        Copies = copies;
        _syncingWhole = true;
        WholePhoto = whole;
        _syncingWhole = false;
        StatusMessage = null;
        Fill();
        RefreshPrinters();
        if (SelectedSize == size) _ = RenderPreviewsAsync();
        else SelectedSize = size;
        RaiseItems();
    }

    /// <summary>The copies an item prints: a gallery photo its requested quantity (prompt_09 §5), anything else the Copies setting.</summary>
    private int CopiesOf(PrintItem item) => item is PhotoPrintItem photo ? _services.PrintQuantities.For(photo.Path) : Copies;

    /// <summary>Lists the chosen items with at least one copy: the first ones previewed, the others by name.</summary>
    private void Fill()
    {
        _previewCts.Cancel();
        _entries = _chosen.Select(i => new PrintPreviewItem(i, CopiesOf(i))).Where(e => e.Copies > 0).ToList();
        Previews.Clear();
        MoreItems.Clear();
        foreach (var e in _entries.Take(PreviewLimit)) Previews.Add(e);
        foreach (var e in _entries.Skip(PreviewLimit)) MoreItems.Add(e);
    }

    /// <summary>
    /// A quantity changed in Photos while its photo waits here: the copies follow, and a photo set to 0 leaves the list
    /// (or comes back when set above 0). Pages are rendered again only when the list itself changes.
    /// </summary>
    private void ApplyQuantities(IReadOnlyList<string> paths)
    {
        var changed = paths.ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (!_chosen.OfType<PhotoPrintItem>().Any(p => changed.Contains(p.Path))) return;
        var included = _chosen.Where(i => CopiesOf(i) > 0).ToList();
        if (included.SequenceEqual(_entries.Select(e => e.Item), ReferenceEqualityComparer.Instance))
        {
            foreach (var e in _entries) e.Copies = CopiesOf(e.Item);
        }
        else
        {
            Fill();
            _ = RenderPreviewsAsync();
        }

        RaiseItems();
    }

    /// <summary>Called when the workspace is shown: printers may have been added or removed meanwhile.</summary>
    public void Activate()
    {
        RefreshPrinters();
        History.Refresh();
    }

    partial void OnSelectedPrinterChanged(string? value) => LoadPapers();

    partial void OnWholePhotoChanged(bool value)
    {
        if (!_syncingWhole) _ = RenderPreviewsAsync();
    }

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
        OnPropertyChanged(nameof(HasNoMatchingPaper));
        OnPropertyChanged(nameof(NoMatchingPaperText));
        OnPropertyChanged(nameof(ClearText));
    }

    partial void OnCopiesChanged(int value)
    {
        if (value < 1) Copies = 1;
        else if (value > 99) Copies = 99;
        foreach (var e in _entries.Where(e => e.Item is not PhotoPrintItem)) e.Copies = Copies;
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

    /// <summary>Back (and Escape): from the calibration sheet back to what was waiting; otherwise to the workspace that sent the print.</summary>
    [RelayCommand]
    private void Back()
    {
        if (IsCalibration) CancelCalibration();
        else BackRequested?.Invoke();
    }

    [RelayCommand]
    private void ToggleStatistics() => StatisticsOpen = !StatisticsOpen;

    [RelayCommand]
    private void SetWhole(bool whole) => WholePhoto = whole;

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

    /// <summary>
    /// Sends every item to the queue with its own copies (one job per item, prompt_09 §5), then clears the list (a
    /// second Print needs a new choice). The requested quantities in Photos stay as they are.
    /// </summary>
    [RelayCommand]
    private void Print()
    {
        if (!CanPrint) return;
        var printer = SelectedPrinter!;
        var settings = _services.Settings.PrinterSettings.TryGetValue(printer, out var b64) ? TryDecode(b64) : null;
        var requests = _entries.Select(e => new PrintRequest(Prepared(e.Item) with { Copies = e.Copies }, printer, SelectedPaper!.Paper, SelectedSize, settings)).ToList();
        _services.Printing.Enqueue(requests);
        _services.Settings.LastPrinter = printer;
        _services.SaveSettings();
        Log.Information("Print: {Count} item(s), {Copies} copies on {Printer}, {Size}, paper {Paper}, {Fit}", requests.Count, requests.Sum(r => r.Item.Copies), printer, SelectedSize.Id, SelectedPaper!.Paper.Name, WholePhoto ? "whole" : "crop");
        if (IsCalibration)
        {
            CancelCalibration();          // the calibration sheet is printed: back to what was waiting
            return;
        }

        ClearItems();
        StatusMessage = Localizer.Instance.Plural("JobsSent", requests.Count);
    }

    /// <summary>The item as it prints: photos follow Crop | Whole.</summary>
    private PrintItem Prepared(PrintItem item) => item is PhotoPrintItem photo ? photo with { Whole = WholePhoto } : item;

    /// <summary>
    /// The calibration sheet (FR-071, prompt_06): printed through the same path as photos, so measuring it checks
    /// PhotoX's direct printing. The sheet follows the chosen size (10×15 or 15×20).
    /// </summary>
    [RelayCommand]
    private void LoadCalibration()
    {
        if (IsCalibration) return;
        var before = (_chosen, SelectedSize, IsSizeFixed, Copies, WholePhoto);
        Show(
            [new SizedPrintItem(Localizer.Instance["CalibrationJob"], (_, size) => size.Id == PrintFormats.P15x20.Id
                ? Imaging.Id.CalibrationSheet.FifteenByTwenty()
                : Imaging.Id.CalibrationSheet.TenByFifteen(), 1)],
            SelectedSize, sizeFixed: false, copies: 1, whole: false);
        _beforeCalibration = before;
        RaiseItems();
    }

    /// <summary>Removes what waits to be printed (nothing was sent), or, with the calibration sheet, cancels it.</summary>
    [RelayCommand]
    private void Clear()
    {
        if (IsCalibration) CancelCalibration();
        else ClearItems();
    }

    private void ClearItems()
    {
        _previewCts.Cancel();
        _chosen = [];
        _entries = [];
        Previews.Clear();
        MoreItems.Clear();
        StatusMessage = null;
        RaiseItems();
    }

    /// <summary>Back to the photos and settings that were waiting before the calibration sheet (prompt_07 §3.4).</summary>
    [RelayCommand]
    private void CancelCalibration()
    {
        if (_beforeCalibration is not { } before) return;
        _beforeCalibration = null;
        if (before.Items.Count == 0)
        {
            ClearItems();
            IsSizeFixed = before.SizeFixed;
            SelectedSize = before.Size;
        }
        else
        {
            Show(before.Items, before.Size, before.SizeFixed, before.Copies, before.Whole);
        }

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
        // The driver's paper of the print size by its reported size (names differ between drivers), a plain one before
        // a two-up or split variant; none when the driver has no such paper (prompt_07 §3.3).
        var best = PaperMatcher.Best(Papers.Select(p => new DriverPaper(p.Paper.Name, p.Paper.WidthMm, p.Paper.HeightMm)).ToList(), SelectedSize.WidthMm, SelectedSize.HeightMm);
        SelectedPaper = best is int i ? Papers[i] : null;
        if (SelectedPaper is not null) Log.Information("Paper for {Size} on {Printer}: {Paper}", SelectedSize.Id, SelectedPrinter, SelectedPaper.Paper.Name);
        else Log.Information("No {Size} paper on {Printer} (papers: {Papers})", SelectedSize.Id, SelectedPrinter, string.Join(", ", Papers.Select(p => p.Label)));
        RaiseTexts();
    }

    /// <summary>Renders the preview pages in the background with the same layout as the print.</summary>
    private async Task RenderPreviewsAsync()
    {
        _previewCts.Cancel();
        _previewCts = new CancellationTokenSource();
        var ct = _previewCts.Token;
        var size = SelectedSize;
        var single = _entries.Count == 1;
        foreach (var preview in Previews.ToList())
        {
            var item = preview.Item;
            try
            {
                var data = await Task.Run(() =>
                {
                    var (content, fill) = PrintContent.Load(Prepared(item), preview: true, size);
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
        OnPropertyChanged(nameof(HasPhotos));
        OnPropertyChanged(nameof(IsCalibration));
        OnPropertyChanged(nameof(ClearText));
        OnPropertyChanged(nameof(HasOrigin));
        OnPropertyChanged(nameof(BackText));
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
        OnPropertyChanged(nameof(TotalCopies));
        OnPropertyChanged(nameof(SummaryText));
        OnPropertyChanged(nameof(PrintButtonText));
        OnPropertyChanged(nameof(LoadedPaperText));
        OnPropertyChanged(nameof(BackText));
        OnPropertyChanged(nameof(HasNoMatchingPaper));
        OnPropertyChanged(nameof(NoMatchingPaperText));
        OnPropertyChanged(nameof(ClearText));
    }
}
