using System.Collections.ObjectModel;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ImageMagick;
using PhotoX.App.Controls;
using PhotoX.App.Localization;
using PhotoX.App.Services;
using PhotoX.Core.Collage;
using PhotoX.Core.Formats;
using PhotoX.Core.Geometry;
using PhotoX.Imaging;
using PhotoX.Imaging.Collage;
using PhotoX.Imaging.Id;
using PhotoX.Storage;
using Serilog;

namespace PhotoX.App.ViewModels;

/// <summary>A photo in a collage slot: a screen-sized preview, the upright photo size, and its framing in the slot.</summary>
public sealed class CollageItem(string path, Bitmap preview, double width, double height)
{
    public string Path { get; } = path;

    public Bitmap Preview { get; } = preview;

    /// <summary>Upright size of the photo file in pixels (the crop is relative to it).</summary>
    public double Width { get; } = width;

    public double Height { get; } = height;

    /// <summary>1 = the largest crop of the slot's ratio; larger values zoom in.</summary>
    public double Zoom { get; set; } = 1;

    public double CenterX { get; set; } = 0.5;

    public double CenterY { get; set; } = 0.5;

    public NormalizedRect CropFor(double slotRatio) => CollageLayout.Crop(Width, Height, slotRatio, Zoom, CenterX, CenterY);
}

/// <summary>A template choice with a small drawing of its slots.</summary>
public sealed partial class CollageTemplateItem(CollageTemplate template) : ViewModelBase
{
    public CollageTemplate Template { get; } = template;

    [ObservableProperty]
    public partial Geometry? Drawing { get; set; }

    [ObservableProperty]
    public partial bool IsSelected { get; set; }
}

/// <summary>
/// Collages (prompt_04 §5): choose photos with the built-in explorer, pick a sheet and a template, frame each photo in
/// its slot, then save a print-ready JPG at 300 ppi. The preview only redraws (the canvas scales the preview bitmaps);
/// the full-resolution photos are read only when saving.
/// </summary>
public sealed partial class CollageViewModel : ViewModelBase
{
    public const int PreviewEdge = 1600;
    private const int MaxSlots = 9;

    private readonly AppServices _services;
    private readonly CollageItem?[] _items = new CollageItem?[MaxSlots];
    private double[] _columnWeights = [];
    private double[] _rowWeights = [];
    private bool _syncing;

    public CollageViewModel(AppServices services)
    {
        _services = services;
        Picker = new PhotoPickerViewModel(services) { ThumbWidth = 116, ThumbHeight = 116 };
        Picker.Picked += photo => _ = PlaceAsync(photo.Path);
        foreach (var t in CollageTemplates.All) Templates.Add(new CollageTemplateItem(t));
        Template = CollageTemplates.Default;
        Sheet = PrintFormats.P10x15;
        OutputFolder = services.Settings.CollageOutputFolder;
        RefreshTemplates();
        Localizer.Instance.LanguageChanged += (_, _) => RaiseTexts();
    }

    /// <summary>The built-in explorer and thumbnails for choosing photos (prompt_04 §6).</summary>
    public PhotoPickerViewModel Picker { get; }

    /// <summary>Set by the shell: the folder open in Photos, where the picker starts.</summary>
    public Func<string?>? StartFolder { get; set; }

    /// <summary>Set by the view: the folder picker for the output folder (not available in headless tests).</summary>
    public Func<Task<string?>>? PickFolder { get; set; }

    public ObservableCollection<CollageTemplateItem> Templates { get; } = [];

    /// <summary>Collage sheets: 10×15 and 15×20 (prompt_05 §8).</summary>
    public IReadOnlyList<PrintFormat> Sheets => PrintFormats.PrintSizes;

    /// <summary>Raised by Print with the collage and its sheet size (prompt_05 §8).</summary>
    public event Action<Printing.PrintItem, PrintFormat>? PrintRequested;

    /// <summary>Width of the selected slot in cm; typing a value resizes its columns (prompt_05 §8).</summary>
    [ObservableProperty]
    public partial decimal SlotWidthCm { get; set; }

    [ObservableProperty]
    public partial decimal SlotHeightCm { get; set; }

    public bool CanResizeWidth => SelectedCell is { } c && c.ColumnSpan < Grid.Columns;

    public bool CanResizeHeight => SelectedCell is { } c && c.RowSpan < Grid.Rows;

    private CollageTemplate Grid => CollageLayout.GridFor(Template, PageWidthMm, PageHeightMm);

    private CollageCell? SelectedCell => SelectedSlot < Grid.Cells.Count ? Grid.Cells[SelectedSlot] : null;

    [ObservableProperty]
    public partial PrintFormat Sheet { get; set; }

    [ObservableProperty]
    public partial bool IsLandscape { get; set; }

    [ObservableProperty]
    public partial CollageTemplate Template { get; set; }

    /// <summary>Outer margin and gap between photos, in mm.</summary>
    [ObservableProperty]
    public partial decimal SpacingMm { get; set; } = 3;

    [ObservableProperty]
    public partial int SelectedSlot { get; set; }

    /// <summary>What the canvas draws; replaced on every change.</summary>
    [ObservableProperty]
    public partial CollageScene? Scene { get; set; }

    [ObservableProperty]
    public partial bool IsBusy { get; set; }

    [ObservableProperty]
    public partial string? StatusMessage { get; set; }

    [ObservableProperty]
    public partial string? ErrorMessage { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(OutputFolderText), nameof(HasOutputFolder))]
    public partial string? OutputFolder { get; set; }

    public bool HasOutputFolder => !string.IsNullOrEmpty(OutputFolder);

    public string OutputFolderText => HasOutputFolder ? OutputFolder! : Localizer.Instance["NoOutputFolder"];

    public string FileNamePreview => CollageFileNames.Base(DateTime.Now) + ".jpg";

    public double PageWidthMm => IsLandscape ? Sheet.HeightMm : Sheet.WidthMm;

    public double PageHeightMm => IsLandscape ? Sheet.WidthMm : Sheet.HeightMm;

    public IReadOnlyList<RectD> Slots => CollageLayout.Slots(Template, PageWidthMm, PageHeightMm, (double)SpacingMm, _columnWeights, _rowWeights);

    public double TemplateIconWidth => IsLandscape ? 54 : 36;

    public double TemplateIconHeight => IsLandscape ? 36 : 54;

    public bool HasPhotos => Enumerable.Range(0, Template.Count).Any(i => _items[i] is not null);

    public bool SelectedHasPhoto => SelectedSlot < Template.Count && _items[SelectedSlot] is not null;

    public string PageText
    {
        get
        {
            var (w, h) = CollageRenderer.PagePixels(PageWidthMm, PageHeightMm);
            return Localizer.Instance.Format("CollagePage", Sheet.Name, w, h);
        }
    }

    public string FilledText => Localizer.Instance.Format("CollageFilled", Enumerable.Range(0, Template.Count).Count(i => _items[i] is not null), Template.Count);

    /// <summary>Called when the workspace is shown: the picker opens the folder of the Photos workspace the first time.</summary>
    public void Activate() => Picker.EnsureStarted(StartFolder?.Invoke());

    partial void OnSheetChanged(PrintFormat value)
    {
        ResetWeights();
        Changed();
    }

    partial void OnIsLandscapeChanged(bool value)
    {
        RefreshTemplates();
        ResetWeights();
        Changed();
    }

    partial void OnTemplateChanged(CollageTemplate value)
    {
        foreach (var t in Templates) t.IsSelected = t.Template == value;
        if (SelectedSlot >= value.Count) SelectedSlot = 0;
        ResetWeights();
        Changed();
    }

    partial void OnSlotWidthCmChanged(decimal value)
    {
        if (_syncing || SelectedCell is not { } c || !CanResizeWidth) return;
        var g = Grid;
        _columnWeights = CollageLayout.Resize(_columnWeights, c.Column, c.ColumnSpan, (double)value * 10, CollageLayout.Available(PageWidthMm, g.Columns, (double)SpacingMm), (double)SpacingMm);
        Changed();
    }

    partial void OnSlotHeightCmChanged(decimal value)
    {
        if (_syncing || SelectedCell is not { } c || !CanResizeHeight) return;
        var g = Grid;
        _rowWeights = CollageLayout.Resize(_rowWeights, c.Row, c.RowSpan, (double)value * 10, CollageLayout.Available(PageHeightMm, g.Rows, (double)SpacingMm), (double)SpacingMm);
        Changed();
    }

    /// <summary>Dragging a gap: the columns (or rows) on both sides change, the photos stay inside the sheet.</summary>
    public void MoveBoundary(bool column, int index, double deltaMm)
    {
        var g = Grid;
        if (column && index < g.Columns - 1) _columnWeights = CollageLayout.MoveBoundary(_columnWeights, index, deltaMm, CollageLayout.Available(PageWidthMm, g.Columns, (double)SpacingMm));
        else if (!column && index < g.Rows - 1) _rowWeights = CollageLayout.MoveBoundary(_rowWeights, index, deltaMm, CollageLayout.Available(PageHeightMm, g.Rows, (double)SpacingMm));
        else return;
        Changed();
    }

    /// <summary>Back to the template's equal columns and rows.</summary>
    [RelayCommand]
    public void ResetLayout()
    {
        ResetWeights();
        Changed();
    }

    private void ResetWeights()
    {
        if (Template is null || Sheet is null) return;
        var g = Grid;
        _columnWeights = Enumerable.Repeat(1.0, g.Columns).ToArray();
        _rowWeights = Enumerable.Repeat(1.0, g.Rows).ToArray();
    }

    partial void OnSpacingMmChanged(decimal value) => Changed();

    partial void OnSelectedSlotChanged(int value) => Changed();

    [RelayCommand]
    private void SetSheet(string id)
    {
        if (PrintFormats.FindPrintSize(id) is { } sheet) Sheet = sheet;
    }

    /// <summary>Print (prompt_05 §8): the same composition as Save, on paper of the collage's sheet size.</summary>
    [RelayCommand]
    private void Print()
    {
        if (!HasPhotos) return;
        var (pageW, pageH, slots, photos) = Composition();
        PrintRequested?.Invoke(new Printing.ImagePrintItem(
            Localizer.Instance.Format("CollageJobName", Sheet.Name, Template.Count),
            preview => CollageRenderer.Render(pageW, pageH, slots, photos, preview ? 75 : CollageRenderer.Ppi, preview ? 800 : null),
            1), Sheet);
    }

    /// <summary>The final composition: page, slots and each photo's part, as used by Save and Print.</summary>
    private (double W, double H, IReadOnlyList<RectD> Slots, IReadOnlyList<CollagePhoto?> Photos) Composition()
    {
        var slots = Slots;
        var photos = Enumerable.Range(0, Template.Count)
            .Select(i => _items[i] is { } item ? new CollagePhoto(item.Path, item.CropFor(slots[i].Width / slots[i].Height)) : null)
            .ToList();
        return (PageWidthMm, PageHeightMm, slots, photos);
    }

    [RelayCommand]
    private void SetLandscape(bool landscape) => IsLandscape = landscape;

    [RelayCommand]
    private void SetTemplate(CollageTemplateItem item) => Template = item.Template;

    /// <summary>Puts a photo into the selected slot (replacing what was there), then selects the next empty slot.</summary>
    public async Task PlaceAsync(string path)
    {
        var slot = SelectedSlot;
        ErrorMessage = null;
        try
        {
            var (pixels, w, h) = await Task.Run(() =>
            {
                var info = ImageProbe.Probe(path);
                using var proxy = ImageLoader.Load(path, PreviewEdge);
                return (PixelData.From(proxy), (double)info.OrientedWidth, (double)info.OrientedHeight);
            });
            _items[slot] = new CollageItem(path, BitmapFactory.Create(pixels), w > 0 ? w : pixels.Width, h > 0 ? h : pixels.Height);
        }
        catch (Exception ex) when (ex is ImageLoadException or MagickException)
        {
            ErrorMessage = ex is ImageLoadException le ? ErrorTexts.ForImage(le.Error) : ex.Message;
            Log.Warning(ex, "Collage: cannot load {Path}", path);
            return;
        }

        var next = Enumerable.Range(1, Template.Count).Select(k => (slot + k) % Template.Count).FirstOrDefault(i => _items[i] is null, -1);
        if (next >= 0) SelectedSlot = next;
        Changed();
    }

    [RelayCommand]
    public void RemovePhoto()
    {
        if (!SelectedHasPhoto) return;
        _items[SelectedSlot] = null;
        Changed();
    }

    [RelayCommand]
    public void ClearAll()
    {
        Array.Clear(_items);
        SelectedSlot = 0;
        Changed();
    }

    public void SelectSlot(int slot)
    {
        if (slot >= 0 && slot < Template.Count) SelectedSlot = slot;
    }

    /// <summary>Moves the photo inside its slot; <paramref name="dx"/>, <paramref name="dy"/> are fractions of the slot size.</summary>
    public void Pan(int slot, double dx, double dy)
    {
        if (ItemAt(slot) is not { } item) return;
        var crop = item.CropFor(SlotRatio(slot));
        // The photo follows the pointer, so the shown part moves the other way; keep the centre where the crop can be.
        var next = CollageLayout.Crop(item.Width, item.Height, SlotRatio(slot), item.Zoom, crop.X + crop.Width / 2 - dx * crop.Width, crop.Y + crop.Height / 2 - dy * crop.Height);
        item.CenterX = next.X + next.Width / 2;
        item.CenterY = next.Y + next.Height / 2;
        Changed();
    }

    public void Zoom(int slot, double factor)
    {
        if (ItemAt(slot) is not { } item) return;
        item.Zoom = Math.Clamp(item.Zoom * factor, 1, 8);
        var crop = item.CropFor(SlotRatio(slot));
        item.CenterX = crop.X + crop.Width / 2;
        item.CenterY = crop.Y + crop.Height / 2;
        Changed();
    }

    /// <summary>Double-click: the largest centred crop again.</summary>
    public void ResetSlot(int slot)
    {
        if (ItemAt(slot) is not { } item) return;
        (item.Zoom, item.CenterX, item.CenterY) = (1, 0.5, 0.5);
        Changed();
    }

    /// <summary>Rearranges photos between slots (dragging the move handle onto another slot).</summary>
    public void Swap(int from, int to)
    {
        if (from == to || from < 0 || to < 0 || from >= Template.Count || to >= Template.Count) return;
        (_items[from], _items[to]) = (_items[to], _items[from]);
        foreach (var item in new[] { _items[from], _items[to] })
        {
            if (item is not null) (item.Zoom, item.CenterX, item.CenterY) = (1, 0.5, 0.5);
        }

        SelectedSlot = to;
        Changed();
    }

    public bool HandleKey(Key key)
    {
        if (key is not (Key.Delete or Key.Back) || !SelectedHasPhoto) return false;
        RemovePhoto();
        return true;
    }

    [RelayCommand]
    private async Task ChangeOutputFolderAsync()
    {
        if (PickFolder is null) return;
        var folder = await PickFolder();
        if (folder is null) return;
        OutputFolder = folder;
        _services.Settings.CollageOutputFolder = folder;
        _services.SaveSettings();
    }

    /// <summary>Renders the collage at 300 ppi from the full-resolution photos and writes a new JPG (never overwrites).</summary>
    [RelayCommand]
    public async Task<string?> SaveAsync()
    {
        if (IsBusy || !HasPhotos) return null;
        if (!HasOutputFolder)
        {
            await ChangeOutputFolderAsync();
            if (!HasOutputFolder) return null;
        }

        IsBusy = true;
        ErrorMessage = null;
        StatusMessage = Localizer.Instance["Saving"];
        try
        {
            var (pageW, pageH, slots, photos) = Composition();
            var folder = OutputFolder!;
            var quality = _services.Settings.JpegQuality;
            var path = await Task.Run(() =>
            {
                using var page = CollageRenderer.Render(pageW, pageH, slots, photos);
                var bytes = IdOutput.Encode(page, ImageKind.Jpeg, quality);
                var now = DateTime.Now;
                return IdExporter.Write(folder, bytes, ".jpg", now, CollageFileNames.Base(now));
            });
            StatusMessage = Localizer.Instance.Format("CollageSaved", Path.GetFileName(path));
            Log.Information("Collage saved: {Path} ({Sheet}, {Template})", path, Sheet.Id, Template.Id);
            return path;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or MagickException or ImageLoadException)
        {
            StatusMessage = null;
            ErrorMessage = ex is ImageLoadException le ? ErrorTexts.ForImage(le.Error) : Localizer.Instance.Format("ErrWriteFailed", ex.Message);
            Log.Warning(ex, "Collage save failed");
            return null;
        }
        finally
        {
            IsBusy = false;
            OnPropertyChanged(nameof(FileNamePreview));
        }
    }

    private CollageItem? ItemAt(int slot) => slot >= 0 && slot < Template.Count ? _items[slot] : null;

    private double SlotRatio(int slot)
    {
        var r = Slots[slot];
        return r.Width / r.Height;
    }

    private void Changed()
    {
        if (Template is null || Sheet is null) return;
        var slots = Slots;
        var photos = slots.Select((r, i) => _items[i] is { } item ? new CollageSceneItem(item.Preview, item.CropFor(r.Width / r.Height)) : null).ToList();
        var labels = slots.Select(r => SizeLabel(r.Width, r.Height)).ToList();
        var g = Grid;
        var cols = Boundaries(slots, g, column: true);
        var rows = Boundaries(slots, g, column: false);
        Scene = new CollageScene(PageWidthMm, PageHeightMm, slots, photos, SelectedSlot, labels, cols, rows);
        _syncing = true;
        if (SelectedSlot < slots.Count)
        {
            SlotWidthCm = Math.Round((decimal)slots[SelectedSlot].Width / 10, 1);
            SlotHeightCm = Math.Round((decimal)slots[SelectedSlot].Height / 10, 1);
        }

        _syncing = false;
        OnPropertyChanged(nameof(HasPhotos));
        OnPropertyChanged(nameof(SelectedHasPhoto));
        OnPropertyChanged(nameof(CanResizeWidth));
        OnPropertyChanged(nameof(CanResizeHeight));
        PrintCommand.NotifyCanExecuteChanged();
        RaiseTexts();
    }

    /// <summary>Slot size in centimetres, as shown on the slot (prompt_05 §8).</summary>
    public static string SizeLabel(double widthMm, double heightMm) =>
        string.Format(Localizer.Instance.Culture, "{0:0.#} × {1:0.#} cm", widthMm / 10, heightMm / 10);

    /// <summary>Centre of each gap between columns (or rows), from the edges of the slots in that column (or row).</summary>
    private static List<double> Boundaries(IReadOnlyList<RectD> slots, CollageTemplate grid, bool column)
    {
        var count = column ? grid.Columns : grid.Rows;
        var result = new List<double>();
        for (var i = 0; i < count - 1; i++)
        {
            var before = grid.Cells.Select((c, k) => (c, k)).Where(x => (column ? x.c.Column + x.c.ColumnSpan - 1 : x.c.Row + x.c.RowSpan - 1) == i).Select(x => column ? slots[x.k].Right : slots[x.k].Bottom).DefaultIfEmpty(double.NaN).Max();
            var after = grid.Cells.Select((c, k) => (c, k)).Where(x => (column ? x.c.Column : x.c.Row) == i + 1).Select(x => column ? slots[x.k].X : slots[x.k].Y).DefaultIfEmpty(double.NaN).Min();
            result.Add(double.IsNaN(before) || double.IsNaN(after) ? double.NaN : (before + after) / 2);
        }

        return result;
    }

    private void RaiseTexts()
    {
        OnPropertyChanged(nameof(PageText));
        OnPropertyChanged(nameof(FilledText));
        OnPropertyChanged(nameof(OutputFolderText));
    }

    /// <summary>Draws each template's slots on a small sheet of the current orientation.</summary>
    private void RefreshTemplates()
    {
        var (w, h) = (TemplateIconWidth, TemplateIconHeight);
        OnPropertyChanged(nameof(TemplateIconWidth));
        OnPropertyChanged(nameof(TemplateIconHeight));
        foreach (var item in Templates)
        {
            var group = new GeometryGroup();
            foreach (var r in CollageLayout.Slots(item.Template, w, h, 2.2)) group.Children.Add(new RectangleGeometry(new Avalonia.Rect(r.X, r.Y, r.Width, r.Height)));
            item.Drawing = group;
            item.IsSelected = item.Template == Template;
        }
    }
}
