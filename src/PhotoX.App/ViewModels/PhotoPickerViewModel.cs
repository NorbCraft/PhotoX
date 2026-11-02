using System.Collections.ObjectModel;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PhotoX.App.Services;
using PhotoX.Imaging;
using PhotoX.Storage;

namespace PhotoX.App.ViewModels;

/// <summary>A photo offered by the photo picker.</summary>
public sealed partial class PickerPhoto(PhotoEntry entry) : ViewModelBase
{
    public PhotoEntry Entry { get; } = entry;

    public string Name => Entry.Name;

    public string Path => Entry.Path;

    [ObservableProperty]
    public partial Bitmap? Thumbnail { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsOpenable))]
    public partial string? Error { get; set; }

    public bool IsOpenable => Entry.Kind is ImageKind.Jpeg or ImageKind.Png && Error is null;
}

/// <summary>
/// Chooses photos with the built-in explorer (prompt_04 §6): the same folder tree as the Photos workspace and a
/// thumbnail grid of the open folder, its subfolders first (prompt_08 §1). Used by the ID Photos workspace (portrait)
/// and the Collages workspace; each keeps its own folder.
/// </summary>
public sealed partial class PhotoPickerViewModel : ViewModelBase
{
    private readonly AppServices _services;
    private CancellationTokenSource _cts = new();

    public PhotoPickerViewModel(AppServices services, string workspace)
    {
        _services = services;
        Explorer = new ExplorerViewModel(services, workspace);
        Explorer.FolderOpened += folder => _ = OpenFolderAsync(folder);
    }

    /// <summary>Raised when a photo is chosen (double-click, Enter or the pick button).</summary>
    public event Action<PickerPhoto>? Picked;

    public ExplorerViewModel Explorer { get; }

    /// <summary>Card size in the thumbnail grid (larger for the portrait picker, smaller beside a collage).</summary>
    public double ThumbWidth { get; init; } = 150;

    public double ThumbHeight { get; init; } = 150;

    /// <summary>Photos can be dragged out of the grid (onto a collage frame, prompt_06 §6).</summary>
    public bool CanDrag { get; init; }

    partial void OnSelectedPhotoChanged(PickerPhoto? value)
    {
        if (_syncing) return;
        _syncing = true;
        SelectedItem = value;
        _syncing = false;
    }

    private bool _syncing;

    /// <summary>
    /// The selected card: a photo or a folder (the grid's selection). A click only selects, as in a file explorer; a
    /// double-click or Enter picks (prompt_09 §7).
    /// </summary>
    [ObservableProperty]
    public partial object? SelectedItem { get; set; }

    partial void OnSelectedItemChanged(object? value)
    {
        if (_syncing) return;
        _syncing = true;
        SelectedPhoto = value as PickerPhoto;
        _syncing = false;
    }

    /// <summary>No photo selected (after the ID photo is cleared).</summary>
    public void ClearSelection() => SelectedPhoto = null;

    public ObservableCollection<PickerPhoto> Photos { get; } = [];

    /// <summary>The cards of the grid: the subfolders, then the photos.</summary>
    public ObservableCollection<object> Items { get; } = [];

    [ObservableProperty]
    public partial PickerPhoto? SelectedPhoto { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEmpty))]
    public partial bool IsScanning { get; set; }

    public bool IsEmpty => Explorer.CurrentFolder is not null && !IsScanning && Items.Count == 0;

    public bool HasFolder => Explorer.CurrentFolder is not null;

    /// <summary>Picks the photo, or opens the folder (double-click, Enter, the pick button).</summary>
    [RelayCommand]
    public void Pick(object? item)
    {
        switch (item ?? SelectedItem)
        {
            case FolderItemViewModel folder: folder.Open(); break;
            case PickerPhoto { IsOpenable: true } photo: Picked?.Invoke(photo); break;
        }
    }

    /// <summary>
    /// Shows the explorer at <paramref name="folder"/> (usually the order open in Photos) the first time the picker is
    /// used; later it keeps its own folder.
    /// </summary>
    public void EnsureStarted(string? folder)
    {
        Explorer.RefreshRecent();
        if (Explorer.CurrentFolder is not null) return;
        // Its own folder from last time first (prompt_08 §4), then the one open in Photos.
        if (Explorer.RememberedFolder is { } own && Directory.Exists(own)) folder = own;
        Explorer.ShowStart(folder);
        if (folder is not null && Directory.Exists(folder)) Explorer.Navigate(folder);
    }

    public async Task OpenFolderAsync(string folder)
    {
        _cts.Cancel();
        _cts = new CancellationTokenSource();
        var ct = _cts.Token;
        IsScanning = true;
        SelectedPhoto = null;
        Photos.Clear();
        Items.Clear();
        OnPropertyChanged(nameof(HasFolder));
        var (entries, folders) = await Task.Run(() => (FolderScanner.Scan(folder), FolderScanner.SubFolders(folder)));
        if (ct.IsCancellationRequested) return;
        var items = entries.Select(e => new PickerPhoto(e)).ToList();
        foreach (var f in folders) Items.Add(Explorer.FolderItem(f));
        foreach (var item in items)
        {
            Photos.Add(item);
            Items.Add(item);
        }

        SelectedPhoto = items.FirstOrDefault(p => p.IsOpenable);
        IsScanning = false;
        _ = ThumbnailSource.LoadAllAsync(_services.ThumbnailCache, items, p => p.IsOpenable ? p.Entry : null, (p, bitmap, _, error) =>
        {
            p.Thumbnail = bitmap;
            p.Error = error;
        }, ct);
    }
}
