using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PhotoX.App.Localization;
using PhotoX.App.Services;

namespace PhotoX.App.ViewModels;

/// <summary>
/// The built-in folder explorer (FR-001): locations, the four most recent orders and the folder tree. One class
/// serves the Photos, ID Photos and Collages workspaces (prompt_04 §6); each workspace has its own instance.
/// </summary>
public sealed partial class ExplorerViewModel : ViewModelBase
{
    private readonly AppServices _services;

    public ExplorerViewModel(AppServices services)
    {
        _services = services;
        RecentCollapsed = services.Settings.RecentCollapsed;
        LoadLocations();
        Localizer.Instance.LanguageChanged += (_, _) => LoadLocations();
    }

    /// <summary>Raised when the operator opens a folder (tree click or recent order).</summary>
    public event Action<string>? FolderOpened;

    public ObservableCollection<LocationItem> Locations { get; } = [];

    public ObservableCollection<FolderNodeViewModel> FolderRoots { get; } = [];

    public ObservableCollection<string> RecentFolders { get; } = [];

    /// <summary>The open folder, as marked in the tree.</summary>
    [ObservableProperty]
    public partial string? CurrentFolder { get; set; }

    [ObservableProperty]
    public partial FolderNodeViewModel? SelectedFolderNode { get; set; }

    /// <summary>The recent-orders list is folded away (prompt_03 §2); remembered for all workspaces.</summary>
    [ObservableProperty]
    public partial bool RecentCollapsed { get; set; }

    partial void OnRecentCollapsedChanged(bool value)
    {
        if (_services.Settings.RecentCollapsed == value) return;
        _services.Settings.RecentCollapsed = value;
        _services.SaveSettings();
    }

    /// <summary>Clicking a folder selects and opens it and shows its subfolders, like a desktop file explorer (prompt_03 §2).</summary>
    partial void OnSelectedFolderNodeChanged(FolderNodeViewModel? value)
    {
        if (value is null || string.IsNullOrEmpty(value.Path)) return;
        value.IsExpanded = true;
        if (!string.Equals(value.Path, CurrentFolder, StringComparison.Ordinal)) Open(value.Path);
    }

    [RelayCommand]
    private void ToggleRecent() => RecentCollapsed = !RecentCollapsed;

    [RelayCommand]
    public void SelectLocation(LocationItem? location)
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
        Open(folder);
    }

    /// <summary>Opens <paramref name="folder"/> as if clicked: marks it and raises <see cref="FolderOpened"/>.</summary>
    public void Open(string folder)
    {
        if (!Directory.Exists(folder)) return;
        CurrentFolder = folder;
        FolderOpened?.Invoke(folder);
    }

    /// <summary>Shows the drive or home folder that contains <paramref name="folder"/> with the path to it expanded.</summary>
    public bool RevealInTree(string folder)
    {
        var location = Locations.Where(l => folder.StartsWith(l.Path, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(l => l.Path.Length).FirstOrDefault();
        if (location is null) return false;
        SelectLocation(location);
        var node = FolderRoots.FirstOrDefault();
        var rel = Path.GetRelativePath(location.Path, folder);
        if (rel == ".") return true;
        foreach (var part in rel.Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries))
        {
            if (node is null) return true;
            node.EnsureLoaded();
            node.IsExpanded = true;
            node = node.Children.FirstOrDefault(c => string.Equals(c.Name, part, StringComparison.OrdinalIgnoreCase));
        }

        return true;
    }

    /// <summary>Shows <paramref name="folder"/> in the tree, or the first location when it lies elsewhere; opens nothing.</summary>
    public void ShowStart(string? folder)
    {
        if (folder is not null && Directory.Exists(folder) && RevealInTree(folder)) return;
        if (Locations.Count > 0) SelectLocation(Locations[0]);
    }

    public void RefreshRecent()
    {
        RecentFolders.Clear();
        foreach (var f in _services.Settings.RecentFolders.Where(Directory.Exists).Take(4))
        {
            RecentFolders.Add(f);
        }

        RecentCollapsed = _services.Settings.RecentCollapsed;
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
}
