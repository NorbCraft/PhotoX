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

    /// <summary>Photos deleted from the gallery and kept in <c>Original/</c> until Discard Originals (prompt_08 §5).</summary>
    Deleted,
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
        ThumbScale = Math.Clamp(services.Settings.ThumbnailScale, MinThumbScale, MaxThumbScale);
        Explorer = new ExplorerViewModel(services, ExplorerViewModel.PhotosWorkspace);
        Explorer.FolderOpened += OpenFolder;
        Localizer.Instance.LanguageChanged += (_, _) => RefreshTexts();
        // Counters change on the print worker once Windows accepts (or loses) a job.
        var ui = Dispatcher.UIThread;
        services.PrintHistory.Changed += path => ui.Post(() =>
        {
            foreach (var p in AllPhotos.Where(p => string.Equals(p.Path, path, StringComparison.OrdinalIgnoreCase))) p.PrintCount = services.PrintHistory.CountFor(p.Path);
            OnPropertyChanged(nameof(PrintedText));
        });
    }

    /// <summary>The built-in explorer (FR-001), shared in design with the ID Photos and Collages workspaces.</summary>
    public ExplorerViewModel Explorer { get; }

    /// <summary>Raised when a photo should open in the editor.</summary>
    public event Action<PhotoItemViewModel>? EditRequested;

    /// <summary>Raised with the photos to print (Print selected / Print all, prompt_05 §5).</summary>
    public event Action<IReadOnlyList<PhotoItemViewModel>>? PrintRequested;

    /// <summary>Raised for Discard Originals: the whole open folder (prompt_08 §5).</summary>
    public event Action? DiscardOriginalsRequested;

    /// <summary>Raised with the photos to delete (moved into <c>Original/</c>, prompt_08 §5).</summary>
    public event Action<IReadOnlyList<PhotoItemViewModel>>? DeleteRequested;

    /// <summary>Raised with deleted photos to bring back into the folder (prompt_08 §5).</summary>
    public event Action<IReadOnlyList<PhotoItemViewModel>>? RestoreDeletedRequested;

    /// <summary>
    /// Set by the shell: asked before another folder opens while photos of this one have unsaved edits, which exist
    /// only in memory (FR-019). True when the folder may change (the edits were saved or discarded).
    /// </summary>
    public Func<Task<bool>>? ConfirmLeaveFolder { get; set; }

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
        RestoreOriginalCommand.NotifyCanExecuteChanged();
        DeleteSelectedCommand.NotifyCanExecuteChanged();
        RestoreDeletedCommand.NotifyCanExecuteChanged();
    }

    /// <summary>The photos an action applies to: the clicked one, else the selection; never a mix of deleted and present ones.</summary>
    private IReadOnlyList<PhotoItemViewModel> Targets(PhotoItemViewModel? item, bool deleted = false) =>
        (item is not null && !SelectedPhotos.Contains(item) ? [item] : SelectedPhotos.Count > 0 ? SelectedPhotos : SelectedPhoto is { } p ? [p] : [])
        .Where(p => p.IsDeleted == deleted).ToList();

    private bool HasTargets(PhotoItemViewModel? item) => Targets(item).Count > 0;

    private bool HasDeletedTargets(PhotoItemViewModel? item) => Targets(item, deleted: true).Count > 0;

    [RelayCommand(CanExecute = nameof(HasTargets))]
    private void PrintSelected(PhotoItemViewModel? item) => PrintRequested?.Invoke(Targets(item));

    [RelayCommand]
    private void PrintAll()
    {
        if (AllPhotos.Any(p => p.IsOpenable)) PrintRequested?.Invoke(AllPhotos.Where(p => p.IsOpenable).ToList());
    }

    /// <summary>Discard Originals: the whole open folder, whatever is selected (prompt_08 §5).</summary>
    [RelayCommand]
    private void DiscardOriginals()
    {
        if (CurrentFolder is not null) DiscardOriginalsRequested?.Invoke();
    }

    [RelayCommand(CanExecute = nameof(HasTargets))]
    private void RestoreOriginal(PhotoItemViewModel? item) => RestoreRequested?.Invoke(Targets(item));

    /// <summary>Delete (context menu, Delete key): the clicked photo or the selection, after a confirmation.</summary>
    [RelayCommand(CanExecute = nameof(HasTargets))]
    private void DeleteSelected(PhotoItemViewModel? item)
    {
        if (Targets(item) is { Count: > 0 } targets) DeleteRequested?.Invoke(targets);
    }

    /// <summary>Restore (Deleted view): the clicked deleted photo or the selected ones.</summary>
    [RelayCommand(CanExecute = nameof(HasDeletedTargets))]
    private void RestoreDeleted(PhotoItemViewModel? item)
    {
        if (Targets(item, deleted: true) is { Count: > 0 } targets) RestoreDeletedRequested?.Invoke(targets);
    }

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

    /// <summary>Subfolders of the open folder, shown before the photos (prompt_08 §1).</summary>
    public ObservableCollection<FolderItemViewModel> Folders { get; } = [];

    /// <summary>Photos deleted from this folder and still kept in <c>Original/</c> (prompt_08 §5).</summary>
    public ObservableCollection<PhotoItemViewModel> DeletedPhotos { get; } = [];

    /// <summary>The gallery's cards: the subfolders, then the visible photos (only the deleted ones under Deleted).</summary>
    public ObservableCollection<object> GalleryItems { get; } = [];

    /// <summary>The gallery's selected card, a photo or a folder; <see cref="SelectedPhoto"/> follows it.</summary>
    [ObservableProperty]
    public partial object? SelectedGalleryItem { get; set; }

    private bool _syncingSelection;

    partial void OnSelectedGalleryItemChanged(object? value)
    {
        if (_syncingSelection) return;
        _syncingSelection = true;
        SelectedPhoto = value as PhotoItemViewModel;
        _syncingSelection = false;
    }

    partial void OnSelectedPhotoChanged(PhotoItemViewModel? value)
    {
        if (_syncingSelection) return;
        _syncingSelection = true;
        SelectedGalleryItem = value;
        _syncingSelection = false;
    }

    public bool IsDeletedView => Filter == PhotoFilter.Deleted;

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

    /// <summary>The gallery shows no card at all (no photos and no subfolders, or nothing deleted under Deleted).</summary>
    public bool ShowNoPhotos => CurrentFolder is not null && !IsScanning && GalleryItems.Count == 0;

    public string EmptyText => Localizer.Instance[IsDeletedView ? "NoDeletedPhotos" : "NoPhotos"];

    public int TotalCount => AllPhotos.Count(p => p.IsOpenable);

    public int DoneCount => AllPhotos.Count(p => p.IsDone);

    public double Progress => TotalCount == 0 ? 0 : (double)DoneCount / TotalCount;

    /// <summary>Edit progress (prompt_06 §4): edited or reviewed photos of the folder.</summary>
    public string ProgressText => Localizer.Instance.Format("EditedOf", DoneCount, TotalCount);

    /// <summary>Copies of this folder's photos that Windows accepted (the sum of the gallery counters, prompt_06 §4).</summary>
    public int PrintedCount => AllPhotos.Sum(p => p.PrintCount);

    public string PrintedText => Localizer.Instance.Plural("PrintedCopies", PrintedCount);

    public const double MinThumbScale = 0.6;
    public const double MaxThumbScale = 2;

    /// <summary>Gallery thumbnail size relative to the default (prompt_06 §4); remembered.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CardWidth), nameof(CardHeight), nameof(ThumbHeight))]
    public partial double ThumbScale { get; set; } = 1;

    public double CardWidth => Math.Round(176 * ThumbScale);

    public double ThumbHeight => Math.Round(132 * ThumbScale);

    public double CardHeight => ThumbHeight + 56;

    partial void OnThumbScaleChanged(double value)
    {
        _services.Settings.ThumbnailScale = Math.Round(value, 2);
        _thumbScaleSave?.Cancel();
        var cts = _thumbScaleSave = new CancellationTokenSource();
        // Saved once the slider rests, not at every step of a drag.
        _ = Task.Delay(600, cts.Token).ContinueWith(t =>
        {
            if (!t.IsCanceled) _services.SaveSettings();
        }, TaskScheduler.Default);
    }

    private CancellationTokenSource? _thumbScaleSave;

    /// <summary>Double-click on the slider: the default thumbnail size.</summary>
    [RelayCommand]
    public void ResetThumbScale() => ThumbScale = 1;

    public string FolderTitle => CurrentFolder is null ? string.Empty : Path.GetFileName(CurrentFolder.TrimEnd(Path.DirectorySeparatorChar));

    public string CountAll => AllPhotos.Count.ToString(Localizer.Instance.Culture);

    public string CountToDo => AllPhotos.Count(p => p.IsToDo).ToString(Localizer.Instance.Culture);

    public string CountDone => DoneCount.ToString(Localizer.Instance.Culture);

    public string CountDeleted => DeletedPhotos.Count.ToString(Localizer.Instance.Culture);

    /// <summary>Under Deleted, what Restore does and how long deleted photos are kept.</summary>
    public string DeletedNote => Localizer.Instance.Plural("DeletedNote", DeletedPhotos.Count);

    partial void OnFilterChanged(PhotoFilter value)
    {
        ApplyFilter();
        OnPropertyChanged(nameof(IsDeletedView));
        RaiseCounts();
    }

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

    /// <summary>Enter in the gallery: opens the selected folder, or edits the selected photo.</summary>
    public void OpenSelected()
    {
        if (SelectedGalleryItem is FolderItemViewModel folder) folder.Open();
        else EditPhoto(null);
    }

    public void Initialize()
    {
        var last = _services.Settings.LastFolder;
        Explorer.ShowStart(last);
        if (last is not null && Directory.Exists(last)) Explorer.Navigate(last);
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

    /// <summary>
    /// Opens an order folder (from the explorer). Unsaved edits of the folder being left are not dropped silently: the
    /// shell asks first, and the explorer stays on this folder when the operator cancels. The scan runs in the
    /// background; <see cref="OpenFolderAsync"/> can be awaited.
    /// </summary>
    public void OpenFolder(string folder) => _ = OpenFolderGuardedAsync(folder);

    private async Task OpenFolderGuardedAsync(string folder)
    {
        if (CurrentFolder is { } current && AllPhotos.Any(p => p.PendingRecipe is not null))
        {
            // The open folder again (a recent order, the tree): nothing to do, and reloading would drop the edits.
            if (string.Equals(Path.GetFullPath(folder), Path.GetFullPath(current), StringComparison.Ordinal)) return;
            if (ConfirmLeaveFolder is { } confirm && !await confirm())
            {
                Explorer.Mark(current);
                return;
            }
        }

        await OpenFolderAsync(folder);
    }

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
        Folders.Clear();
        DeletedPhotos.Clear();
        GalleryItems.Clear();
        RaiseCounts();
        var (sidecar, entries, subfolders, deleted) = await Task.Run(() =>
        {
            var store = new OrderStore(folder);
            var s = store.Load();
            if (store.UnhideRecordedPhotos(s) is > 0 and var unhidden)
            {
                Log.Warning("Made {Count} saved photos visible again in {Folder} (hidden by PhotoX 0.4.0)", unhidden, folder);
            }

            return (s, FolderScanner.Scan(folder), FolderScanner.SubFolders(folder), PhotoTrash.List(folder));
        });
        if (ct.IsCancellationRequested) return;
        IsScanning = false;
        if (Filter == PhotoFilter.Deleted && deleted.Count == 0) Filter = PhotoFilter.All;
        foreach (var f in subfolders) Folders.Add(Explorer.FolderItem(f));
        foreach (var d in deleted) DeletedPhotos.Add(DeletedItem(folder, d));

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
        _ = LoadThumbnailsAsync([.. items, .. DeletedPhotos], ct);
        Log.Information("Opened folder {Folder} with {Count} photos, {Folders} subfolders, {Deleted} deleted", folder, AllPhotos.Count, Folders.Count, DeletedPhotos.Count);
    }

    private PhotoItemViewModel DeletedItem(string folder, DeletedPhoto d) =>
        new(d.Entry(FormatSniffer.Detect(d.StoredPath)), PhotoState.ToDo)
        {
            DeletedKey = d.StoredName,
            DeletedUtc = d.DeletedUtc,
            PrintCount = _services.PrintHistory.CountFor(Path.Combine(folder, d.Name)),
        };

    /// <summary>After deleting (prompt_08 §5): the photos leave the gallery and appear under Deleted; other photos keep their state and unsaved edits.</summary>
    public void ApplyDeleted(IReadOnlyList<PhotoItemViewModel> items, IReadOnlyList<DeletedPhoto> deleted)
    {
        if (CurrentFolder is not { } folder) return;
        var byName = items.ToDictionary(p => p.Name, StringComparer.OrdinalIgnoreCase);
        var added = new List<PhotoItemViewModel>();
        foreach (var d in deleted)
        {
            if (byName.TryGetValue(d.Name, out var item)) AllPhotos.Remove(item);
            var card = DeletedItem(folder, d);
            card.Thumbnail = item?.Thumbnail;
            card.Info = item?.Info ?? string.Empty;
            added.Add(card);
        }

        foreach (var card in added) DeletedPhotos.Insert(IndexIn(DeletedPhotos, card), card);

        ApplyFilter();
        RaiseCounts();
        foreach (var card in added.Where(c => c.Thumbnail is null)) RefreshThumbnail(card);
    }

    /// <summary>After restoring (prompt_08 §5): the photos leave Deleted and return to the gallery in name order.</summary>
    public void ApplyRestored(IReadOnlyList<RestoredPhoto> restored)
    {
        if (CurrentFolder is not { } folder) return;
        var sidecar = new OrderStore(folder).Load();
        var entries = FolderScanner.Scan(folder).ToDictionary(e => e.Name, StringComparer.OrdinalIgnoreCase);
        foreach (var r in restored)
        {
            var card = DeletedPhotos.FirstOrDefault(p => string.Equals(p.DeletedKey, r.StoredName, StringComparison.OrdinalIgnoreCase));
            if (card is not null) DeletedPhotos.Remove(card);
            if (!entries.TryGetValue(r.Name, out var entry) || AllPhotos.Any(p => string.Equals(p.Name, r.Name, StringComparison.OrdinalIgnoreCase))) continue;
            var item = new PhotoItemViewModel(entry, StateFromSidecar(sidecar, entry))
            {
                Thumbnail = card?.Thumbnail,
                Info = card?.Info ?? string.Empty,
                PrintCount = _services.PrintHistory.CountFor(entry.Path),
            };
            AllPhotos.Insert(IndexIn(AllPhotos, item), item);
            if (item.Thumbnail is null) RefreshThumbnail(item);
        }

        ApplyFilter();
        RaiseCounts();
    }

    /// <summary>After Discard Originals: deleted photos removed for good leave the Deleted view.</summary>
    public void RemoveDeletedCards(IReadOnlyList<string> names)
    {
        foreach (var card in DeletedPhotos.Where(p => names.Contains(p.Name, StringComparer.OrdinalIgnoreCase)).ToList()) DeletedPhotos.Remove(card);
        if (Filter == PhotoFilter.Deleted && DeletedPhotos.Count == 0) Filter = PhotoFilter.All;
        ApplyFilter();
        RaiseCounts();
    }

    /// <summary>Where <paramref name="item"/> belongs in a list in natural name order.</summary>
    private static int IndexIn(IList<PhotoItemViewModel> list, PhotoItemViewModel item)
    {
        var i = 0;
        while (i < list.Count && Core.Naming.NaturalComparer.Instance.Compare(list[i].Name, item.Name) <= 0) i++;
        return i;
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
        OnPropertyChanged(nameof(PrintedCount));
        OnPropertyChanged(nameof(PrintedText));
        OnPropertyChanged(nameof(HasFolder));
        OnPropertyChanged(nameof(IsEmpty));
        OnPropertyChanged(nameof(FolderTitle));
        OnPropertyChanged(nameof(CountAll));
        OnPropertyChanged(nameof(CountToDo));
        OnPropertyChanged(nameof(CountDone));
        OnPropertyChanged(nameof(CountDeleted));
        OnPropertyChanged(nameof(DeletedNote));
        OnPropertyChanged(nameof(ShowNoPhotos));
        OnPropertyChanged(nameof(EmptyText));
    }

    /// <summary>
    /// Updates the visible photos in place: only photos that enter or leave the filter are inserted or removed,
    /// so the gallery keeps its cards (rebuilding hundreds of cards made returning from the editor slow).
    /// </summary>
    public void ApplyFilter()
    {
        var keep = SelectedPhoto;
        var keepFolder = SelectedGalleryItem as FolderItemViewModel;
        var wanted = Filter == PhotoFilter.Deleted
            ? DeletedPhotos.ToList()
            : AllPhotos.Where(p => Filter == PhotoFilter.All || (Filter == PhotoFilter.ToDo && p.IsToDo) || (Filter == PhotoFilter.Done && p.IsDone)).ToList();
        var wantedSet = wanted.ToHashSet();
        Sync(VisiblePhotos, wanted);
        // Folders stay reachable under All, To do and Done; Deleted lists only deleted photos.
        Sync(GalleryItems, (Filter == PhotoFilter.Deleted ? [] : Folders.Cast<object>()).Concat(VisiblePhotos).ToList());
        if (keepFolder is not null && GalleryItems.Contains(keepFolder)) return;
        SelectedPhoto = keep is not null && wantedSet.Contains(keep) ? keep : VisiblePhotos.FirstOrDefault();
    }

    /// <summary>Makes <paramref name="list"/> equal to <paramref name="wanted"/> by removing, moving and inserting only what differs.</summary>
    private static void Sync<T>(ObservableCollection<T> list, IReadOnlyList<T> wanted)
        where T : class
    {
        var wantedSet = wanted.ToHashSet();
        for (var i = list.Count - 1; i >= 0; i--)
        {
            if (!wantedSet.Contains(list[i])) list.RemoveAt(i);
        }

        for (var i = 0; i < wanted.Count; i++)
        {
            if (i < list.Count && ReferenceEquals(list[i], wanted[i])) continue;
            var existing = list.IndexOf(wanted[i]);
            if (existing >= 0) list.Move(existing, i);
            else list.Insert(i, wanted[i]);
        }
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
        foreach (var p in AllPhotos.Concat(DeletedPhotos)) p.RefreshTexts();
        RaiseCounts();
    }

    private Task LoadThumbnailsAsync(IReadOnlyList<PhotoItemViewModel> items, CancellationToken ct) =>
        ThumbnailSource.LoadAllAsync(_services.ThumbnailCache, items, item => item.HasPreview ? item.Entry : null, (item, bitmap, info, error) =>
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
