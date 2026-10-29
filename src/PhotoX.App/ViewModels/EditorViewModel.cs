using Avalonia;
using Avalonia.Input;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ImageMagick;
using PhotoX.App.Localization;
using PhotoX.App.Services;
using PhotoX.Core.Editing;
using PhotoX.Core.Formats;
using PhotoX.Core.Geometry;
using PhotoX.Imaging;
using PhotoX.Storage;
using Serilog;
using PixelRect = PhotoX.Core.Geometry.PixelRect;

namespace PhotoX.App.ViewModels;

/// <summary>
/// Ordinary-photo editor (FR-003, FR-004, FR-012, FR-013, FR-016–FR-019). Save &amp; Next saves the current
/// edits with exactly the selected crop, waits for success, then opens the next photo.
/// </summary>
public sealed partial class EditorViewModel : ViewModelBase
{
    public const int ProxyEdge = 1800;
    public const double ExposureStep = 1.0 / 6.0;

    private readonly AppServices _services;
    private readonly SemaphoreSlim _renderGate = new(1, 1);
    private readonly List<EditRecipe> _undo = [];
    private readonly List<EditRecipe> _redo = [];
    private MagickImage? _proxy;
    private int _renderVersion;
    private FileStamp _loadedStamp;
    private (int W, int H) _sourceSize;
    private EditRecipe _recipe = new();
    private EditRecipe _baseline = new();
    private string? _lastUndoTag;
    private DateTime _lastUndoTime;
    private bool _syncing;
    private PixelData? _lastPixels;
    private (string Path, Task<Loaded> Task)? _preload;

    private sealed record Loaded(FileStamp Stamp, EditRecipe? Saved, ImageInfo Info, MagickImage Proxy);

    public EditorViewModel(AppServices services, BrowseViewModel browse)
    {
        _services = services;
        Browse = browse;
        Localizer.Instance.LanguageChanged += (_, _) => RaiseInfo();
    }

    public event Action? BackRequested;

    public BrowseViewModel Browse { get; }

    public EditRecipe Recipe => _recipe;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PositionText), nameof(Title))]
    public partial PhotoItemViewModel? Current { get; set; }

    [ObservableProperty]
    public partial Bitmap? Preview { get; set; }

    [ObservableProperty]
    public partial Rect CropRect { get; set; }

    [ObservableProperty]
    public partial double CropRatio { get; set; } = 1.5;

    [ObservableProperty]
    public partial bool IsLoading { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsBusy))]
    public partial bool IsSaving { get; set; }

    [ObservableProperty]
    public partial string? StatusMessage { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    public partial string? ErrorMessage { get; set; }

    [ObservableProperty]
    public partial bool ShowBefore { get; set; }

    [ObservableProperty]
    public partial double Exposure { get; set; }

    [ObservableProperty]
    public partial double Contrast { get; set; }

    [ObservableProperty]
    public partial double Saturation { get; set; }

    [ObservableProperty]
    public partial double Temperature { get; set; }

    [ObservableProperty]
    public partial double Tint { get; set; }

    [ObservableProperty]
    public partial double LevelsBlack { get; set; }

    [ObservableProperty]
    public partial double LevelsMid { get; set; } = 1;

    [ObservableProperty]
    public partial double LevelsWhite { get; set; } = 1;

    [ObservableProperty]
    public partial double Straighten { get; set; }

    [ObservableProperty]
    public partial Histogram? Histogram { get; set; }

    [ObservableProperty]
    public partial Rect CropBounds { get; set; }

    [ObservableProperty]
    public partial decimal CustomWidth { get; set; } = 10;

    [ObservableProperty]
    public partial decimal CustomHeight { get; set; } = 15;

    public bool IsBusy => IsSaving || IsLoading;

    public bool HasError => ErrorMessage is not null;

    public bool IsDirty => _recipe != _baseline;

    public string FormatId => _recipe.FormatId;

    public bool IsWholeWithBorder => _recipe.Composition == Composition.WholeWithBorder;

    public bool ShowCrop => _recipe.Composition == Composition.Fill && !ShowBefore;

    public string ContrastText => Signed(Contrast);

    public string SaturationText => Signed(Saturation);

    public string TemperatureText => Signed(Temperature);

    public string TintText => Signed(Tint);

    public string LevelsText => string.Format(Localizer.Instance.Culture, "{0:0}  ·  {1:0.00}  ·  {2:0}", LevelsBlack * 255, LevelsMid, LevelsWhite * 255);

    public string StraightenText => string.Format(Localizer.Instance.Culture, "{0:+0.0;−0.0;0.0}°", Straighten);

    private static string Signed(double v) => v == 0 ? "0" : v.ToString("+0;−0", Localizer.Instance.Culture);

    public bool IsPortrait => _recipe.Orientation == CropOrientation.Portrait;

    public bool CanUndo => _undo.Count > 0;

    public bool CanRedo => _redo.Count > 0;

    public string Title => Current is null ? string.Empty : $"{Browse.FolderTitle}  ›  {Current.Name}";

    public string PositionText
    {
        get
        {
            var seq = Sequence;
            var i = Current is null ? -1 : seq.IndexOf(Current);
            return Localizer.Instance.Format("Position", i + 1, seq.Count);
        }
    }

    public string ExposureText => Localizer.Instance.Format("ExposureValue", Exposure);

    public double PreviewWidth => _proxy is null ? 1 : RotatedPreviewSize().W;

    public double PreviewHeight => _proxy is null ? 1 : RotatedPreviewSize().H;

    public string CropInfo
    {
        get
        {
            if (_sourceSize.W == 0) return string.Empty;
            var plan = RenderPlan.Create(_sourceSize.W, _sourceSize.H, _recipe);
            var format = PrintFormats.Find(_recipe.FormatId);
            return plan.EffectivePpi is double ppi && format is not null
                ? Localizer.Instance.Format("CropInfo", format.Name, plan.CanvasWidth, plan.CanvasHeight, Math.Round(ppi))
                : Localizer.Instance.Format("CropInfoNoPpi", plan.CanvasWidth, plan.CanvasHeight);
        }
    }

    /// <summary>Low-resolution warning (FR-047).</summary>
    public string? ResolutionWarning
    {
        get
        {
            if (_sourceSize.W == 0) return null;
            var ppi = RenderPlan.Create(_sourceSize.W, _sourceSize.H, _recipe).EffectivePpi;
            return ppi switch
            {
                < 150 => Localizer.Instance["VeryLowResWarning"],
                < 200 => Localizer.Instance["LowResWarning"],
                _ => null,
            };
        }
    }

    public List<PhotoItemViewModel> Sequence => Browse.AllPhotos.Where(p => p.IsOpenable).ToList();

    partial void OnExposureChanged(double value)
    {
        OnPropertyChanged(nameof(ExposureText));
        if (!_syncing) SetRecipe(_recipe with { Exposure = Math.Round(value, 3) }, "exposure");
    }

    partial void OnShowBeforeChanged(bool value)
    {
        OnPropertyChanged(nameof(ShowCrop));
        _ = RenderPreviewAsync();
    }

    partial void OnContrastChanged(double value) => SliderChanged(nameof(ContrastText), r => r with { Contrast = Math.Round(value) }, "contrast");

    partial void OnSaturationChanged(double value) => SliderChanged(nameof(SaturationText), r => r with { Saturation = Math.Round(value) }, "saturation");

    partial void OnTemperatureChanged(double value) => SliderChanged(nameof(TemperatureText), r => r with { Temperature = Math.Round(value) }, "temperature");

    partial void OnTintChanged(double value) => SliderChanged(nameof(TintText), r => r with { Tint = Math.Round(value) }, "tint");

    partial void OnLevelsBlackChanged(double value) => SliderChanged(nameof(LevelsText), r => r with { LevelsBlack = Math.Round(value, 3) }, "levels-black");

    partial void OnLevelsMidChanged(double value) => SliderChanged(nameof(LevelsText), r => r with { LevelsMid = Math.Round(value, 2) }, "levels-mid");

    partial void OnLevelsWhiteChanged(double value) => SliderChanged(nameof(LevelsText), r => r with { LevelsWhite = Math.Round(value, 3) }, "levels-white");

    partial void OnStraightenChanged(double value)
    {
        OnPropertyChanged(nameof(StraightenText));
        if (_syncing || _proxy is null) return;
        var next = _recipe with { Straighten = Math.Round(value, 1) };
        SetRecipe(next with { Crop = KeepCropInside(_recipe, next) }, "straighten");
    }

    private void SliderChanged(string textProperty, Func<EditRecipe, EditRecipe> change, string tag)
    {
        OnPropertyChanged(textProperty);
        if (!_syncing && _proxy is not null) SetRecipe(change(_recipe), tag);
    }

    // ------------------------------------------------------------------ navigation and saving

    public async Task OpenAsync(PhotoItemViewModel item)
    {
        KeepPending();
        if (Current is not null) Current.IsCurrent = false;
        Current = item;
        item.IsCurrent = true;
        ErrorMessage = null;
        StatusMessage = null;
        IsLoading = true;
        try
        {
            var loaded = await TakePreloadedAsync(item) ?? await Task.Run(() => Load(item.Path));
            if (Current != item)
            {
                loaded.Proxy.Dispose();
                return;
            }

            await _renderGate.WaitAsync();
            try
            {
                _proxy?.Dispose();
                _proxy = loaded.Proxy;
            }
            finally
            {
                _renderGate.Release();
            }

            _loadedStamp = loaded.Stamp;
            _sourceSize = (loaded.Info.OrientedWidth, loaded.Info.OrientedHeight);
            _baseline = loaded.Saved ?? DefaultRecipe();
            _recipe = item.PendingRecipe ?? _baseline;
            _undo.Clear();
            _redo.Clear();
            SyncUiFromRecipe();
            await RenderPreviewAsync();
            StartPreload(item);
        }
        catch (ImageLoadException ex)
        {
            item.State = PhotoState.Error;
            item.ErrorText = ErrorTexts.ForImage(ex.Error);
            ErrorMessage = item.ErrorText;
            Log.Warning("Cannot open {Path}: {Message}", item.Path, ex.Message);
        }
        finally
        {
            IsLoading = false;
            RaiseAll();
        }
    }

    private static Loaded Load(string path)
    {
        var store = new OrderStore(Path.GetDirectoryName(path)!);
        var stamp = FileStamp.Of(path) ?? throw new ImageLoadException(ImageError.Unreadable, "File missing");
        var sidecar = store.Load();
        var name = Path.GetFileName(path);
        var backup = SourceSelector.TrustedBackup(store, sidecar, name, stamp);
        var saved = SourceSelector.SavedRecipe(store, sidecar, name, stamp);
        var source = backup ?? path;
        return new Loaded(stamp, saved, ImageProbe.Probe(source), ImageLoader.Load(source, ProxyEdge));
    }

    /// <summary>Preloads the next photo in the background (FR-052): only one, to bound memory.</summary>
    private void StartPreload(PhotoItemViewModel current)
    {
        var seq = Sequence;
        var i = seq.IndexOf(current);
        if (i < 0 || i >= seq.Count - 1) return;
        var next = seq[i + 1];
        if (_preload?.Path == next.Path) return;
        DiscardPreload();
        var path = next.Path;
        _preload = (path, Task.Run(() => Load(path)));
    }

    private async Task<Loaded?> TakePreloadedAsync(PhotoItemViewModel item)
    {
        if (_preload is not { } p || p.Path != item.Path)
        {
            DiscardPreload();
            return null;
        }

        _preload = null;
        try
        {
            var loaded = await p.Task;
            if (FileStamp.Of(item.Path) == loaded.Stamp) return loaded;
            loaded.Proxy.Dispose();
        }
        catch (ImageLoadException)
        {
        }

        return null;
    }

    private void DiscardPreload()
    {
        if (_preload is not { } p) return;
        _preload = null;
        _ = p.Task.ContinueWith(t =>
        {
            if (t.IsCompletedSuccessfully) t.Result.Proxy.Dispose();
        }, TaskScheduler.Default);
    }

    /// <summary>Saves the current photo with its exact current crop and edits (FR-004). Returns true on success.</summary>
    public async Task<bool> SaveCurrentAsync()
    {
        if (Current is null || IsBusy || _proxy is null) return false;
        var item = Current;
        var recipe = _recipe;
        var stamp = _loadedStamp;
        IsSaving = true;
        ErrorMessage = null;
        StatusMessage = Localizer.Instance["Saving"];
        SaveResult result;
        try
        {
            result = await Task.Run(() => _services.Saver.Save(item.Path, recipe, stamp));
        }
        finally
        {
            IsSaving = false;
        }

        if (!result.Succeeded)
        {
            StatusMessage = null;
            ErrorMessage = ErrorTexts.ForSave(result);
            Log.Warning("Save failed for {Path}: {Error} {Detail}", item.Path, result.Error, result.Detail);
            return false;
        }

        Log.Information("Saved {Path} ({Outcome})", item.Path, result.Outcome);
        item.PendingRecipe = null;
        item.State = result.Outcome == SaveOutcome.Reviewed ? PhotoState.Reviewed : PhotoState.Edited;
        item.UpdateEntry();
        _loadedStamp = result.NewStamp ?? FileStamp.Of(item.Path) ?? stamp;
        _baseline = recipe;
        StatusMessage = Localizer.Instance.Format(result.Outcome == SaveOutcome.Saved ? "SavedMsg" : "ReviewedMsg", item.Name);
        Browse.RefreshThumbnail(item);
        Browse.RaiseCounts();
        RaiseAll();
        return true;
    }

    [RelayCommand]
    public async Task SaveAndNextAsync()
    {
        if (IsBusy) return;
        if (!await SaveCurrentAsync()) return;
        var seq = Sequence;
        var i = Current is null ? -1 : seq.IndexOf(Current);
        if (i >= 0 && i < seq.Count - 1)
        {
            var saved = StatusMessage;
            await OpenAsync(seq[i + 1]);
            StatusMessage ??= saved;
        }
        else
        {
            StatusMessage = Localizer.Instance["OrderEnd"];
        }
    }

    [RelayCommand]
    public async Task SaveAsync()
    {
        if (!IsBusy) await SaveCurrentAsync();
    }

    [RelayCommand]
    public async Task NextAsync() => await MoveAsync(+1);

    [RelayCommand]
    public async Task PreviousAsync() => await MoveAsync(-1);

    [RelayCommand]
    public async Task OpenItemAsync(PhotoItemViewModel? item)
    {
        if (item is { IsOpenable: true } && !IsBusy && item != Current) await OpenAsync(item);
    }

    [RelayCommand]
    public void BackToFolder()
    {
        KeepPending();
        Browse.RefreshStates();
        BackRequested?.Invoke();
    }

    private async Task MoveAsync(int delta)
    {
        if (IsBusy || Current is null) return;
        var seq = Sequence;
        var i = seq.IndexOf(Current) + delta;
        if (i < 0 || i >= seq.Count)
        {
            if (delta > 0) StatusMessage = Localizer.Instance["OrderEnd"];
            return; // no wrap-around (FR-017)
        }

        await OpenAsync(seq[i]);
    }

    /// <summary>Keeps manual edits of the photo being left in memory, never saving them (FR-019).</summary>
    public void KeepPending()
    {
        if (Current is null || _proxy is null) return;
        if (IsDirty)
        {
            Current.PendingRecipe = _recipe;
            Current.State = PhotoState.Unsaved;
        }
        else if (Current.PendingRecipe is not null)
        {
            Current.PendingRecipe = null;
            if (Current.State == PhotoState.Unsaved) Current.State = PhotoState.ToDo;
        }

        Browse.RaiseCounts();
    }

    // ------------------------------------------------------------------ editing

    [RelayCommand]
    public void SetFormat(string formatId)
    {
        if (_proxy is null || formatId == _recipe.FormatId) return;
        var next = _recipe with { FormatId = formatId };
        SetRecipe(next with { Crop = RefitCrop(next) }, null);
    }

    [RelayCommand]
    public void SetOrientation(CropOrientation orientation)
    {
        if (_proxy is null || orientation == _recipe.Orientation) return;
        var next = _recipe with { Orientation = orientation };
        SetRecipe(next with { Crop = RefitCrop(next) }, null);
    }

    [RelayCommand]
    public void SetComposition(Composition composition)
    {
        if (_proxy is null || composition == _recipe.Composition) return;
        SetRecipe(_recipe with { Composition = composition }, null);
        _ = RenderPreviewAsync();
    }

    /// <summary>Applies a custom ratio from width × height (FR-010).</summary>
    [RelayCommand]
    public void ApplyCustomRatio()
    {
        if (_proxy is null || CustomWidth <= 0 || CustomHeight <= 0) return;
        var w = (double)Math.Min(CustomWidth, CustomHeight);
        var h = (double)Math.Max(CustomWidth, CustomHeight);
        var next = _recipe with { FormatId = PrintFormats.CustomId, CustomRatio = w / h };
        SetRecipe(next with { Crop = RefitCrop(next) }, null);
    }

    [RelayCommand]
    public void RotateLeft() => Rotate(3);

    [RelayCommand]
    public void RotateRight() => Rotate(1);

    private void Rotate(int turns)
    {
        if (_proxy is null) return;
        var next = _recipe with { QuarterTurns = (_recipe.QuarterTurns + turns) % 4 };
        var (w, h) = RotatedFullSize(next with { Straighten = 0 });
        next = next with { Orientation = CropMath.OrientationFor(w, h) };
        var (bw, bh) = RotatedFullSize(next);
        var valid = ValidArea(next);
        SetRecipe(next with { Crop = NormalizedRect.FromPixels(CropMath.LargestCentered(valid, next.CropRatio), bw, bh) }, null);
    }

    /// <summary>Auto Correct (FR-045): conservative values, one undo step, fully adjustable afterwards.</summary>
    [RelayCommand]
    public async Task AutoCorrectAsync()
    {
        if (_proxy is null || IsBusy) return;
        var proxy = _proxy;
        var recipe = _recipe;
        var region = PreviewCropPixels();
        var result = await Task.Run(() =>
        {
            using var plain = RecipeRenderer.RenderPreview(proxy, recipe, applyTone: false);
            var data = PixelData.From(plain);
            return AutoCorrect.Apply(recipe, ImageStatistics.Histogram(data, region), ImageStatistics.NeutralMeans(data, region));
        });
        SetRecipe(result with { Contrast = _recipe.Contrast, Saturation = _recipe.Saturation }, null);
    }

    [RelayCommand]
    public void ResetLevels() => SetRecipe(_recipe with { LevelsBlack = 0, LevelsMid = 1, LevelsWhite = 1 }, null);

    [RelayCommand]
    public void ResetColor() => SetRecipe(_recipe with { Contrast = 0, Saturation = 0, Temperature = 0, Tint = 0 }, null);

    [RelayCommand]
    public void ResetCrop()
    {
        if (_proxy is null) return;
        var (w, h) = RotatedFullSize(_recipe);
        SetRecipe(_recipe with { Crop = NormalizedRect.FromPixels(CropMath.LargestCentered(ValidArea(_recipe), _recipe.CropRatio), w, h) }, null);
    }

    [RelayCommand]
    public void ResetPhoto()
    {
        if (_proxy is not null) SetRecipe(DefaultRecipe(), null);
    }

    [RelayCommand]
    public void ExposureMinus() => Exposure = Math.Clamp(Math.Round((Exposure - ExposureStep) * 6) / 6, -2, 2);

    [RelayCommand]
    public void ExposurePlus() => Exposure = Math.Clamp(Math.Round((Exposure + ExposureStep) * 6) / 6, -2, 2);

    [RelayCommand]
    public void ResetExposure() => Exposure = 0;

    [RelayCommand]
    public void Undo()
    {
        if (_undo.Count == 0) return;
        _redo.Add(_recipe);
        _recipe = _undo[^1];
        _undo.RemoveAt(_undo.Count - 1);
        _lastUndoTag = null;
        AfterRecipeChanged(true);
    }

    [RelayCommand]
    public void Redo()
    {
        if (_redo.Count == 0) return;
        _undo.Add(_recipe);
        _recipe = _redo[^1];
        _redo.RemoveAt(_redo.Count - 1);
        _lastUndoTag = null;
        AfterRecipeChanged(true);
    }

    /// <summary>Called by the canvas when a crop drag ends (one undo step).</summary>
    public void CommitCrop(Rect previewRect)
    {
        if (_proxy is null) return;
        var (pw, ph) = RotatedPreviewSize();
        var crop = new NormalizedRect(previewRect.X / pw, previewRect.Y / ph, previewRect.Width / pw, previewRect.Height / ph);
        SetRecipe(_recipe with { Crop = crop }, null);
        UpdateHistogram();
    }

    private void SetRecipe(EditRecipe next, string? tag)
    {
        if (next == _recipe) return;
        var coalesce = tag is not null && tag == _lastUndoTag && (DateTime.UtcNow - _lastUndoTime).TotalMilliseconds < 800;
        if (!coalesce)
        {
            _undo.Add(_recipe);
            if (_undo.Count > 100) _undo.RemoveAt(0);
        }

        _lastUndoTag = tag;
        _lastUndoTime = DateTime.UtcNow;
        _redo.Clear();
        var render = RenderKey(next) != RenderKey(_recipe);
        _recipe = next;
        AfterRecipeChanged(render);
    }

    /// <summary>The parts of a recipe that change preview pixels (crop and format are drawn as an overlay).</summary>
    private static EditRecipe RenderKey(EditRecipe r) => r with
    {
        Crop = NormalizedRect.Full, FormatId = PrintFormats.DefaultId, Orientation = CropOrientation.Landscape,
        Composition = Composition.Fill, CustomRatio = 0,
    };

    private void AfterRecipeChanged(bool render)
    {
        SyncUiFromRecipe();
        if (render) _ = RenderPreviewAsync();
        else UpdateHistogram();
        RaiseAll();
    }

    private void SyncUiFromRecipe()
    {
        _syncing = true;
        try
        {
            Exposure = _recipe.Exposure;
            Contrast = _recipe.Contrast;
            Saturation = _recipe.Saturation;
            Temperature = _recipe.Temperature;
            Tint = _recipe.Tint;
            LevelsBlack = _recipe.LevelsBlack;
            LevelsMid = _recipe.LevelsMid;
            LevelsWhite = _recipe.LevelsWhite;
            Straighten = _recipe.Straighten;
            CropRatio = _recipe.CropRatio;
            if (_recipe.FormatId == PrintFormats.CustomId)
            {
                CustomWidth = Math.Round((decimal)(_recipe.CustomRatio * 15), 1);
                CustomHeight = 15;
            }

            var (bw, bh) = RotatedFullSize(_recipe);
            var (vw, vh) = RotatedPreviewSize();
            var valid = ValidArea(_recipe);
            CropBounds = new Rect(valid.X / bw * vw, valid.Y / bh * vh, valid.Width / bw * vw, valid.Height / bh * vh);
            var (pw, ph) = RotatedPreviewSize();
            CropRect = new Rect(_recipe.Crop.X * pw, _recipe.Crop.Y * ph, _recipe.Crop.Width * pw, _recipe.Crop.Height * ph);
        }
        finally
        {
            _syncing = false;
        }
    }

    private EditRecipe DefaultRecipe()
    {
        var (w, h) = _sourceSize;
        var r = new EditRecipe { FormatId = PrintFormats.DefaultId, Orientation = CropMath.OrientationFor(w, h) };
        return r with { Crop = NormalizedRect.FromPixels(CropMath.LargestCentered(RectD.FromSize(w, h), r.CropRatio), w, h) };
    }

    private NormalizedRect RefitCrop(EditRecipe next)
    {
        var (w, h) = RotatedFullSize(next);
        var current = next.Crop.ToPixels(w, h);
        return NormalizedRect.FromPixels(CropMath.Refit(current, next.CropRatio, ValidArea(next)), w, h);
    }

    /// <summary>Area of the rotated photo that contains image content (no empty corners after straightening).</summary>
    private RectD ValidArea(EditRecipe r)
    {
        var (w, h) = r.QuarterTurns % 2 == 1 ? (_sourceSize.H, _sourceSize.W) : (_sourceSize.W, _sourceSize.H);
        return CropMath.ValidAreaAfterRotation(w, h, r.Straighten);
    }

    /// <summary>Keeps the crop's size and centre when the straightening angle changes, fitted inside the new valid area.</summary>
    private NormalizedRect KeepCropInside(EditRecipe previous, EditRecipe next)
    {
        var (pw, ph) = RotatedFullSize(previous);
        var (nw, nh) = RotatedFullSize(next);
        var crop = previous.Crop.ToPixels(pw, ph).Offset((nw - pw) / 2, (nh - ph) / 2);
        return NormalizedRect.FromPixels(CropMath.Refit(crop, next.CropRatio, ValidArea(next)), nw, nh);
    }

    private PixelRect PreviewCropPixels()
    {
        var (pw, ph) = RotatedPreviewSize();
        return CropMath.ToPixelRect(_recipe.Composition == Composition.Fill ? _recipe.Crop : NormalizedRect.Full, (int)Math.Round(pw), (int)Math.Round(ph));
    }

    private void UpdateHistogram()
    {
        if (_lastPixels is not { } data) return;
        var region = _recipe.Composition == Composition.Fill
            ? CropMath.ToPixelRect(_recipe.Crop, data.Width, data.Height)
            : (PixelRect?)null;
        Histogram = ImageStatistics.Histogram(data, region);
    }

    private (double W, double H) RotatedFullSize(EditRecipe r)
    {
        var (w, h) = r.QuarterTurns % 2 == 1 ? (_sourceSize.H, _sourceSize.W) : (_sourceSize.W, _sourceSize.H);
        return Math.Abs(r.Straighten) > 1e-6 ? CropMath.RotatedSize(w, h, r.Straighten) : (w, h);
    }

    private (double W, double H) RotatedPreviewSize()
    {
        if (_proxy is null) return (1, 1);
        var (w, h) = _recipe.QuarterTurns % 2 == 1 ? ((double)_proxy.Height, (double)_proxy.Width) : (_proxy.Width, _proxy.Height);
        return Math.Abs(_recipe.Straighten) > 1e-6 ? CropMath.RotatedSize(w, h, _recipe.Straighten) : (w, h);
    }

    private async Task RenderPreviewAsync()
    {
        var version = Interlocked.Increment(ref _renderVersion);
        await _renderGate.WaitAsync();
        try
        {
            if (version != _renderVersion || _proxy is null) return;
            var proxy = _proxy;
            var recipe = _recipe;
            var applyTone = !ShowBefore;
            var data = await Task.Run(() =>
            {
                using var img = RecipeRenderer.RenderPreview(proxy, recipe, applyTone);
                return PixelData.From(img);
            });
            var display = data;
            if (recipe.Composition == Composition.WholeWithBorder && !ShowBefore)
            {
                // Show the bordered result as it will be saved.
                display = await Task.Run(() =>
                {
                    using var img = RecipeRenderer.RenderFinal(proxy, recipe);
                    return PixelData.From(img);
                });
            }

            _lastPixels = data;
            var old = Preview;
            Preview = BitmapFactory.Create(display);
            (old as IDisposable)?.Dispose();
            UpdateHistogram();
            OnPropertyChanged(nameof(PreviewWidth));
            OnPropertyChanged(nameof(PreviewHeight));
        }
        catch (MagickException ex)
        {
            Log.Error(ex, "Preview rendering failed");
        }
        finally
        {
            _renderGate.Release();
        }
    }

    // ------------------------------------------------------------------ keyboard (FR-018)

    /// <summary>Editor shortcuts. The window filters out auto-repeat and keys aimed at text inputs before calling this.</summary>
    public bool HandleKey(Key key, KeyModifiers modifiers)
    {
        var ctrl = modifiers.HasFlag(KeyModifiers.Control);
        switch (key)
        {
            case Key.Enter when modifiers == KeyModifiers.None:
                _ = SaveAndNextAsync();
                return true;
            case Key.S when ctrl:
                _ = SaveAsync();
                return true;
            case Key.Right or Key.PageDown when modifiers == KeyModifiers.None:
                _ = NextAsync();
                return true;
            case Key.Left or Key.PageUp when modifiers == KeyModifiers.None:
                _ = PreviousAsync();
                return true;
            case Key.Z when ctrl && modifiers.HasFlag(KeyModifiers.Shift):
            case Key.Y when ctrl:
                Redo();
                return true;
            case Key.Z when ctrl:
                Undo();
                return true;
            case Key.Escape:
                BackToFolder();
                return true;
            case Key.Space:
                ShowBefore = true;
                return true;
            default:
                return false;
        }
    }

    public bool HandleKeyUp(Key key)
    {
        if (key != Key.Space) return false;
        ShowBefore = false;
        return true;
    }

    private void RaiseInfo()
    {
        OnPropertyChanged(nameof(LevelsText));
        OnPropertyChanged(nameof(StraightenText));
        OnPropertyChanged(nameof(CropInfo));
        OnPropertyChanged(nameof(ResolutionWarning));
        OnPropertyChanged(nameof(PositionText));
        OnPropertyChanged(nameof(ExposureText));
    }

    private void RaiseAll()
    {
        RaiseInfo();
        OnPropertyChanged(nameof(IsDirty));
        OnPropertyChanged(nameof(FormatId));
        OnPropertyChanged(nameof(IsWholeWithBorder));
        OnPropertyChanged(nameof(ShowCrop));
        OnPropertyChanged(nameof(IsPortrait));
        OnPropertyChanged(nameof(CanUndo));
        OnPropertyChanged(nameof(CanRedo));
        OnPropertyChanged(nameof(Title));
        OnPropertyChanged(nameof(PreviewWidth));
        OnPropertyChanged(nameof(PreviewHeight));
    }
}
