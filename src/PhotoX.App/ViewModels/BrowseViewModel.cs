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
        Explorer = new ExplorerViewModel(services);
        Explorer.FolderOpened += OpenFolder;
        Localizer.Instance.LanguageChanged += (_, _) => RefreshTexts();
        // Counters change on the print worker once Windows accepts (or loses) a job.
        services.PrintHistory.Changed += path => Dispatcher.UIThread.Post(() =>
        {
            foreach (var p in AllPhotos.Where(p => string.Equals(p.Path, path, StringComparison.OrdinalIgnoreCase))) p.PrintCount = services.PrintHistory.CountFor(p.Path);
        });
    }

    /// <summary>The built-in explorer (FR-001), shared in design with the ID Photos and Collages workspaces.</summary>
    public ExplorerViewModel Explorer { get; }

    /// <summary>Raised when a photo should open in the editor.</summary>
    public event Action<PhotoItemViewModel>? EditRequested;

    /// <summary>Raised with the photos to print (Print selected / Print all, prompt_05 §5).</summary>
    public event Action<IReadOnlyList<PhotoItemViewModel>>? PrintRequested;

    /// <summary>Raised with the photos whose changes should be saved and originals removed (prompt_05 §5).</summary>
    public event Action<IReadOnlyList<PhotoItemViewModel>>? SaveChangesRequested;

    /// <summary>Raised with the photos to restore from their originals (prompt_05 §5).</summary>
    public event Action<IReadOnlyList<PhotoItemViewModel>>? RestoreRequested;

    /// <summary>The photos selected in the gallery (Ctrl/Shift + click, Select all), in gallery order.</summary>
    public IReadOnlyList<PhotoItemViewModel> SelectedPhotos { get; private set; } = [];

    public int SelectionCount => SelectedPhotos.Count;

    public bool HasSingleSelection => SelectedPhotos.Count == 1;

    public bool HasMultipleSelection => SelectedPhotos.Count > 1;

    public string SelectionText => Localizer.Instance.Plural("PhotosSelected", SelectedPhotos.Count);

    /// <summary>Called by the gallery when its selection changes.</summary>
    public void SetSelection(IEnumerable<PhotoItemViewModel> photos)
    {
        var set = photos.ToHashSet();
        SelectedPhotos = VisiblePhotos.Where(set.Contains).ToList();
        OnPropertyChanged(nameof(SelectedPhotos));
        OnPropertyChanged(nameof(SelectionCount));
        OnPropertyChanged(nameof(HasSingleSelection));
        OnPropertyChanged(nameof(HasMultipleSelection));
        OnPropertyChanged(nameof(SelectionText));
        PrintSelectedCommand.NotifyCanExecuteChanged();
        SaveChangesCommand.NotifyCanExecuteChanged();
        RestoreOriginalCommand.NotifyCanExecuteChanged();
    }

    private IReadOnlyList<PhotoItemViewModel> Targets(PhotoItemViewModel? item) =>
        item is not null && !SelectedPhotos.Contains(item) ? [item] : SelectedPhotos.Count > 0 ? SelectedPhotos : SelectedPhoto is { } p ? [p] : [];

    private bool HasTargets(PhotoItemViewModel? item) => item is not null || SelectedPhotos.Count > 0 || SelectedPhoto is not null;

    [RelayCommand(CanExecute = nameof(HasTargets))]
    private void PrintSelected(PhotoItemViewModel? item) => PrintRequested?.Invoke(Targets(item));

    [RelayCommand]
    private void PrintAll()
    {
        if (AllPhotos.Any(p => p.IsOpenable)) PrintRequested?.Invoke(AllPhotos.Where(p => p.IsOpenable).ToList());
    }

    [RelayCommand(CanExecute = nameof(HasTargets))]
    private void SaveChanges(PhotoItemViewModel? item) => SaveChangesRequested?.Invoke(Targets(item));

    [RelayCommand(CanExecute = nameof(HasTargets))]
    private void RestoreOriginal(PhotoItemViewModel? item) => RestoreRequested?.Invoke(Targets(item));

    /// <summary>Raised when the selected photo should open in the ID Photo Creator.</summary>
    public event Action<PhotoItemViewModel>? UseForIdRequested;

    [RelayCommand]
    private void UseForId(PhotoItemViewModel? item)
    {
        if ((item ?? SelectedPhoto) is { IsOpenable: true } p) UseForIdRequested?.Invoke(p);
    }

    public int EditedCount => AllPhotos.Count(p => p.State == PhotoState.Edited);

    public int ReviewedCount => AllPhotos.Count(p => p.State == PhotoState.Reviewed);

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
    public partial bool DetailsCollapsed { get; set; }

    [ObservableProperty]
    public partial bool IsRemovable { get; set; }

    /// <summary>True while the folder is scanned (opening is asynchronous).</summary>
    [ObservableProperty]
    public partial bool IsScanning { get; set; }

    public bool HasSelection => SelectedPhoto is not null;

    public bool HasFolder => CurrentFolder is not null;

    public bool IsEmpty => CurrentFolder is not null && AllPhotos.Count == 0;

    public int TotalCount => AllPhotos.Count(p => p.IsOpenable);

    public int DoneCount => AllPhotos.Count(p => p.IsDone);

    public double Progress => TotalCount == 0 ? 0 : (double)DoneCount / TotalCount;

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


    [RelayCommand]
    private void SetFilter(PhotoFilter filter) => Filter = filter;

    [RelayCommand]
    private void ToggleDetails() => DetailsCollapsed = !DetailsCollapsed;

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
        Explorer.ShowStart(last);
        if (last is not null && Directory.Exists(last)) OpenFolder(last);
    }

    /// <summary>Duplicate image (prompt_04 §4): copies the photo in its folder under a free name and shows the copy.</summary>
    [RelayCommand]
    private async Task DuplicateAsync(PhotoItemViewModel? item)
    {
        item ??= SelectedPhoto;
        if (item is null || CurrentFolder is null) return;
        string copy;
        try
        {
            copy = await Task.Run(() => PhotoDuplicator.Duplicate(item.Path));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Log.Warning(ex, "Duplicate failed for {Path}", item.Path);
            DuplicateFailed?.Invoke(ex.Message);
            return;
        }

        Log.Information("Duplicated {Path} as {Copy}", item.Path, copy);
        var entry = FolderScanner.Scan(CurrentFolder).FirstOrDefault(e => string.Equals(e.Path, copy, StringComparison.Ordinal));
        if (entry is null) return;
        var added = new PhotoItemViewModel(entry, PhotoState.ToDo) { Thumbnail = item.Thumbnail, Info = item.Info };
        var all = AllPhotos.Append(added).OrderBy(p => p.Name, Core.Naming.NaturalComparer.Instance).ToList();
        AllPhotos.Insert(all.IndexOf(added), added);
        ApplyFilter();
        if (VisiblePhotos.Contains(added)) SelectedPhoto = added;
        RaiseCounts();
        RefreshThumbnail(added);
    }

    /// <summary>Raised with the reason when a duplicate cannot be written.</summary>
    public event Action<string>? DuplicateFailed;

    /// <summary>Opens an order folder. The scan runs in the background; <see cref="OpenFolderAsync"/> can be awaited.</summary>
    public void OpenFolder(string folder) => _ = OpenFolderAsync(folder);

    public async Task OpenFolderAsync(string folder)
    {
        if (!Directory.Exists(folder)) return;
        _thumbCts.Cancel();
        _thumbCts = new CancellationTokenSource();
        var ct = _thumbCts.Token;
        CurrentFolder = folder;
        Explorer.CurrentFolder = folder;
        IsRemovable = DriveHelper.IsRemovable(folder);
        _services.Settings.LastFolder = folder;
        _services.Settings.AddRecent(folder);
        _services.SaveSettings();
        Explorer.RefreshRecent();

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
            var item = new PhotoItemViewModel(entry, StateFromSidecar(sidecar, entry)) { PrintCount = _services.PrintHistory.CountFor(entry.Path) };
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
    }

    private Task LoadThumbnailsAsync(IReadOnlyList<PhotoItemViewModel> items, CancellationToken ct) =>
        ThumbnailSource.LoadAllAsync(_services.ThumbnailCache, items, item => item.IsOpenable ? item.Entry : null, (item, bitmap, info, error) =>
        {
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
        }, ct);
}
