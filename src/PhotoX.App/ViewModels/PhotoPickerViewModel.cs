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
/// thumbnail grid of the open folder. Used by the ID Photos workspace (portrait) and the Collages workspace.
/// </summary>
public sealed partial class PhotoPickerViewModel : ViewModelBase
{
    private readonly AppServices _services;
    private CancellationTokenSource _cts = new();

    public PhotoPickerViewModel(AppServices services)
    {
        _services = services;
        Explorer = new ExplorerViewModel(services);
        Explorer.FolderOpened += folder => _ = OpenFolderAsync(folder);
    }

    /// <summary>Raised when a photo is chosen (double-click, Enter or the pick button).</summary>
    public event Action<PickerPhoto>? Picked;

    public ExplorerViewModel Explorer { get; }

    /// <summary>Card size in the thumbnail grid (larger for the portrait picker, smaller beside a collage).</summary>
    public double ThumbWidth { get; init; } = 150;

    public double ThumbHeight { get; init; } = 150;

    /// <summary>A click (selection) picks the photo, as in ID Photos where the explorer replaces the file dialog (prompt_05 §7).</summary>
    public bool PickOnSelect { get; init; }

    /// <summary>Photos can be dragged out of the grid (onto a collage frame, prompt_06 §6).</summary>
    public bool CanDrag { get; init; }

    private bool _opening;

    partial void OnSelectedPhotoChanged(PickerPhoto? value)
    {
        if (PickOnSelect && !_opening && value is { IsOpenable: true }) Picked?.Invoke(value);
    }

    public ObservableCollection<PickerPhoto> Photos { get; } = [];

    [ObservableProperty]
    public partial PickerPhoto? SelectedPhoto { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEmpty))]
    public partial bool IsScanning { get; set; }

    public bool IsEmpty => Explorer.CurrentFolder is not null && !IsScanning && Photos.Count == 0;

    public bool HasFolder => Explorer.CurrentFolder is not null;

    [RelayCommand]
    public void Pick(PickerPhoto? photo)
    {
        photo ??= SelectedPhoto;
        if (photo is { IsOpenable: true }) Picked?.Invoke(photo);
    }

    /// <summary>
    /// Shows the explorer at <paramref name="folder"/> (usually the order open in Photos) the first time the picker is
    /// used; later it keeps its own folder.
    /// </summary>
    public void EnsureStarted(string? folder)
    {
        Explorer.RefreshRecent();
        if (Explorer.CurrentFolder is not null) return;
        Explorer.ShowStart(folder);
        if (folder is not null) Explorer.Open(folder);
    }

    public async Task OpenFolderAsync(string folder)
    {
        _cts.Cancel();
        _cts = new CancellationTokenSource();
        var ct = _cts.Token;
        IsScanning = true;
        _opening = true;
        SelectedPhoto = null;
        Photos.Clear();
        OnPropertyChanged(nameof(HasFolder));
        var entries = await Task.Run(() => FolderScanner.Scan(folder));
        if (ct.IsCancellationRequested) return;
        var items = entries.Select(e => new PickerPhoto(e)).ToList();
        foreach (var item in items) Photos.Add(item);
        if (!PickOnSelect) SelectedPhoto = items.FirstOrDefault(p => p.IsOpenable);
        _opening = false;
        IsScanning = false;
        _ = ThumbnailSource.LoadAllAsync(_services.ThumbnailCache, items, p => p.IsOpenable ? p.Entry : null, (p, bitmap, _, error) =>
        {
            p.Thumbnail = bitmap;
            p.Error = error;
        }, ct);
    }
}
