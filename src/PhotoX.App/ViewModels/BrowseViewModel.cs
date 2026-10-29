using System.Collections.ObjectModel;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PhotoX.App.Localization;
using PhotoX.App.Services;
using PhotoX.Core.Orders;
using PhotoX.Imaging;
using PhotoX.Storage;
using Serilog;

namespace PhotoX.App.ViewModels;

public enum PhotoFilter
{
    All,
    ToDo,
    Done,
}

/// <summary>Built-in explorer and gallery (FR-001, FR-002, FR-053, FR-054).</summary>
public sealed partial class BrowseViewModel : ViewModelBase
{
    private readonly AppServices _services;
    private CancellationTokenSource _thumbCts = new();

    public BrowseViewModel(AppServices services)
    {
        _services = services;
        DetailsCollapsed = services.Settings.BrowseDetailsCollapsed;
        RecentCollapsed = services.Settings.RecentCollapsed;
        LoadLocations();
        Localizer.Instance.LanguageChanged += (_, _) => RefreshTexts();
    }

    /// <summary>Raised when a photo should open in the editor.</summary>
    public event Action<PhotoItemViewModel>? EditRequested;

    /// <summary>Raised when the operator asks to finish the order (FR-072).</summary>
    public event Action? FinishRequested;

    [RelayCommand]
    private void FinishOrder() => FinishRequested?.Invoke();

    /// <summary>Raised when the selected photo should open in the ID Photo Creator.</summary>
    public event Action<PhotoItemViewModel>? UseForIdRequested;

    [RelayCommand]
    private void UseForId()
    {
        if (SelectedPhoto is { IsOpenable: true } p) UseForIdRequested?.Invoke(p);
    }

    public int EditedCount => AllPhotos.Count(p => p.State == PhotoState.Edited);

    public int ReviewedCount => AllPhotos.Count(p => p.State == PhotoState.Reviewed);

    public ObservableCollection<LocationItem> Locations { get; } = [];

    public ObservableCollection<FolderNodeViewModel> FolderRoots { get; } = [];

    public ObservableCollection<string> RecentFolders { get; } = [];

    public ObservableCollection<PhotoItemViewModel> AllPhotos { get; } = [];

    public ObservableCollection<PhotoItemViewModel> VisiblePhotos { get; } = [];

    [ObservableProperty]
    public partial string? CurrentFolder { get; set; }

    [ObservableProperty]
    public partial PhotoFilter Filter { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSelection))]
    public partial PhotoItemViewModel? SelectedPhoto { get; set; }

    [ObservableProperty]
    public partial FolderNodeViewModel? SelectedFolderNode { get; set; }

    [ObservableProperty]
    public partial bool DetailsCollapsed { get; set; }

    [ObservableProperty]
    public partial bool IsRemovable { get; set; }

    /// <summary>The recent-orders list is folded away (prompt_03 §2).</summary>
    [ObservableProperty]
    public partial bool RecentCollapsed { get; set; }

    /// <summary>True while the folder is scanned (opening is asynchronous).</summary>
    [ObservableProperty]
    public partial bool IsScanning { get; set; }

    public bool HasSelection => SelectedPhoto is not null;

    public bool HasFolder => CurrentFolder is not null;

    public bool IsEmpty => CurrentFolder is not null && AllPhotos.Count == 0;

    public int TotalCount => AllPhotos.Count(p => p.IsOpenable);

    public int DoneCount => AllPhotos.Count(p => p.IsDone);

    public double Progress => TotalCount == 0 ? 0 : (double)DoneCount / TotalCount;

    /// <summary>Every photo is saved or reviewed and nothing is pending: the main action becomes Finish Order (prompt_03 §5).</summary>
    public bool IsOrderDone => TotalCount > 0 && DoneCount == TotalCount && AllPhotos.All(p => p.PendingRecipe is null);

    public string ProgressText => Localizer.Instance.Format("DoneOf", DoneCount, TotalCount);

    public string PhotosLeftText => Localizer.Instance.Plural("PhotosLeft", TotalCount - DoneCount);

    public string FolderTitle => CurrentFolder is null ? string.Empty : Path.GetFileName(CurrentFolder.TrimEnd(Path.DirectorySeparatorChar));

    public string CountAll => AllPhotos.Count.ToString(Localizer.Instance.Culture);

    public string CountToDo => AllPhotos.Count(p => p.IsToDo).ToString(Localizer.Instance.Culture);

    public string CountDone => DoneCount.ToString(Localizer.Instance.Culture);

    partial void OnFilterChanged(PhotoFilter value) => ApplyFilter();

    partial void OnDetailsCollapsedChanged(bool value)
    {
        _services.Settings.BrowseDetailsCollapsed = value;
        _services.SaveSettings();
    }

    partial void OnRecentCollapsedChanged(bool value)
    {
        _services.Settings.RecentCollapsed = value;
        _services.SaveSettings();
    }

    /// <summary>Clicking a folder selects and opens it and shows its subfolders, like a desktop file explorer (prompt_03 §2).</summary>
    partial void OnSelectedFolderNodeChanged(FolderNodeViewModel? value)
    {
        if (value is null || string.IsNullOrEmpty(value.Path)) return;
        value.IsExpanded = true;
        if (!string.Equals(value.Path, CurrentFolder, StringComparison.Ordinal)) OpenFolder(value.Path);
    }

    [RelayCommand]
    private void ToggleRecent() => RecentCollapsed = !RecentCollapsed;

    /// <summary>The primary button: Start editing, or Finish Order once every photo is done.</summary>
    [RelayCommand]
    private void PrimaryAction()
    {
        if (IsOrderDone) FinishRequested?.Invoke();
        else StartEditing();
    }

    [RelayCommand]
    private void SetFilter(PhotoFilter filter) => Filter = filter;

    [RelayCommand]
    private void ToggleDetails() => DetailsCollapsed = !DetailsCollapsed;

    [RelayCommand]
    private void SelectLocation(LocationItem? location)
    {
        if (location is null) return;
        FolderRoots.Clear();
        var root = new FolderNodeViewModel(location.Path, location.Name);
        root.EnsureLoaded();
        root.IsExpanded = true;
        FolderRoots.Add(root);
    }

    [RelayCommand]
    private void OpenRecent(string? folder)
    {
        if (folder is null) return;
        RevealInTree(folder);
        OpenFolder(folder);
    }

    [RelayCommand]
    private void StartEditing()
    {
        var target = SelectedPhoto is { IsOpenable: true } && Filter != PhotoFilter.All ? SelectedPhoto
            : AllPhotos.FirstOrDefault(p => p.IsToDo && p.IsOpenable) ?? AllPhotos.FirstOrDefault(p => p.IsOpenable);
        if (target is not null) EditRequested?.Invoke(target);
    }

    [RelayCommand]
    private void EditPhoto(PhotoItemViewModel? item)
    {
        item ??= SelectedPhoto;
        if (item is { IsOpenable: true }) EditRequested?.Invoke(item);
    }

    public void Initialize()
    {
        var last = _services.Settings.LastFolder;
        if (last is not null && Directory.Exists(last))
        {
            RevealInTree(last);
            OpenFolder(last);
        }
        else if (Locations.Count > 0)
        {
            SelectLocation(Locations[0]);
        }
    }

    /// <summary>Opens an order folder. The scan runs in the background; <see cref="OpenFolderAsync"/> can be awaited.</summary>
    public void OpenFolder(string folder) => _ = OpenFolderAsync(folder);

    public async Task OpenFolderAsync(string folder)
    {
        if (!Directory.Exists(folder)) return;
        _thumbCts.Cancel();
        _thumbCts = new CancellationTokenSource();
        var ct = _thumbCts.Token;
        CurrentFolder = folder;
        IsRemovable = DriveHelper.IsRemovable(folder);
        _services.Settings.LastFolder = folder;
        _services.Settings.AddRecent(folder);
        _services.SaveSettings();
        RefreshRecent();

        IsScanning = true;
        SelectedPhoto = null;
        AllPhotos.Clear();
        VisiblePhotos.Clear();
        RaiseCounts();
        var (sidecar, entries) = await Task.Run(() =>
        {
            var store = new OrderStore(folder);
            var s = store.Load();
            if (store.UnhideRecordedPhotos(s) is > 0 and var unhidden)
            {
                Log.Warning("Made {Count} saved photos visible again in {Folder} (hidden by PhotoX 0.4.0)", unhidden, folder);
            }

            return (s, FolderScanner.Scan(folder));
        });
        if (ct.IsCancellationRequested) return;
        IsScanning = false;

        var items = new List<PhotoItemViewModel>(entries.Count);
        foreach (var entry in entries)
        {
            var item = new PhotoItemViewModel(entry, StateFromSidecar(sidecar, entry));
            item.ErrorText = entry.Kind switch
            {
                ImageKind.Heic => ErrorTexts.ForImage(ImageError.UnsupportedHeic),
                ImageKind.OtherImage or ImageKind.Unknown => ErrorTexts.ForImage(ImageError.UnsupportedFormat),
                _ => null,
            };
            items.Add(item);
        }

        foreach (var item in items) AllPhotos.Add(item);
        ApplyFilter();
        // Select after the gallery holds the items, so the list shows the selection.
        SelectedPhoto = VisiblePhotos.FirstOrDefault(p => p.IsToDo && p.IsOpenable) ?? VisiblePhotos.FirstOrDefault();
        RaiseCounts();
        _ = LoadThumbnailsAsync(items, ct);
        Log.Information("Opened folder {Folder} with {Count} photos", folder, AllPhotos.Count);
    }

    /// <summary>Reloads states from disk (after saving or returning from the editor).</summary>
    public void RefreshStates()
    {
        if (CurrentFolder is null) return;
        var sidecar = new OrderStore(CurrentFolder).Load();
        foreach (var item in AllPhotos)
        {
            item.UpdateEntry();
            if (!item.IsOpenable) continue;
            item.State = item.PendingRecipe is not null ? PhotoState.Unsaved : StateFromSidecar(sidecar, item.Entry);
        }

        ApplyFilter();
        RaiseCounts();
    }

    public void RefreshThumbnail(PhotoItemViewModel item) => _ = LoadThumbnailsAsync([item], _thumbCts.Token);

    public void RaiseCounts()
    {
        OnPropertyChanged(nameof(TotalCount));
        OnPropertyChanged(nameof(DoneCount));
        OnPropertyChanged(nameof(Progress));
        OnPropertyChanged(nameof(ProgressText));
        OnPropertyChanged(nameof(PhotosLeftText));
        OnPropertyChanged(nameof(HasFolder));
        OnPropertyChanged(nameof(IsEmpty));
        OnPropertyChanged(nameof(FolderTitle));
        OnPropertyChanged(nameof(CountAll));
        OnPropertyChanged(nameof(CountToDo));
        OnPropertyChanged(nameof(CountDone));
        OnPropertyChanged(nameof(IsOrderDone));
    }

    /// <summary>
    /// Updates the visible photos in place: only photos that enter or leave the filter are inserted or removed,
    /// so the gallery keeps its cards (rebuilding hundreds of cards made returning from the editor slow).
    /// </summary>
    public void ApplyFilter()
    {
        var keep = SelectedPhoto;
        var wanted = AllPhotos.Where(p => Filter == PhotoFilter.All || (Filter == PhotoFilter.ToDo && p.IsToDo) || (Filter == PhotoFilter.Done && p.IsDone)).ToList();
        var wantedSet = wanted.ToHashSet();
        for (var i = VisiblePhotos.Count - 1; i >= 0; i--)
        {
            if (!wantedSet.Contains(VisiblePhotos[i])) VisiblePhotos.RemoveAt(i);
        }

        for (var i = 0; i < wanted.Count; i++)
        {
            if (i < VisiblePhotos.Count && ReferenceEquals(VisiblePhotos[i], wanted[i])) continue;
            var existing = VisiblePhotos.IndexOf(wanted[i]);
            if (existing >= 0) VisiblePhotos.Move(existing, i);
            else VisiblePhotos.Insert(i, wanted[i]);
        }

        SelectedPhoto = keep is not null && wantedSet.Contains(keep) ? keep : VisiblePhotos.FirstOrDefault();
    }

    internal static PhotoState StateFromSidecar(OrderSidecar sidecar, PhotoEntry entry)
    {
        var rec = sidecar.Find(entry.Name);
        if (rec is null) return PhotoState.ToDo;
        // A photo changed outside PhotoX after saving is treated as not done.
        if (rec.OutputLength != entry.Length || rec.OutputWriteTimeUtc != entry.LastWriteUtc) return PhotoState.ToDo;
        return rec.State == SavedState.Reviewed ? PhotoState.Reviewed : PhotoState.Edited;
    }

    private void RefreshTexts()
    {
        foreach (var p in AllPhotos) p.RefreshTexts();
        RaiseCounts();
        LoadLocations();
    }

    private void LoadLocations()
    {
        Locations.Clear();
        foreach (var l in DriveHelper.Locations(Localizer.Instance["Home"], Localizer.Instance["RemovableDrive"]))
        {
            Locations.Add(l);
        }

        RefreshRecent();
    }

    private void RefreshRecent()
    {
        RecentFolders.Clear();
        foreach (var f in _services.Settings.RecentFolders.Where(Directory.Exists).Take(4))
        {
            RecentFolders.Add(f);
        }
    }

    private void RevealInTree(string folder)
    {
        var location = Locations.Where(l => folder.StartsWith(l.Path, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(l => l.Path.Length).FirstOrDefault();
        if (location is null) return;
        SelectLocation(location);
        var node = FolderRoots.FirstOrDefault();
        var rel = Path.GetRelativePath(location.Path, folder);
        if (rel == ".") return;
        foreach (var part in rel.Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries))
        {
            if (node is null) return;
            node.EnsureLoaded();
            node.IsExpanded = true;
            node = node.Children.FirstOrDefault(c => string.Equals(c.Name, part, StringComparison.OrdinalIgnoreCase));
        }
    }

    private async Task LoadThumbnailsAsync(IReadOnlyList<PhotoItemViewModel> items, CancellationToken ct)
    {
        using var gate = new SemaphoreSlim(Math.Clamp(Environment.ProcessorCount - 1, 1, 4));
        var tasks = items.Select(async item =>
        {
            await gate.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                if (ct.IsCancellationRequested || !item.IsOpenable) return;
                var entry = item.Entry;
                var (bitmap, info, error) = await Task.Run(() =>
                {
                    var (bytes, i, e) = MakeThumbnail(entry);
                    if (bytes is null) return ((Bitmap?)null, i, e);
                    using var ms = new MemoryStream(bytes);
                    return (new Bitmap(ms), i, e); // decoded here, not on the UI thread
                }, ct).ConfigureAwait(false);
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    if (ct.IsCancellationRequested) return;
                    item.Info = info;
                    if (bitmap is not null)
                    {
                        item.Thumbnail = bitmap;
                    }
                    else if (error is not null)
                    {
                        item.ErrorText = error;
                        item.State = PhotoState.Error;
                        RaiseCounts();
                    }
                });
            }
            catch (OperationCanceledException)
            {
            }
            finally
            {
                gate.Release();
            }
        });
        try
        {
            await Task.WhenAll(tasks).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
        }
    }

    private (byte[]? Bytes, string Info, string? Error) MakeThumbnail(PhotoEntry entry)
    {
        var path = entry.Path;
        try
        {
            var info = ImageProbe.Probe(path);
            var dims = info.OrientedWidth > 0 ? $"{info.OrientedWidth} × {info.OrientedHeight}" : string.Empty;
            var bytes = _services.ThumbnailCache.TryGet(entry);
            if (bytes is null)
            {
                bytes = Thumbnailer.Create(path);
                _services.ThumbnailCache.Put(entry, bytes);
            }

            return (bytes, dims, null);
        }
        catch (ImageLoadException ex)
        {
            Log.Warning("Thumbnail failed for {Path}: {Message}", path, ex.Message);
            return (null, string.Empty, ErrorTexts.ForImage(ex.Error));
        }
    }
}
