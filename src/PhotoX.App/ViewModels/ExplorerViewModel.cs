using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PhotoX.App.Localization;
using PhotoX.App.Services;

namespace PhotoX.App.ViewModels;

/// <summary>
/// The built-in folder explorer (FR-001): locations, the four most recent orders and the folder tree. One class
/// serves the Photos, ID Photos and Collages workspaces (prompt_04 §6); each workspace has its own instance, so each
/// keeps its own folder and shows or hides its own tree (prompt_08 §4). The galleries list the open folder's
/// subfolders as <see cref="FolderItemViewModel"/> cards that open through <see cref="Navigate"/>.
/// </summary>
public sealed partial class ExplorerViewModel : ViewModelBase
{
    public const string PhotosWorkspace = "photos";
    public const string IdWorkspace = "id";
    public const string CollageWorkspace = "collage";

    private readonly AppServices _services;

    public ExplorerViewModel(AppServices services, string workspace)
    {
        _services = services;
        Workspace = workspace;
        IsTreeVisible = !services.Settings.TreeHidden.Contains(workspace);
        RecentCollapsed = services.Settings.RecentCollapsed;
        LoadLocations();
        Localizer.Instance.LanguageChanged += (_, _) => LoadLocations();
    }

    /// <summary>Raised when the operator opens a folder (tree click or recent order).</summary>
    public event Action<string>? FolderOpened;

    public ObservableCollection<LocationItem> Locations { get; } = [];

    public ObservableCollection<FolderNodeViewModel> FolderRoots { get; } = [];

    public ObservableCollection<string> RecentFolders { get; } = [];

    /// <summary>"photos", "id" or "collage": the key of this workspace's remembered folder and tree visibility.</summary>
    public string Workspace { get; }

    /// <summary>The open folder, as marked in the tree.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanGoUp), nameof(ParentFolder), nameof(UpTip))]
    [NotifyCanExecuteChangedFor(nameof(GoUpCommand))]
    public partial string? CurrentFolder { get; set; }

    /// <summary>The folder tree is shown (prompt_08 §4); remembered for this workspace only.</summary>
    [ObservableProperty]
    public partial bool IsTreeVisible { get; set; }

    partial void OnIsTreeVisibleChanged(bool value)
    {
        var hidden = _services.Settings.TreeHidden;
        if (value == !hidden.Contains(Workspace)) return;
        if (value) hidden.Remove(Workspace);
        else hidden.Add(Workspace);
        _services.SaveSettings();
    }

    [RelayCommand]
    public void ToggleTree() => IsTreeVisible = !IsTreeVisible;

    /// <summary>The folder this workspace had open last time (Photos keeps it as the last folder of the app).</summary>
    public string? RememberedFolder => Workspace == PhotosWorkspace
        ? _services.Settings.LastFolder
        : _services.Settings.WorkspaceFolders.GetValueOrDefault(Workspace);

    /// <summary>The parent of the open folder (none at a drive's root).</summary>
    public string? ParentFolder =>
        CurrentFolder is { } folder && Path.GetDirectoryName(folder.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)) is { Length: > 0 } parent
        && Directory.Exists(parent) ? parent : null;

    public bool CanGoUp => ParentFolder is not null;

    public string UpTip => ParentFolder is { } p ? Localizer.Instance.Format("UpTo", FolderName(p)) : Localizer.Instance["Up"];

    /// <summary>Back to the parent folder (the Up button above the gallery, Backspace).</summary>
    [RelayCommand(CanExecute = nameof(CanGoUp))]
    public void GoUp()
    {
        if (ParentFolder is { } parent) Navigate(parent);
    }

    /// <summary>A gallery card for the subfolder <paramref name="path"/>; double-click opens it here.</summary>
    public FolderItemViewModel FolderItem(string path) => new(path, this);

    /// <summary>
    /// Opens <paramref name="folder"/> from the gallery (a folder card, Up) and selects it in the tree, expanding the
    /// path to it, as if it had been clicked there.
    /// </summary>
    public void Navigate(string folder)
    {
        if (!Directory.Exists(folder)) return;
        var node = FindNode(folder);
        if (node is null && RevealInTree(folder)) node = FindNode(folder);
        if (node is null)
        {
            Open(folder);
            return;
        }

        if (SelectedFolderNode == node && !string.Equals(node.Path, CurrentFolder, StringComparison.Ordinal)) Open(folder);
        SelectedFolderNode = node;
    }

    /// <summary>Marks <paramref name="folder"/> as the open folder (and in the tree) without opening it again.</summary>
    public void Mark(string folder)
    {
        CurrentFolder = folder;
        if (FindNode(folder) is { } node) SelectedFolderNode = node;
    }

    /// <summary>The loaded tree node of <paramref name="folder"/>, loading and expanding the path to it.</summary>
    private FolderNodeViewModel? FindNode(string folder)
    {
        foreach (var root in FolderRoots)
        {
            if (!IsInside(root.Path, folder)) continue;
            var node = root;
            var rel = Path.GetRelativePath(root.Path, folder);
            if (rel == ".") return node;
            foreach (var part in rel.Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries))
            {
                node.EnsureLoaded();
                node.IsExpanded = true;
                node = node.Children.FirstOrDefault(c => string.Equals(c.Name, part, StringComparison.OrdinalIgnoreCase));
                if (node is null) return null;
            }

            return node;
        }

        return null;
    }

    /// <summary>True when <paramref name="path"/> is <paramref name="root"/> or lies below it.</summary>
    internal static bool IsInside(string root, string path)
    {
        var rel = Path.GetRelativePath(root, path);
        return rel == "." || (!Path.IsPathRooted(rel) && rel != ".." && !rel.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal));
    }

    internal static string FolderName(string path)
    {
        var name = Path.GetFileName(path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        return string.IsNullOrEmpty(name) ? path : name;
    }

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
        if (Workspace != PhotosWorkspace)
        {
            _services.Settings.WorkspaceFolders[Workspace] = folder;
            _services.SaveSettings();
        }

        FolderOpened?.Invoke(folder);
    }

    /// <summary>Shows the drive or home folder that contains <paramref name="folder"/> with the path to it expanded.</summary>
    public bool RevealInTree(string folder)
    {
        var location = Locations.Where(l => IsInside(l.Path, folder))
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
        OnPropertyChanged(nameof(UpTip));
    }
}
