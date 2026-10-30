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
using PhotoX.Imaging.Faces;
using PhotoX.Imaging.Id;
using PhotoX.Storage;
using Serilog;

namespace PhotoX.App.ViewModels;

public sealed record FaceChoice(int Number, string Text, DetectedFace Face);

/// <summary>An ID size in the size list, labelled in centimetres (no pixel sizes, prompt_04 §8).</summary>
public sealed partial class IdSizeItem(IdSize size) : ViewModelBase
{
    public IdSize Size { get; } = size;

    public string Label => Localizer.Instance.Language == "ro" ? Size.Label.Replace('.', ',') : Size.Label;

    /// <summary>The centimetre equivalent of inch sizes (2 × 2 in ≈ 5,1 × 5,1 cm).</summary>
    public string? Detail => Size.Approximation is { } a ? (Localizer.Instance.Language == "ro" ? a.Replace('.', ',') : a) : null;

    public bool HasDetail => Detail is not null;

    [ObservableProperty]
    public partial bool IsSelected { get; set; }

    public void RefreshTexts()
    {
        OnPropertyChanged(nameof(Label));
        OnPropertyChanged(nameof(Detail));
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
    private List<MagickImage> _levels = [];
    private double _proxyScale = 1;
    private int _renderVersion;
    private bool _syncing;
    private IdFraming? _initial;

    public IdViewModel(AppServices services)
    {
        _services = services;
        Tone = new ToneAdjustmentsViewModel(AutoAdjustCommand, "AutoAdjust");
        Tone.Changed += tag =>
        {
            if (_syncing || Framing is null) return;
            PushUndo(tag ?? "adjust");
            Rerender();
        };
        Picker = new PhotoPickerViewModel(services) { ThumbWidth = 124, ThumbHeight = 124, PickOnSelect = true };
        Picker.Picked += photo => _ = LoadSourceAsync(photo.Path);
        foreach (var s in IdSizes.BuiltIn) Sizes.Add(new IdSizeItem(s));
        foreach (var c in services.Settings.CustomIdSizes.Where(c => IdSizes.IsValidCustom(c.WidthMm, c.HeightMm)))
        {
            CustomSizes.Add(new IdSizeItem(IdSizes.Custom(c.WidthMm, c.HeightMm)));
        }

        var start = Sizes.FirstOrDefault(s => s.Size.Id == IdSizes.S35x45.Id) ?? Sizes[0];
        SelectedSize = start;
        start.IsSelected = true;
        PreviousRatio = start.Size.Ratio;
        OutputFolder = services.Settings.IdOutputFolder;
        Localizer.Instance.LanguageChanged += (_, _) =>
        {
            foreach (var s in Sizes.Concat(CustomSizes)) s.RefreshTexts();
            OnPropertyChanged(nameof(OutputFolderText));
            RaiseSheet();
        };
    }

    /// <summary>Histogram, levels and tone sliders, the same controls as in the Photos editor (prompt_03 §7).</summary>
    public ToneAdjustmentsViewModel Tone { get; }

    /// <summary>The built-in explorer, under the size list: a click on a photo loads it (prompt_05 §7).</summary>
    public PhotoPickerViewModel Picker { get; }

    /// <summary>Raised by Print on the sheet step with the sheet to print and its paper size (prompt_05 §7).</summary>
    public event Action<Printing.PrintItem, Core.Formats.PrintFormat>? PrintRequested;

    /// <summary>Set by the shell: the folder open in Photos, where the picker starts.</summary>
    public Func<string?>? StartFolder { get; set; }

    /// <summary>Set by the view: the folder picker for the output folder (not available in headless tests).</summary>
    public Func<Task<string?>>? PickFolder { get; set; }

    /// <summary>Built-in sizes from the data file.</summary>
    public ObservableCollection<IdSizeItem> Sizes { get; } = [];

    /// <summary>Sizes added with + New size; kept apart from the built-in ones and removable (prompt_04 §8).</summary>
    public ObservableCollection<IdSizeItem> CustomSizes { get; } = [];

    [ObservableProperty]
    public partial bool IsAddingSize { get; set; }

    /// <summary>New size in centimetres.</summary>
    [ObservableProperty]
    public partial decimal NewSizeWidth { get; set; } = 3;

    [ObservableProperty]
    public partial decimal NewSizeHeight { get; set; } = 4;

    [ObservableProperty]
    public partial string? NewSizeError { get; set; }

    /// <summary>Faces to choose from when Auto Align finds more than one (FR-063).</summary>
    public ObservableCollection<FaceChoice> FaceChoices { get; } = [];

    public bool HasFaceChoices => FaceChoices.Count > 0;

    private static readonly Lazy<FaceDetector?> Detector = new(() =>
    {
        try
        {
            return File.Exists(FaceDetector.DefaultModelPath) ? new FaceDetector(FaceDetector.DefaultModelPath) : null;
        }
        catch (Exception ex) when (ex is Microsoft.ML.OnnxRuntime.OnnxRuntimeException or DllNotFoundException or TypeInitializationException)
        {
            Log.Error(ex, "Face detector unavailable");
            return null;
        }
    });

    /// <summary>Auto Align (FR-063): runs only when pressed; the result is ordinary, undoable framing.</summary>
    [RelayCommand]
    public async Task AutoAlignAsync()
    {
        if (_proxy is null || IsBusy || !HasGuides) return;
        IsBusy = true;
        ErrorMessage = null;
        StatusMessage = null;
        ClearFaceChoices();
        try
        {
            await _renderGate.WaitAsync();
            IReadOnlyList<DetectedFace> faces;
            try
            {
                var proxy = _proxy;
                var detector = Detector.Value;
                if (detector is null)
                {
                    ErrorMessage = Localizer.Instance["AutoAlignUnavailable"];
                    return;
                }

                faces = await Task.Run(() =>
                {
                    lock (detector) return detector.Detect(proxy);
                });
            }
            finally
            {
                _renderGate.Release();
            }

            Log.Information("Auto Align: {Count} face(s)", faces.Count);
            switch (faces.Count)
            {
                case 0:
                    ErrorMessage = Localizer.Instance["AutoAlignNoFace"];
                    break;
                case 1:
                    ApplyFace(faces[0]);
                    break;
                default:
                    for (var i = 0; i < faces.Count; i++)
                    {
                        var pos = faces.Count == 2 ? (i == 0 ? "PosLeft" : "PosRight") : (i == 0 ? "PosLeft" : i == faces.Count - 1 ? "PosRight" : "PosMiddle");
                        FaceChoices.Add(new FaceChoice(i + 1, Localizer.Instance.Format("FaceN", i + 1, Localizer.Instance[pos]), faces[i]));
                    }

                    OnPropertyChanged(nameof(HasFaceChoices));
                    StatusMessage = Localizer.Instance["AutoAlignSeveral"];
                    break;
            }
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void ChooseFace(FaceChoice choice)
    {
        ClearFaceChoices();
        ApplyFace(choice.Face);
    }

    [RelayCommand]
    private void KeepManualFraming()
    {
        ClearFaceChoices();
        StatusMessage = null;
    }

    private void ClearFaceChoices()
    {
        FaceChoices.Clear();
        OnPropertyChanged(nameof(HasFaceChoices));
    }

    private void ApplyFace(DetectedFace face)
    {
        var k = _proxyScale;
        var f = IdFraming.FromFace(face.LeftEye.X / k, face.LeftEye.Y / k, face.RightEye.X / k, face.RightEye.Y / k, face.HeadHeightEstimate / k, SelectedSize.Size);
        SetFraming(f, null);
        StatusMessage = Localizer.Instance["AutoAlignDone"];
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(FrameRatio), nameof(PreviewFrameWidth), nameof(Capacity), nameof(FamiliarCounts), nameof(HasStripOption), nameof(HasGuides), nameof(CanAutoAlign))]
    public partial IdSizeItem SelectedSize { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSource), nameof(ShowFraming), nameof(ShowSheet))]
    public partial string? SourcePath { get; set; }

    [ObservableProperty]
    public partial IdFraming? Framing { get; set; }

    [ObservableProperty]
    public partial Bitmap? Preview { get; set; }

    [ObservableProperty]
    public partial Bitmap? SheetPreview { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsFraming), nameof(ShowFraming), nameof(ShowSheet))]
    public partial bool IsSheetStep { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanAutoAlign))]
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
    public partial double BackgroundBrightness { get; set; }

    [ObservableProperty]
    public partial bool ShowBefore { get; set; }

    // Sheet options
    [ObservableProperty]
    public partial int Copies { get; set; } = 6;

    [ObservableProperty]
    public partial decimal MarginMm { get; set; } = 3;

    [ObservableProperty]
    public partial decimal SpacingMm { get; set; } = 2;

    /// <summary>3×4 only: keep the 7 mm white strip below each photo (FR-074).</summary>
    [ObservableProperty]
    public partial bool IncludeStrip { get; set; } = true;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasOutputFolder), nameof(OutputFolderText))]
    public partial string? OutputFolder { get; set; }

    public bool HasSource => SourcePath is not null;

    public bool IsFraming => !IsSheetStep;

    /// <summary>The framing canvas is shown (a portrait is loaded).</summary>
    public bool ShowFraming => HasSource && !IsSheetStep;

    public bool ShowSheet => HasSource && IsSheetStep;

    /// <summary>Face guides and Auto Align; the 9×12 police photo is framed by hand (prompt_04 §8).</summary>
    public bool HasGuides => SelectedSize.Size.Guides;

    public bool CanAutoAlign => HasGuides && !IsBusy;

    public bool HasOutputFolder => !string.IsNullOrEmpty(OutputFolder);

    public string OutputFolderText => HasOutputFolder ? OutputFolder! : Localizer.Instance["NoOutputFolder"];

    public double FrameRatio => SelectedSize.Size.Ratio;

    public int Capacity => SheetLayout.CapacityFor(SelectedSize.Size, (double)MarginMm, (double)SpacingMm, IncludeStrip);

    public bool HasStripOption => SelectedSize.Size.StripBelowMm > 0;

    public string PageText => Localizer.Instance[CurrentLayout().Landscape ? "PageLandscape" : "PagePortrait"];

    public IReadOnlyList<int> FamiliarCounts => SelectedSize.Size.FamiliarCounts.Where(c => c <= Capacity).Append(Capacity).Distinct().Order().ToList();

    public string SourceName => SourcePath is null ? string.Empty : Path.GetFileName(SourcePath);

    public IdAdjustments Adjustments => new(Tone.ApplyTo(new Core.Editing.EditRecipe()), BackgroundBrightness);

    public string CopiesText => Localizer.Instance.Format("OfPlaces", Capacity);

    /// <summary>ID sheets are saved as JPG only (prompt_04 §8).</summary>
    public string FileNamePreview => IdFileNames.Base(DateTime.Now) + ".jpg";

    /// <summary>Without page margins the outer photos reach the paper edge (2 × 2 in: four fit only this way).</summary>
    public bool IsEdgeToEdge => MarginMm == 0;

    public string PhotoInfo => Localizer.Instance.Format("IdPhotoInfo", SelectedSize.Label, SelectedSize.Size.WidthPx, SelectedSize.Size.HeightPx)
        + (HasStripOption && IncludeStrip ? "  ·  " + Localizer.Instance["StripNote"] : string.Empty);

    public string RotationText => string.Format(Localizer.Instance.Culture, "{0:+0.0;−0.0;0.0}°", Rotation);

    public string ZoomText => string.Format(Localizer.Instance.Culture, "{0:0}%", Zoom);

    // ------------------------------------------------------------------ source

    /// <summary>Called when the workspace is shown: the explorer opens the order of the Photos workspace the first time.</summary>
    public void Activate() => Picker.EnsureStarted(SourcePath is { } p ? Path.GetDirectoryName(p) : StartFolder?.Invoke());

    /// <summary>Loads a portrait (read-only). Used by Open photo and by "Use for ID photo" in the Photos workspace.</summary>
    public async Task LoadSourceAsync(string path)
    {
        ErrorMessage = null;
        IsBusy = true;
        try
        {
            var (source, proxy, levels) = await Task.Run(() =>
            {
                var full = ImageLoader.Load(path);
                var small = (MagickImage)full.Clone();
                if (Math.Max(small.Width, small.Height) > ProxyEdge) small.Resize(new MagickGeometry(ProxyEdge, ProxyEdge));
                // Half and quarter size copies keep the framing preview fast at any zoom.
                var list = new List<MagickImage> { small };
                for (var i = 0; i < 2 && list[^1].Width > 400; i++)
                {
                    var half = (MagickImage)list[^1].Clone();
                    half.Resize(new Percentage(50));
                    list.Add(half);
                }

                return (full, small, list);
            });
            await _renderGate.WaitAsync();
            try
            {
                _source?.Dispose();
                foreach (var l in _levels) l.Dispose();
                _source = source;
                _proxy = proxy;
                _levels = levels;
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
        foreach (var s in Sizes.Concat(CustomSizes)) s.IsSelected = s == item;
        SelectedSize = item;
        // Sizes may bring their own sheet margins (2 × 2 in: none, so four fit, prompt_04 §8).
        MarginMm = (decimal)(item.Size.SheetMarginMm ?? 3);
        SpacingMm = (decimal)(item.Size.SheetSpacingMm ?? 2);
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

    private double PreviousRatio { get; set; }

    [RelayCommand]
    private void ToggleNewSize()
    {
        IsAddingSize = !IsAddingSize;
        NewSizeError = null;
    }

    /// <summary>+ New size (prompt_04 §8): adds a removable size in centimetres, remembered in the settings.</summary>
    [RelayCommand]
    public void AddCustomSize()
    {
        var w = (double)NewSizeWidth * 10;
        var h = (double)NewSizeHeight * 10;
        if (!IdSizes.IsValidCustom(w, h))
        {
            NewSizeError = Localizer.Instance["NewSizeInvalid"];
            return;
        }

        var size = IdSizes.Custom(w, h);
        var item = CustomSizes.FirstOrDefault(c => c.Size.Id == size.Id);
        if (item is null)
        {
            item = new IdSizeItem(size);
            CustomSizes.Add(item);
            _services.Settings.CustomIdSizes.Add(new CustomIdSize(size.WidthMm, size.HeightMm));
            _services.SaveSettings();
        }

        IsAddingSize = false;
        NewSizeError = null;
        SelectSize(item);
    }

    [RelayCommand]
    public void RemoveCustomSize(IdSizeItem item)
    {
        if (!CustomSizes.Remove(item)) return;
        _services.Settings.CustomIdSizes.RemoveAll(c => IdSizes.Custom(c.WidthMm, c.HeightMm).Id == item.Size.Id);
        _services.SaveSettings();
        if (item.IsSelected) SelectSize(Sizes.FirstOrDefault(s => s.Size.Id == IdSizes.S35x45.Id) ?? Sizes[0]);
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

    partial void OnBackgroundBrightnessChanged(double value)
    {
        if (_syncing || Framing is null) return;
        PushUndo("background");
        Rerender();
    }

    partial void OnShowBeforeChanged(bool value) => Rerender();

    partial void OnCopiesChanged(int value) => RaiseSheet();

    partial void OnMarginMmChanged(decimal value)
    {
        OnPropertyChanged(nameof(IsEdgeToEdge));
        RaiseSheet();
    }

    partial void OnSpacingMmChanged(decimal value) => RaiseSheet();

    partial void OnIncludeStripChanged(bool value) => RaiseSheet();

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
        Tone.Load(new Core.Editing.EditRecipe());
        BackgroundBrightness = 0;
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
        Tone.Load(a.Tone);
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

    /// <summary>Framing keys; Space (held) shows the photo without adjustments, the only before/after control here (prompt_04 §8).</summary>
    public bool HandleKey(Key key, KeyModifiers modifiers)
    {
        if (!ShowFraming || Framing is null) return false;
        var step = modifiers.HasFlag(KeyModifiers.Shift) ? 10 : 1;
        var ctrl = modifiers.HasFlag(KeyModifiers.Control);
        switch (key)
        {
            case Key.Left: MoveBy(-step, 0); return true;
            case Key.Right: MoveBy(step, 0); return true;
            case Key.Up: MoveBy(0, -step); return true;
            case Key.Down: MoveBy(0, step); return true;
            case Key.Z when ctrl: Undo(); return true;
            case Key.Space: ShowBefore = true; return true;
            case Key.OemPlus or Key.Add when !ctrl: Tone.ExposurePlus(); return true;
            case Key.OemMinus or Key.Subtract when !ctrl: Tone.ExposureMinus(); return true;
            default: return false;
        }
    }

    public bool HandleKeyUp(Key key)
    {
        if (key != Key.Space || !ShowBefore) return false;
        ShowBefore = false;
        return true;
    }

    /// <summary>Double-click on the rotation slider (prompt_04 §8).</summary>
    public void ResetRotation()
    {
        if (Framing is not null) SetFraming(Framing with { RotationDeg = 0 }, null);
    }

    /// <summary>Double-click on the zoom slider: back to the initial zoom (prompt_04 §8).</summary>
    public void ResetZoom()
    {
        if (Framing is not null && _initial is not null) SetFraming(Framing with { FrameWidthSource = _initial.FrameWidthSource }, null);
    }

    /// <summary>
    /// Auto Adjust (prompt_04 §8): the Auto Correct estimation of the Photos editor, applied to the framed photo.
    /// The result is ordinary slider values: one undo step, adjustable afterwards.
    /// </summary>
    [RelayCommand]
    public async Task AutoAdjustAsync()
    {
        if (_lastFrame is not { } frame || Framing is null || IsBusy) return;
        var tone = Tone.ApplyTo(new Core.Editing.EditRecipe());
        var result = await Task.Run(() =>
            Core.Editing.AutoCorrect.Apply(tone, ImageStatistics.Histogram(frame.Pixels, frame.Region), ImageStatistics.NeutralMeans(frame.Pixels, frame.Region)));
        PushUndo(null);
        _syncing = true;
        Tone.Load(result with { Contrast = tone.Contrast, Saturation = tone.Saturation });
        _syncing = false;
        Rerender();
    }

    public double PreviewFrameWidth => PreviewFrameHeight * FrameRatio;

    public int ViewportWidth => (int)Math.Round(PreviewFrameWidth * 1.8);

    public int ViewportHeight => (int)Math.Round(PreviewFrameHeight * 1.3);

    private string? _lastTag;
    private DateTime _lastTime;
    private (PixelData Pixels, Core.Geometry.PixelRect Region)? _lastFrame;

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
            var framing = Framing;
            var size = SelectedSize.Size;
            var adj = ShowBefore ? new IdAdjustments() : Adjustments;
            var fw = (int)Math.Round(PreviewFrameWidth);
            var (vw, vh) = (ViewportWidth, ViewportHeight);
            var (proxy, factor) = IdRenderer.PreviewLevel(_levels, fw / (framing.FrameWidthSource * _proxyScale));
            var k = _proxyScale * factor;
            var frame = new Core.Geometry.PixelRect((vw - fw) / 2, (int)Math.Round((vh - PreviewFrameHeight) / 2), fw, (int)PreviewFrameHeight);
            var (data, unadjusted, histogram) = await Task.Run(() =>
            {
                // The unadjusted frame feeds Auto Adjust and the levels histogram (tones entering levels, prompt_04 §7).
                using var img = IdRenderer.FramePreview(proxy, k, framing, fw, vw, vh);
                var before = PixelData.From(img);
                var levelsInput = ImageStatistics.LevelsInput(before, frame, adj.Tone);
                IdRenderer.AdjustPreview(img, size, adj, fw);
                return (PixelData.From(img), before, levelsInput);
            });
            _lastFrame = (unadjusted, frame);
            var old = Preview;
            Preview = BitmapFactory.Create(data);
            (old as IDisposable)?.Dispose();
            Tone.Histogram = histogram;
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
        SheetLayout.Compute(SelectedSize.Size, new SheetOptions(Copies, (double)MarginMm, (double)SpacingMm, IncludeStrip));

    private void RaiseSheet()
    {
        OnPropertyChanged(nameof(Capacity));
        OnPropertyChanged(nameof(FamiliarCounts));
        OnPropertyChanged(nameof(CopiesText));
        OnPropertyChanged(nameof(PhotoInfo));
        OnPropertyChanged(nameof(FileNamePreview));
        OnPropertyChanged(nameof(PageText));
        OnPropertyChanged(nameof(HasStripOption));
        OnPropertyChanged(nameof(ViewportWidth));
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
        var scale = _services.Settings.IdScale;
        var (pw, ph) = SheetRenderer.PhotoPixels(size, scale);
        var data = await Task.Run(() =>
        {
            using var photo = IdRenderer.Render(proxy, framing, size, adj, pw, ph);
            using var sheet = SheetRenderer.Render(photo, layout, scale);
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
            var folder = OutputFolder!;
            var quality = _services.Settings.JpegQuality;
            var scale = _services.Settings.IdScale;
            var (pw, ph) = SheetRenderer.PhotoPixels(size, scale);
            var path = await Task.Run(() =>
            {
                using var photo = IdRenderer.Render(source, framing, size, adj, pw, ph);
                using var sheet = SheetRenderer.Render(photo, layout, scale);
                var bytes = IdOutput.Encode(sheet, ImageKind.Jpeg, quality);
                return IdExporter.Write(folder, bytes, ".jpg", DateTime.Now);
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

    /// <summary>
    /// Print (prompt_05 §7): the same sheet as Save sheet, rendered from the full-resolution portrait when printed,
    /// on 10×15 paper at its exact size. The preview renders from the screen-sized copy.
    /// </summary>
    [RelayCommand]
    private void PrintSheet()
    {
        if (_source is null || _proxy is null || Framing is null) return;
        var (source, proxy, proxyScale) = (_source, _proxy, _proxyScale);
        var framing = Framing;
        var size = SelectedSize.Size;
        var adj = Adjustments;
        var layout = CurrentLayout();
        var scale = _services.Settings.IdScale;
        var name = $"{SelectedSize.Label} × {layout.Photos.Count} · {SourceName}";
        PrintRequested?.Invoke(new Printing.ImagePrintItem(name, preview =>
        {
            var (pw, ph) = SheetRenderer.PhotoPixels(size, scale);
            var f = preview ? framing with { CenterX = framing.CenterX * proxyScale, CenterY = framing.CenterY * proxyScale, FrameWidthSource = framing.FrameWidthSource * proxyScale } : framing;
            using var photo = IdRenderer.Render(preview ? proxy : source, f, size, adj, pw, ph);
            return SheetRenderer.Render(photo, layout, scale);
        }, 1), Core.Formats.PrintFormats.P10x15);
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
