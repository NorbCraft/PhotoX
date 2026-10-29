using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using PhotoX.App.Localization;
using PhotoX.Core.Editing;
using PhotoX.Imaging;
using PhotoX.Storage;

namespace PhotoX.App.ViewModels;

public enum PhotoState
{
    ToDo,
    Edited,
    Reviewed,
    Unsaved,
    Error,
    UnsupportedHeic,
    Unsupported,
}

/// <summary>A photo of the current order, shared by the gallery, filmstrip and editor.</summary>
public sealed partial class PhotoItemViewModel : ViewModelBase
{
    public PhotoItemViewModel(PhotoEntry entry, PhotoState state)
    {
        Entry = entry;
        State = entry.Kind switch
        {
            ImageKind.Heic => PhotoState.UnsupportedHeic,
            ImageKind.OtherImage or ImageKind.Unknown => PhotoState.Unsupported,
            _ => state,
        };
    }

    public PhotoEntry Entry { get; private set; }

    public string Name => Entry.Name;

    public string Path => Entry.Path;

    [ObservableProperty]
    public partial Bitmap? Thumbnail { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StateText), nameof(IsDone), nameof(IsToDo), nameof(IsOpenable), nameof(HasBadge), nameof(IsWarning))]
    public partial PhotoState State { get; set; }

    [ObservableProperty]
    public partial string Info { get; set; } = string.Empty;

    /// <summary>True for the photo open in the editor (filmstrip highlight).</summary>
    [ObservableProperty]
    public partial bool IsCurrent { get; set; }

    /// <summary>Manual edits made in this session but not saved (FR-019).</summary>
    public EditRecipe? PendingRecipe { get; set; }

    /// <summary>Explanation when the photo cannot be opened.</summary>
    public string? ErrorText { get; set; }

    public bool IsDone => State is PhotoState.Edited or PhotoState.Reviewed;

    public bool IsToDo => State is PhotoState.ToDo or PhotoState.Unsaved;

    public bool IsOpenable => State is not (PhotoState.Error or PhotoState.UnsupportedHeic or PhotoState.Unsupported);

    public bool HasBadge => State != PhotoState.ToDo;

    public bool IsWarning => State is PhotoState.Unsaved or PhotoState.Error or PhotoState.UnsupportedHeic or PhotoState.Unsupported;

    public string StateText => Localizer.Instance[State switch
    {
        PhotoState.Edited => "StateEdited",
        PhotoState.Reviewed => "StateReviewed",
        PhotoState.Unsaved => "StateUnsaved",
        PhotoState.Error => "StateError",
        PhotoState.UnsupportedHeic => "StateUnsupportedHeic",
        PhotoState.Unsupported => "StateUnsupported",
        _ => "StateToDo",
    }];

    public void RefreshTexts() => OnPropertyChanged(nameof(StateText));

    public void UpdateEntry()
    {
        var fi = new FileInfo(Path);
        if (fi.Exists)
        {
            Entry = Entry with { Length = fi.Length, LastWriteUtc = fi.LastWriteTimeUtc };
        }
    }
}
