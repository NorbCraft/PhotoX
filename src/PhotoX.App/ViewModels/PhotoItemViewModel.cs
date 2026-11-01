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
    [NotifyPropertyChangedFor(nameof(StateText), nameof(IsDone), nameof(IsToDo), nameof(IsOpenable), nameof(HasPreview), nameof(HasBadge), nameof(ShowStateLine), nameof(IsWarning))]
    public partial PhotoState State { get; set; }

    /// <summary>
    /// The photo's file name in <c>Original/</c> when it was deleted from the gallery (prompt_08 §5): such a photo is
    /// shown only under Deleted and can only be restored.
    /// </summary>
    public string? DeletedKey { get; init; }

    public DateTime DeletedUtc { get; init; }

    public bool IsDeleted => DeletedKey is not null;

    [ObservableProperty]
    public partial string Info { get; set; } = string.Empty;

    /// <summary>Copies of this photo accepted by Windows through PhotoX (prompt_05 §6); not a physical-print confirmation.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsPrinted), nameof(PrintCountText), nameof(PrintCountTip))]
    public partial int PrintCount { get; set; }

    public bool IsPrinted => PrintCount > 0;

    public string PrintCountText => PrintCount > 0 ? PrintCount.ToString(Localizer.Instance.Culture) : "–";

    public string PrintCountTip => PrintCount > 0 ? Localizer.Instance.Plural("PrintedTimes", PrintCount) : Localizer.Instance["NotPrinted"];

    /// <summary>True for the photo open in the editor (filmstrip highlight).</summary>
    [ObservableProperty]
    public partial bool IsCurrent { get; set; }

    /// <summary>Manual edits made in this session but not saved (FR-019).</summary>
    public EditRecipe? PendingRecipe { get; set; }

    /// <summary>Explanation when the photo cannot be opened.</summary>
    public string? ErrorText { get; set; }

    public bool IsDone => State is PhotoState.Edited or PhotoState.Reviewed;

    public bool IsToDo => State is PhotoState.ToDo or PhotoState.Unsaved;

    /// <summary>Can be edited, printed or used for an ID photo: a readable photo that is not deleted.</summary>
    public bool IsOpenable => !IsDeleted && HasPreview;

    /// <summary>A thumbnail can be made (deleted photos included).</summary>
    public bool HasPreview => State is not (PhotoState.Error or PhotoState.UnsupportedHeic or PhotoState.Unsupported);

    public bool HasBadge => !IsDeleted && State != PhotoState.ToDo;

    /// <summary>The state line under the name replaces the size line (an edited, unsaved, failing or deleted photo).</summary>
    public bool ShowStateLine => HasBadge || IsDeleted;

    public bool IsWarning => IsDeleted || State is PhotoState.Unsaved or PhotoState.Error or PhotoState.UnsupportedHeic or PhotoState.Unsupported;

    public string StateText => IsDeleted
        ? Localizer.Instance.Format("StateDeleted", DeletedUtc.ToLocalTime().ToString("g", Localizer.Instance.Culture))
        : Localizer.Instance[State switch
    {
        PhotoState.Edited => "StateEdited",
        PhotoState.Reviewed => "StateReviewed",
        PhotoState.Unsaved => "StateUnsaved",
        PhotoState.Error => "StateError",
        PhotoState.UnsupportedHeic => "StateUnsupportedHeic",
        PhotoState.Unsupported => "StateUnsupported",
        _ => "StateToDo",
    }];

    public void RefreshTexts()
    {
        OnPropertyChanged(nameof(StateText));
        OnPropertyChanged(nameof(PrintCountTip));
    }

    public void UpdateEntry()
    {
        var fi = new FileInfo(Path);
        if (fi.Exists)
        {
            Entry = Entry with { Length = fi.Length, LastWriteUtc = fi.LastWriteTimeUtc };
        }
    }
}
