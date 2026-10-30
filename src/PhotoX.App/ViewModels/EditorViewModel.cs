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

    private readonly AppServices _services;
    private readonly SemaphoreSlim _renderGate = new(1, 1);
    private readonly List<EditorState> _undo = [];
    private readonly List<EditorState> _redo = [];
    private readonly byte[][] _buffers = new byte[2][];
    private MagickImage? _proxy;
    private PreviewBase? _base;
    private int _bufferIndex;
    private int _renderVersion;
    private int _histogramVersion;
    private FileStamp _loadedStamp;
    private (int W, int H) _sourceSize;
    private EditRecipe _recipe = new();
    private EditRecipe _baseline = new();
    private string? _lastUndoTag;
    private DateTime _lastUndoTime;
    private bool _syncing;
    private (string Path, Task<Loaded> Task)? _preload;
    private (string Path, Loaded Data)? _shown;
    private (string Path, Loaded Data)? _previous;

    private sealed record Loaded(FileStamp Stamp, EditRecipe? Saved, ImageInfo Info, MagickImage Proxy, PixelData Pixels);

    /// <summary>One undo step: the recipe and whether the crop was applied to the preview.</summary>
    private sealed record EditorState(EditRecipe Recipe, bool CropApplied);

    /// <summary>The preview proxy after quarter turns, as 8-bit BGRA (straightening is drawn by the canvas).</summary>
    private sealed record PreviewBase(int Turns, PixelData Pixels);

    public EditorViewModel(AppServices services, BrowseViewModel browse)
    {
        _services = services;
        Browse = browse;
        Tone = new ToneAdjustmentsViewModel(AutoCorrectCommand);
        Tone.Changed += tag =>
        {
            if (_proxy is not null) SetRecipe(Tone.ApplyTo(_recipe), tag);
        };
        Localizer.Instance.LanguageChanged += (_, _) => RaiseInfo();
    }

    /// <summary>Histogram, levels and tone sliders (shared with the ID Photo Creator).</summary>
    public ToneAdjustmentsViewModel Tone { get; }

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
    public partial double Straighten { get; set; }

    /// <summary>The crop is shown applied: the cropped result fills the viewer, without the crop frame (prompt_03 §4).</summary>
    [ObservableProperty]
    public partial bool CropApplied { get; set; }

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

    public bool ShowCrop => _recipe.Composition == Composition.Fill && !CropApplied && !ShowBefore;

    /// <summary>The canvas shows the output: the crop (or whole photo) on its white canvas, fitted to the viewer.</summary>
    public bool ShowResult => _recipe.Composition == Composition.WholeWithBorder || CropApplied;

    public string StraightenText => string.Format(Localizer.Instance.Culture, "{0:+0.0;−0.0;0.0}°", Straighten);

    /// <summary>Every photo of the order is saved or reviewed and nothing is pending: Save &amp; Next becomes Close (prompt_03 §5).</summary>
    public bool IsCloseMode =>
        Current is not null && !IsDirty && Sequence is { Count: > 0 } seq && seq.All(p => p.IsDone && p.PendingRecipe is null);

    /// <summary>Area of the output canvas in preview coordinates (crop plus any white border), for the result view.</summary>
    public Rect ResultFrame
    {
        get
        {
            if (_proxy is null || _sourceSize.W == 0) return default;
            var (pw, ph) = RotatedPreviewSize();
            var (fw, fh) = RotatedFullSize(_recipe);
            var crop = _recipe.Composition == Composition.Fill ? _recipe.Crop : NormalizedRect.Full;
            var pixels = CropMath.ToPixelRect(crop, (int)Math.Round(fw), (int)Math.Round(fh));
            var (canvasW, canvasH, _) = RenderPlan.CanvasFor(pixels.Width, pixels.Height, _recipe);
            var c = new Rect(crop.X * pw, crop.Y * ph, crop.Width * pw, crop.Height * ph);
            var w = c.Width * canvasW / Math.Max(1, pixels.Width);
            var h = c.Height * canvasH / Math.Max(1, pixels.Height);
            return new Rect(c.Center.X - w / 2, c.Center.Y - h / 2, w, h);
        }
    }

    /// <summary>The crop shown in the result view: the selected crop, or the whole photo with borders.</summary>
    public Rect ResultCrop
    {
        get
        {
            var (pw, ph) = RotatedPreviewSize();
            var crop = _recipe.Composition == Composition.Fill ? _recipe.Crop : NormalizedRect.Full;
            return new Rect(crop.X * pw, crop.Y * ph, crop.Width * pw, crop.Height * ph);
        }
    }

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

    public double PreviewWidth => _proxy is null ? 1 : RotatedPreviewSize().W;

    public double PreviewHeight => _proxy is null ? 1 : RotatedPreviewSize().H;

    /// <summary>Bounding box of the straightened preview: the coordinate space of the crop frame.</summary>
    public Size PreviewSize => new(PreviewWidth, PreviewHeight);

    public string CropInfo
    {
        get
        {
            if (_sourceSize.W == 0) return string.Empty;
            var plan = RenderPlan.Create(_sourceSize.W, _sourceSize.H, _recipe);
            var format = _recipe.Format;
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

    partial void OnShowBeforeChanged(bool value)
    {
        OnPropertyChanged(nameof(ShowCrop));
        _ = RenderPreviewAsync();
    }

    partial void OnCropAppliedChanged(bool value)
    {
        OnPropertyChanged(nameof(ShowCrop));
        OnPropertyChanged(nameof(ShowResult));
    }

    partial void OnStraightenChanged(double value)
    {
        OnPropertyChanged(nameof(StraightenText));
        if (_syncing || _proxy is null) return;
        var next = _recipe with { Straighten = Math.Round(value, 1) };
        SetRecipe(next with { Crop = KeepCropInside(_recipe, next) }, "straighten");
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
            var loaded = TakePrevious(item) ?? await TakePreloadedAsync(item) ?? await Task.Run(() => Load(item.Path));
            if (Current != item)
            {
                loaded.Proxy.Dispose();
                return;
            }

            await _renderGate.WaitAsync();
            try
            {
                // Keep the photo being left, so Previous can show it again without loading (FR-052).
                if (_previous is { } older) older.Data.Proxy.Dispose();
                _previous = _shown;
                _shown = (item.Path, loaded);
                _proxy = loaded.Proxy;
                _base = new PreviewBase(0, loaded.Pixels);
            }
            finally
            {
                _renderGate.Release();
            }

            _loadedStamp = loaded.Stamp;
            _sourceSize = (loaded.Info.OrientedWidth, loaded.Info.OrientedHeight);
            _baseline = loaded.Saved ?? DefaultRecipe();
            _recipe = item.PendingRecipe ?? _baseline;
            CropApplied = false; // every photo opens with the crop tool active
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
        var proxy = ImageLoader.Load(source, ProxyEdge);
        return new Loaded(stamp, saved, ImageProbe.Probe(source), proxy, PixelData.From(proxy));
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

    /// <summary>The photo shown before the current one, if it is unchanged on disk.</summary>
    private Loaded? TakePrevious(PhotoItemViewModel item)
    {
        if (_previous is not { } prev || prev.Path != item.Path) return null;
        _previous = null;
        if (FileStamp.Of(item.Path) == prev.Data.Stamp) return prev.Data;
        prev.Data.Proxy.Dispose();
        return null;
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
        if (Current == item) CropApplied = true; // show what was saved (prompt_03 §4)
        StatusMessage = Localizer.Instance.Format(result.Outcome == SaveOutcome.Saved ? "SavedMsg" : "ReviewedMsg", item.Name);
        Browse.RefreshThumbnail(item);
        Browse.RaiseCounts();
        RaiseAll();
        return true;
    }

    /// <summary>Enter: saves and opens the next photo; once the whole order is done it closes the editor instead (prompt_03 §5).</summary>
    [RelayCommand]
    public async Task SaveAndNextAsync()
    {
        if (IsBusy) return;
        if (IsCloseMode)
        {
            BackToFolder();
            return;
        }

        if (!await SaveCurrentAsync()) return;
        if (IsCloseMode)
        {
            StatusMessage = Localizer.Instance["OrderDone"];
            return;
        }

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

    /// <summary>Switching orientation gives the largest valid crop of the new orientation, never a leftover smaller one (prompt_04 §7).</summary>
    [RelayCommand]
    public void SetOrientation(CropOrientation orientation)
    {
        if (_proxy is null || orientation == _recipe.Orientation) return;
        var next = _recipe with { Orientation = orientation };
        var (w, h) = RotatedFullSize(next);
        SetRecipe(next with { Crop = NormalizedRect.FromPixels(CropMath.LargestCentered(ValidArea(next), next.CropRatio), w, h) }, null);
    }

    [RelayCommand]
    public void SetComposition(Composition composition)
    {
        if (_proxy is null || composition == _recipe.Composition) return;
        SetRecipe(_recipe with { Composition = composition }, null);
    }

    /// <summary>
    /// Applies a custom print size of width × height cm (FR-010, prompt_04 §7): the crop gets its ratio, and saving
    /// centres the photo at exactly that size on the smallest print sheet it fits, with white around it.
    /// </summary>
    [RelayCommand]
    public void ApplyCustomSize()
    {
        if (_proxy is null || CustomWidth <= 0 || CustomHeight <= 0) return;
        var w = (double)Math.Min(CustomWidth, CustomHeight) * 10;
        var h = (double)Math.Max(CustomWidth, CustomHeight) * 10;
        var next = _recipe with { FormatId = PrintFormats.CustomId, CustomRatio = w / h, CustomWidthMm = w, CustomHeightMm = h };
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
        var recipe = _recipe;
        var data = (await EnsureBaseAsync(recipe.QuarterTurns)).Pixels;
        var region = BaseRegion(recipe, data.Width, data.Height);
        var result = await Task.Run(() =>
            AutoCorrect.Apply(recipe, ImageStatistics.Histogram(data, region), ImageStatistics.NeutralMeans(data, region)));
        SetRecipe(result with { Contrast = _recipe.Contrast, Saturation = _recipe.Saturation }, null);
    }

    /// <summary>Double-click inside the crop frame: show the cropped result without saving (prompt_03 §4).</summary>
    [RelayCommand]
    public void ApplyCrop()
    {
        if (_proxy is not null && _recipe.Composition == Composition.Fill && !CropApplied) SetState(_recipe, true, null);
    }

    /// <summary>Back to the crop frame on the whole photo.</summary>
    [RelayCommand]
    public void EditCrop()
    {
        if (_proxy is not null && CropApplied) SetState(_recipe, false, null);
    }

    [RelayCommand]
    public void ToggleCrop()
    {
        if (CropApplied) EditCrop();
        else ApplyCrop();
    }

    [RelayCommand]
    public void ResetCrop()
    {
        if (_proxy is null) return;
        var (w, h) = RotatedFullSize(_recipe);
        SetRecipe(_recipe with { Crop = NormalizedRect.FromPixels(CropMath.LargestCentered(ValidArea(_recipe), _recipe.CropRatio), w, h) }, null);
    }

    /// <summary>Reset (prompt_04 §7): clears all tone adjustments as one undo step; the crop and rotation stay.</summary>
    [RelayCommand]
    public void ResetAdjustments()
    {
        if (_proxy is not null) SetRecipe(_recipe.WithoutToneAdjustments(), null);
    }

    [RelayCommand]
    public void Undo()
    {
        if (_undo.Count == 0) return;
        _redo.Add(new EditorState(_recipe, CropApplied));
        Restore(_undo[^1]);
        _undo.RemoveAt(_undo.Count - 1);
    }

    [RelayCommand]
    public void Redo()
    {
        if (_redo.Count == 0) return;
        _undo.Add(new EditorState(_recipe, CropApplied));
        Restore(_redo[^1]);
        _redo.RemoveAt(_redo.Count - 1);
    }

    private void Restore(EditorState state)
    {
        var render = RenderKey(state.Recipe) != RenderKey(_recipe);
        _recipe = state.Recipe;
        CropApplied = state.CropApplied;
        _lastUndoTag = null;
        AfterRecipeChanged(render);
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

    /// <summary>A recipe change. Changes of geometry return to the crop frame; tone changes keep an applied crop.</summary>
    private void SetRecipe(EditRecipe next, string? tag) => SetState(next, CropApplied && GeometryKey(next) == GeometryKey(_recipe), tag);

    private void SetState(EditRecipe next, bool cropApplied, string? tag)
    {
        if (next == _recipe && cropApplied == CropApplied) return;
        var coalesce = tag is not null && tag == _lastUndoTag && (DateTime.UtcNow - _lastUndoTime).TotalMilliseconds < 800;
        if (!coalesce)
        {
            _undo.Add(new EditorState(_recipe, CropApplied));
            if (_undo.Count > 100) _undo.RemoveAt(0);
        }

        _lastUndoTag = tag;
        _lastUndoTime = DateTime.UtcNow;
        _redo.Clear();
        var render = RenderKey(next) != RenderKey(_recipe);
        _recipe = next;
        CropApplied = cropApplied;
        AfterRecipeChanged(render);
    }

    /// <summary>The parts of a recipe that change preview pixels: quarter turns and tone. Crop, format and straightening are drawn by the canvas.</summary>
    private static EditRecipe RenderKey(EditRecipe r) => r with
    {
        Crop = NormalizedRect.Full, FormatId = PrintFormats.DefaultId, Orientation = CropOrientation.Landscape,
        Composition = Composition.Fill, CustomRatio = 0, CustomWidthMm = 0, CustomHeightMm = 0, Straighten = 0,
    };

    private static EditRecipe GeometryKey(EditRecipe r) => r.WithoutToneAdjustments();

    private void AfterRecipeChanged(bool render)
    {
        SyncUiFromRecipe();
        if (render) _ = RenderPreviewAsync();
        else UpdateHistogram();
        RaiseAll();
    }

    private void SyncUiFromRecipe()
    {
        Tone.Load(_recipe);
        _syncing = true;
        try
        {
            Straighten = _recipe.Straighten;
            CropRatio = _recipe.CropRatio;
            if (_recipe.FormatId == PrintFormats.CustomId)
            {
                (CustomWidth, CustomHeight) = _recipe.HasCustomSize
                    ? (Math.Round((decimal)_recipe.CustomWidthMm / 10, 1), Math.Round((decimal)_recipe.CustomHeightMm / 10, 1))
                    : (Math.Round((decimal)(_recipe.CustomRatio * 15), 1), 15m);
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

    /// <summary>
    /// The crop in base pixels (before straightening), for the histogram and Auto Correct. It is the axis-aligned
    /// rectangle around the crop centre, which is close enough within the ±10° straightening range.
    /// </summary>
    private static PixelRect? BaseRegion(EditRecipe r, int width, int height)
    {
        if (r.Composition != Composition.Fill) return null;
        var (bw, bh) = Math.Abs(r.Straighten) > 1e-6 ? CropMath.RotatedSize(width, height, r.Straighten) : (width, height);
        var c = r.Crop;
        var dx = (c.X + c.Width / 2) * bw - bw / 2;
        var dy = (c.Y + c.Height / 2) * bh - bh / 2;
        var a = -r.Straighten * Math.PI / 180;
        var cx = dx * Math.Cos(a) - dy * Math.Sin(a) + width / 2.0;
        var cy = dx * Math.Sin(a) + dy * Math.Cos(a) + height / 2.0;
        var w = c.Width * bw;
        var h = c.Height * bh;
        return new PixelRect((int)Math.Round(cx - w / 2), (int)Math.Round(cy - h / 2), (int)Math.Round(w), (int)Math.Round(h));
    }

    /// <summary>Recomputes the histogram of the crop (tones entering levels, prompt_04 §7) off the UI thread.</summary>
    private void UpdateHistogram()
    {
        if (_base is not { } b || b.Turns != _recipe.QuarterTurns) return;
        var data = b.Pixels;
        var recipe = ShowBefore ? new EditRecipe() : _recipe;
        var version = Interlocked.Increment(ref _histogramVersion);
        var region = BaseRegion(_recipe, data.Width, data.Height);
        _ = Task.Run(() => ImageStatistics.LevelsInput(data, region, recipe)).ContinueWith(
            t =>
            {
                if (version == _histogramVersion && t.IsCompletedSuccessfully) Tone.Histogram = t.Result;
            },
            TaskScheduler.FromCurrentSynchronizationContext());
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

    /// <summary>The proxy turned by quarter turns as BGRA pixels; recomputed only when the turns change.</summary>
    private async Task<PreviewBase> EnsureBaseAsync(int turns)
    {
        if (_base is { } b && b.Turns == turns) return b;
        var proxy = _proxy!;
        var pixels = await Task.Run(() =>
        {
            if (turns == 0) return PixelData.From(proxy);
            using var turned = (MagickImage)proxy.Clone();
            turned.Rotate(90 * turns);
            return PixelData.From(turned);
        });
        _base = new PreviewBase(turns, pixels);
        return _base;
    }

    /// <summary>
    /// Renders the preview (DEC-044): the cached base pixels through the tone curves in one parallel pass, then the
    /// histogram of the crop. Straightening, crop and borders are drawn by the canvas, so moving them costs no rendering.
    /// </summary>
    private async Task RenderPreviewAsync()
    {
        var version = Interlocked.Increment(ref _renderVersion);
        await _renderGate.WaitAsync();
        try
        {
            if (version != _renderVersion || _proxy is null) return;
            var recipe = _recipe;
            var before = ShowBefore;
            var source = (await EnsureBaseAsync(recipe.QuarterTurns)).Pixels;
            _bufferIndex ^= 1;
            if (_buffers[_bufferIndex]?.Length != source.Bgra.Length) _buffers[_bufferIndex] = new byte[source.Bgra.Length];
            var target = _buffers[_bufferIndex];
            var region = BaseRegion(recipe, source.Width, source.Height);
            var (data, histogram) = await Task.Run(() =>
            {
                var tone = before ? new EditRecipe() : recipe;
                var levelsInput = ImageStatistics.LevelsInput(source, region, tone);
                if (!tone.HasToneAdjustments) return (source, levelsInput);
                ToneProcessor.ApplyToPreview(source.Bgra, target, ToneCurve.For(tone, exact: false), tone.Saturation);
                return (source with { Bgra = target }, levelsInput);
            });

            var old = Preview;
            Preview = BitmapFactory.Create(data);
            (old as IDisposable)?.Dispose();
            Interlocked.Increment(ref _histogramVersion);
            Tone.Histogram = histogram;
            OnPropertyChanged(nameof(PreviewWidth));
            OnPropertyChanged(nameof(PreviewHeight));
            OnPropertyChanged(nameof(PreviewSize));
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
            case Key.OemPlus or Key.Add when !ctrl:
                Tone.ExposurePlus();
                return true;
            case Key.OemMinus or Key.Subtract when !ctrl:
                Tone.ExposureMinus();
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
        OnPropertyChanged(nameof(StraightenText));
        OnPropertyChanged(nameof(CropInfo));
        OnPropertyChanged(nameof(ResolutionWarning));
        OnPropertyChanged(nameof(PositionText));
    }

    private void RaiseAll()
    {
        RaiseInfo();
        OnPropertyChanged(nameof(IsDirty));
        OnPropertyChanged(nameof(FormatId));
        OnPropertyChanged(nameof(IsWholeWithBorder));
        OnPropertyChanged(nameof(ShowCrop));
        OnPropertyChanged(nameof(ShowResult));
        OnPropertyChanged(nameof(ResultFrame));
        OnPropertyChanged(nameof(ResultCrop));
        OnPropertyChanged(nameof(IsPortrait));
        OnPropertyChanged(nameof(CanUndo));
        OnPropertyChanged(nameof(CanRedo));
        OnPropertyChanged(nameof(Title));
        OnPropertyChanged(nameof(PreviewWidth));
        OnPropertyChanged(nameof(PreviewHeight));
        OnPropertyChanged(nameof(PreviewSize));
        OnPropertyChanged(nameof(IsCloseMode));
    }
}
