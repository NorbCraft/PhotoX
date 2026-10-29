using System.Collections.ObjectModel;
using Avalonia.Input;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ImageMagick;
using PhotoX.App.Localization;
using PhotoX.App.Services;
using PhotoX.Core.Id;
using PhotoX.Imaging;
using PhotoX.Imaging.Id;
using PhotoX.Storage;
using Serilog;

namespace PhotoX.App.ViewModels;

public sealed partial class IdSizeItem(IdSize size) : ViewModelBase
{
    public IdSize Size { get; } = size;

    public string Label => Localizer.Instance.Language == "ro" ? Size.Label.Replace('.', ',') : Size.Label;

    public string PixelText => $"{Size.WidthPx} × {Size.HeightPx} px" + (Size.Approximation is { } a ? "  ·  " + (Localizer.Instance.Language == "ro" ? a.Replace('.', ',') : a) : string.Empty);

    [ObservableProperty]
    public partial bool IsSelected { get; set; }

    public void RefreshTexts()
    {
        OnPropertyChanged(nameof(Label));
        OnPropertyChanged(nameof(PixelText));
    }
}

/// <summary>
/// ID Photo Creator (FR-007, FR-061–FR-068, FR-071, FR-074, FR-075): framing and adjustments, then the sheet.
/// The portrait is only read; outputs go to the remembered folder with timestamp names.
/// </summary>
public sealed partial class IdViewModel : ViewModelBase
{
    public const double PreviewFrameHeight = 600;
    private const int ProxyEdge = 1800;

    private readonly AppServices _services;
    private readonly SemaphoreSlim _renderGate = new(1, 1);
    private readonly List<(IdFraming F, IdAdjustments A)> _undo = [];
    private MagickImage? _source;
    private MagickImage? _proxy;
    private double _proxyScale = 1;
    private int _renderVersion;
    private bool _syncing;
    private IdFraming? _initial;

    public IdViewModel(AppServices services)
    {
        _services = services;
        foreach (var s in IdSizes.BuiltIn) Sizes.Add(new IdSizeItem(s));
        SelectedSize = Sizes[1];
        Sizes[1].IsSelected = true;
        OutputFolder = services.Settings.IdOutputFolder;
        Localizer.Instance.LanguageChanged += (_, _) =>
        {
            foreach (var s in Sizes) s.RefreshTexts();
            OnPropertyChanged(nameof(OutputFolderText));
            RaiseSheet();
        };
    }

    /// <summary>Set by the view: file and folder pickers (not available in headless tests).</summary>
    public Func<Task<string?>>? PickPhoto { get; set; }

    public Func<Task<string?>>? PickFolder { get; set; }

    public ObservableCollection<IdSizeItem> Sizes { get; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(FrameRatio), nameof(Capacity), nameof(FamiliarCounts))]
    public partial IdSizeItem SelectedSize { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSource))]
    public partial string? SourcePath { get; set; }

    [ObservableProperty]
    public partial IdFraming? Framing { get; set; }

    [ObservableProperty]
    public partial Bitmap? Preview { get; set; }

    [ObservableProperty]
    public partial Bitmap? SheetPreview { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsFraming))]
    public partial bool IsSheetStep { get; set; }

    [ObservableProperty]
    public partial bool IsBusy { get; set; }

    [ObservableProperty]
    public partial string? StatusMessage { get; set; }

    [ObservableProperty]
    public partial string? ErrorMessage { get; set; }

    [ObservableProperty]
    public partial double Zoom { get; set; } = 100;

    [ObservableProperty]
    public partial double Rotation { get; set; }

    [ObservableProperty]
    public partial double Exposure { get; set; }

    [ObservableProperty]
    public partial double Contrast { get; set; }

    [ObservableProperty]
    public partial double Temperature { get; set; }

    [ObservableProperty]
    public partial double Tint { get; set; }

    [ObservableProperty]
    public partial double BackgroundBrightness { get; set; }

    [ObservableProperty]
    public partial bool ShowBefore { get; set; }

    [ObservableProperty]
    public partial decimal CustomWidth { get; set; } = 30;

    [ObservableProperty]
    public partial decimal CustomHeight { get; set; } = 40;

    // Sheet options
    [ObservableProperty]
    public partial int Copies { get; set; } = 6;

    [ObservableProperty]
    public partial decimal MarginMm { get; set; } = 3;

    [ObservableProperty]
    public partial decimal SpacingMm { get; set; } = 2;

    [ObservableProperty]
    public partial bool CutGuides { get; set; } = true;

    [ObservableProperty]
    public partial bool SaveAsPng { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasOutputFolder), nameof(OutputFolderText))]
    public partial string? OutputFolder { get; set; }

    public bool HasSource => SourcePath is not null;

    public bool IsFraming => !IsSheetStep;

    public bool HasOutputFolder => !string.IsNullOrEmpty(OutputFolder);

    public string OutputFolderText => HasOutputFolder ? OutputFolder! : Localizer.Instance["NoOutputFolder"];

    public double FrameRatio => SelectedSize.Size.Ratio;

    public int Capacity => SheetLayout.CapacityFor(SelectedSize.Size, (double)MarginMm, (double)SpacingMm);

    public IReadOnlyList<int> FamiliarCounts => SelectedSize.Size.FamiliarCounts.Where(c => c <= Capacity).Append(Capacity).Distinct().Order().ToList();

    public string SourceName => SourcePath is null ? string.Empty : Path.GetFileName(SourcePath);

    public IdAdjustments Adjustments => new(Exposure, Contrast, Temperature, Tint, BackgroundBrightness);

    public string CopiesText => Localizer.Instance.Format("OfPlaces", Capacity);

    public string FileNamePreview => IdFileNames.Base(DateTime.Now) + (SaveAsPng ? ".png" : ".jpg");

    public string PhotoInfo => Localizer.Instance.Format("IdPhotoInfo", SelectedSize.Label, SelectedSize.Size.WidthPx, SelectedSize.Size.HeightPx)
        + (SelectedSize.Size.StripBelowMm > 0 ? "  ·  " + Localizer.Instance["StripNote"] : string.Empty);

    public string ExposureText => Localizer.Instance.Format("ExposureValue", Exposure);

    public string RotationText => string.Format(Localizer.Instance.Culture, "{0:+0.0;−0.0;0.0}°", Rotation);

    public string ZoomText => string.Format(Localizer.Instance.Culture, "{0:0}%", Zoom);

    // ------------------------------------------------------------------ source

    [RelayCommand]
    private async Task OpenPhotoAsync()
    {
        if (PickPhoto is null) return;
        var path = await PickPhoto();
        if (path is not null) await LoadSourceAsync(path);
    }

    /// <summary>Loads a portrait (read-only). Used by Open photo and by "Use for ID photo" in the Photos workspace.</summary>
    public async Task LoadSourceAsync(string path)
    {
        ErrorMessage = null;
        IsBusy = true;
        try
        {
            var (source, proxy) = await Task.Run(() =>
            {
                var full = ImageLoader.Load(path);
                var small = (MagickImage)full.Clone();
                if (Math.Max(small.Width, small.Height) > ProxyEdge) small.Resize(new MagickGeometry(ProxyEdge, ProxyEdge));
                return (full, small);
            });
            await _renderGate.WaitAsync();
            try
            {
                _source?.Dispose();
                _proxy?.Dispose();
                _source = source;
                _proxy = proxy;
                _proxyScale = (double)proxy.Width / source.Width;
            }
            finally
            {
                _renderGate.Release();
            }

            SourcePath = path;
            OnPropertyChanged(nameof(SourceName));
            IsSheetStep = false;
            ResetAll();
            Log.Information("ID: loaded portrait {Path}", path);
        }
        catch (ImageLoadException ex)
        {
            ErrorMessage = ErrorTexts.ForImage(ex.Error);
        }
        finally
        {
            IsBusy = false;
        }
    }

    // ------------------------------------------------------------------ sizes and framing

    [RelayCommand]
    private void SelectSize(IdSizeItem item)
    {
        foreach (var s in Sizes) s.IsSelected = s == item;
        SelectedSize = item;
        Copies = item.Size.FamiliarCounts.Where(c => c <= Capacity).DefaultIfEmpty(1).Max();
        if (_source is not null && Framing is not null)
        {
            // Keep the centre and the frame height; adapt the width to the new ratio.
            var height = Framing.FrameWidthSource / PreviousRatio;
            Framing = Framing with { FrameWidthSource = height * item.Size.Ratio };
            _initial = IdFraming.Initial(_source.Width, _source.Height, item.Size);
        }

        PreviousRatio = item.Size.Ratio;
        SyncSliders();
        Rerender();
        RaiseSheet();
    }

    private double PreviousRatio { get; set; } = IdSizes.S35x45.Ratio;

    [RelayCommand]
    private void ApplyCustomSize()
    {
        if (CustomWidth <= 0 || CustomHeight <= 0) return;
        var custom = new IdSizeItem(IdSizes.Custom((double)CustomWidth, (double)CustomHeight));
        var existing = Sizes.FirstOrDefault(s => s.Size.Id == "custom");
        if (existing is not null) Sizes.Remove(existing);
        Sizes.Add(custom);
        SelectSize(custom);
    }

    public void MoveBy(double dxPreview, double dyPreview)
    {
        if (Framing is null) return;
        var s = PreviewFrameWidth / Framing.FrameWidthSource;
        var a = Framing.RotationDeg * Math.PI / 180;
        // The photo moves with the pointer, so the frame centre moves the other way (in rotated source coordinates).
        var dx = -(dxPreview * Math.Cos(a) - dyPreview * Math.Sin(a)) / s;
        var dy = -(dxPreview * Math.Sin(a) + dyPreview * Math.Cos(a)) / s;
        SetFraming(Framing with { CenterX = Framing.CenterX + dx, CenterY = Framing.CenterY + dy }, "move");
    }

    public void ZoomBy(double factor)
    {
        if (Framing is null) return;
        SetFraming(Framing with { FrameWidthSource = Math.Clamp(Framing.FrameWidthSource / factor, 20, 100000) }, "zoom");
    }

    public void RotateBy(double degrees)
    {
        if (Framing is null) return;
        SetFraming(Framing with { RotationDeg = Math.Clamp(Framing.RotationDeg + degrees, -20, 20) }, "rotate");
    }

    partial void OnZoomChanged(double value)
    {
        OnPropertyChanged(nameof(ZoomText));
        if (_syncing || Framing is null || _initial is null) return;
        SetFraming(Framing with { FrameWidthSource = _initial.FrameWidthSource * 100 / Math.Max(10, value) }, "zoom");
    }

    partial void OnRotationChanged(double value)
    {
        OnPropertyChanged(nameof(RotationText));
        if (_syncing || Framing is null) return;
        SetFraming(Framing with { RotationDeg = Math.Round(value, 1) }, "rotate");
    }

    partial void OnExposureChanged(double value) => AdjustmentChanged(nameof(ExposureText));

    partial void OnContrastChanged(double value) => AdjustmentChanged(null);

    partial void OnTemperatureChanged(double value) => AdjustmentChanged(null);

    partial void OnTintChanged(double value) => AdjustmentChanged(null);

    partial void OnBackgroundBrightnessChanged(double value) => AdjustmentChanged(null);

    partial void OnShowBeforeChanged(bool value) => Rerender();

    partial void OnCopiesChanged(int value) => RaiseSheet();

    partial void OnMarginMmChanged(decimal value) => RaiseSheet();

    partial void OnSpacingMmChanged(decimal value) => RaiseSheet();

    partial void OnCutGuidesChanged(bool value) => RaiseSheet();

    partial void OnSaveAsPngChanged(bool value) => OnPropertyChanged(nameof(FileNamePreview));

    [RelayCommand]
    private void ExposureMinus() => Exposure = Math.Clamp(Math.Round((Exposure - 1.0 / 6) * 6) / 6, -2, 2);

    [RelayCommand]
    private void ExposurePlus() => Exposure = Math.Clamp(Math.Round((Exposure + 1.0 / 6) * 6) / 6, -2, 2);

    [RelayCommand]
    private void ResetFraming()
    {
        if (_initial is not null) SetFraming(_initial, null);
    }

    [RelayCommand]
    private void ResetAll()
    {
        if (_source is null) return;
        _initial = IdFraming.Initial(_source.Width, _source.Height, SelectedSize.Size);
        _undo.Clear();
        _syncing = true;
        Exposure = Contrast = Temperature = Tint = BackgroundBrightness = 0;
        _syncing = false;
        Framing = _initial;
        SyncSliders();
        Rerender();
    }

    [RelayCommand]
    private void Undo()
    {
        if (_undo.Count == 0) return;
        var (f, a) = _undo[^1];
        _undo.RemoveAt(_undo.Count - 1);
        _syncing = true;
        Exposure = a.Exposure;
        Contrast = a.Contrast;
        Temperature = a.Temperature;
        Tint = a.Tint;
        BackgroundBrightness = a.BackgroundBrightness;
        _syncing = false;
        Framing = f;
        SyncSliders();
        Rerender();
    }

    [RelayCommand]
    private void ContinueToSheet()
    {
        if (_source is null) return;
        IsSheetStep = true;
        _ = RenderSheetPreviewAsync();
    }

    [RelayCommand]
    private void BackToFraming() => IsSheetStep = false;

    [RelayCommand]
    private void SetCopies(int count) => Copies = Math.Clamp(count, 1, Math.Max(1, Capacity));

    [RelayCommand]
    private void CopiesMinus() => Copies = Math.Max(1, Copies - 1);

    [RelayCommand]
    private void CopiesPlus() => Copies = Math.Min(Math.Max(1, Capacity), Copies + 1);

    public bool HandleKey(Key key, KeyModifiers modifiers)
    {
        if (IsSheetStep || Framing is null) return false;
        var step = modifiers.HasFlag(KeyModifiers.Shift) ? 10 : 1;
        switch (key)
        {
            case Key.Left: MoveBy(-step, 0); return true;
            case Key.Right: MoveBy(step, 0); return true;
            case Key.Up: MoveBy(0, -step); return true;
            case Key.Down: MoveBy(0, step); return true;
            case Key.Z when modifiers.HasFlag(KeyModifiers.Control): Undo(); return true;
            default: return false;
        }
    }

    public double PreviewFrameWidth => PreviewFrameHeight * FrameRatio;

    public int ViewportWidth => (int)Math.Round(PreviewFrameWidth * 1.8);

    public int ViewportHeight => (int)Math.Round(PreviewFrameHeight * 1.3);

    private void AdjustmentChanged(string? textProperty)
    {
        if (textProperty is not null) OnPropertyChanged(textProperty);
        if (_syncing || Framing is null) return;
        PushUndo("adjust");
        Rerender();
    }

    private string? _lastTag;
    private DateTime _lastTime;

    private void PushUndo(string? tag)
    {
        if (Framing is null) return;
        if (tag is not null && tag == _lastTag && (DateTime.UtcNow - _lastTime).TotalMilliseconds < 800) return;
        _lastTag = tag;
        _lastTime = DateTime.UtcNow;
        _undo.Add((Framing, Adjustments));
        if (_undo.Count > 100) _undo.RemoveAt(0);
    }

    private void SetFraming(IdFraming next, string? tag)
    {
        if (next == Framing) return;
        PushUndo(tag);
        Framing = next;
        SyncSliders();
        Rerender();
    }

    private void SyncSliders()
    {
        if (Framing is null || _initial is null) return;
        _syncing = true;
        Zoom = Math.Round(_initial.FrameWidthSource / Framing.FrameWidthSource * 100, 1);
        Rotation = Framing.RotationDeg;
        _syncing = false;
    }

    private void Rerender() => _ = RenderPreviewAsync();

    private async Task RenderPreviewAsync()
    {
        var version = Interlocked.Increment(ref _renderVersion);
        await _renderGate.WaitAsync();
        try
        {
            if (version != _renderVersion || _proxy is null || Framing is null) return;
            var proxy = _proxy;
            var framing = Framing;
            var size = SelectedSize.Size;
            var adj = ShowBefore ? new IdAdjustments() : Adjustments;
            var (fw, vw, vh, k) = ((int)Math.Round(PreviewFrameWidth), ViewportWidth, ViewportHeight, _proxyScale);
            var data = await Task.Run(() =>
            {
                using var img = IdRenderer.RenderPreview(proxy, k, framing, size, adj, fw, vw, vh);
                return PixelData.From(img);
            });
            var old = Preview;
            Preview = BitmapFactory.Create(data);
            (old as IDisposable)?.Dispose();
        }
        catch (MagickException ex)
        {
            Log.Error(ex, "ID preview failed");
        }
        finally
        {
            _renderGate.Release();
        }
    }

    // ------------------------------------------------------------------ sheet

    private SheetLayout CurrentLayout() =>
        SheetLayout.Compute(SelectedSize.Size, new SheetOptions(Copies, (double)MarginMm, (double)SpacingMm, CutGuides));

    private void RaiseSheet()
    {
        OnPropertyChanged(nameof(Capacity));
        OnPropertyChanged(nameof(FamiliarCounts));
        OnPropertyChanged(nameof(CopiesText));
        OnPropertyChanged(nameof(PhotoInfo));
        OnPropertyChanged(nameof(FileNamePreview));
        if (Copies > Capacity && Capacity > 0) Copies = Capacity;
        if (IsSheetStep) _ = RenderSheetPreviewAsync();
    }

    private async Task RenderSheetPreviewAsync()
    {
        if (_proxy is null || Framing is null) return;
        var proxy = _proxy;
        var framing = Framing with { CenterX = Framing.CenterX * _proxyScale, CenterY = Framing.CenterY * _proxyScale, FrameWidthSource = Framing.FrameWidthSource * _proxyScale };
        var size = SelectedSize.Size;
        var adj = Adjustments;
        var layout = CurrentLayout();
        var guides = CutGuides;
        var data = await Task.Run(() =>
        {
            using var photo = IdRenderer.Render(proxy, framing, size, adj);
            using var sheet = SheetRenderer.Render(photo, layout, guides);
            return PixelData.From(sheet);
        });
        var old = SheetPreview;
        SheetPreview = BitmapFactory.Create(data);
        (old as IDisposable)?.Dispose();
    }

    [RelayCommand]
    private async Task ChangeOutputFolderAsync()
    {
        if (PickFolder is null) return;
        var folder = await PickFolder();
        if (folder is null) return;
        OutputFolder = folder;
        _services.Settings.IdOutputFolder = folder;
        _services.SaveSettings();
    }

    /// <summary>Renders the full-resolution sheet and writes it with a timestamp name (FR-068, FR-075).</summary>
    [RelayCommand]
    public async Task<string?> SaveSheetAsync()
    {
        if (_source is null || Framing is null || IsBusy) return null;
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
            var source = _source;
            var framing = Framing;
            var size = SelectedSize.Size;
            var adj = Adjustments;
            var layout = CurrentLayout();
            var guides = CutGuides;
            var kind = SaveAsPng ? ImageKind.Png : ImageKind.Jpeg;
            var folder = OutputFolder!;
            var quality = _services.Settings.JpegQuality;
            var path = await Task.Run(() =>
            {
                using var photo = IdRenderer.Render(source, framing, size, adj);
                using var sheet = SheetRenderer.Render(photo, layout, guides);
                var bytes = IdOutput.Encode(sheet, kind, quality);
                return IdExporter.Write(folder, bytes, kind == ImageKind.Png ? ".png" : ".jpg", DateTime.Now);
            });
            StatusMessage = Localizer.Instance.Format("IdSaved", Path.GetFileName(path));
            Log.Information("ID sheet saved: {Path} ({Size}, {Copies} copies)", path, size.Id, layout.Photos.Count);
            return path;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or MagickException)
        {
            StatusMessage = null;
            ErrorMessage = Localizer.Instance.Format("ErrWriteFailed", ex.Message);
            Log.Warning(ex, "ID sheet save failed");
            return null;
        }
        finally
        {
            IsBusy = false;
            OnPropertyChanged(nameof(FileNamePreview));
        }
    }

    /// <summary>Writes the 10×15 and 15×20 calibration sheets to the output folder (FR-071).</summary>
    [RelayCommand]
    private async Task SaveCalibrationAsync()
    {
        if (!HasOutputFolder)
        {
            await ChangeOutputFolderAsync();
            if (!HasOutputFolder) return;
        }

        var folder = OutputFolder!;
        await Task.Run(() =>
        {
            using var a = CalibrationSheet.TenByFifteen();
            IdExporter.Write(folder, IdOutput.Encode(a, ImageKind.Png), ".png", DateTime.Now, "PhotoX_calibration_10x15");
            using var b = CalibrationSheet.FifteenByTwenty();
            IdExporter.Write(folder, IdOutput.Encode(b, ImageKind.Png), ".png", DateTime.Now, "PhotoX_calibration_15x20");
        });
        StatusMessage = Localizer.Instance["CalibrationSaved"];
    }
}
