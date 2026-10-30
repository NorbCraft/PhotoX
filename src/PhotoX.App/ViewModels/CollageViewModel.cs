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

    public CollageViewModel(AppServices services)
    {
        _services = services;
        Picker = new PhotoPickerViewModel(services) { ThumbWidth = 116, ThumbHeight = 116 };
        Picker.Picked += photo => _ = PlaceAsync(photo.Path);
        foreach (var t in CollageTemplates.All) Templates.Add(new CollageTemplateItem(t));
        Sheet = PrintFormats.P10x15;
        Template = CollageTemplates.Default;
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

    public IReadOnlyList<PrintFormat> Sheets => PrintFormats.Sheets;

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

    public IReadOnlyList<RectD> Slots => CollageLayout.Slots(Template, PageWidthMm, PageHeightMm, (double)SpacingMm);

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

    partial void OnSheetChanged(PrintFormat value) => Changed();

    partial void OnIsLandscapeChanged(bool value)
    {
        RefreshTemplates();
        Changed();
    }

    partial void OnTemplateChanged(CollageTemplate value)
    {
        foreach (var t in Templates) t.IsSelected = t.Template == value;
        if (SelectedSlot >= value.Count) SelectedSlot = 0;
        Changed();
    }

    partial void OnSpacingMmChanged(decimal value) => Changed();

    partial void OnSelectedSlotChanged(int value) => Changed();

    [RelayCommand]
    private void SetSheet(string id)
    {
        if (PrintFormats.FindSheet(id) is { } sheet) Sheet = sheet;
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
            var slots = Slots;
            var photos = Enumerable.Range(0, Template.Count)
                .Select(i => _items[i] is { } item ? new CollagePhoto(item.Path, item.CropFor(slots[i].Width / slots[i].Height)) : null)
                .ToList();
            var (pageW, pageH) = (PageWidthMm, PageHeightMm);
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
        Scene = new CollageScene(PageWidthMm, PageHeightMm, slots, photos, SelectedSlot);
        OnPropertyChanged(nameof(HasPhotos));
        OnPropertyChanged(nameof(SelectedHasPhoto));
        RaiseTexts();
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
