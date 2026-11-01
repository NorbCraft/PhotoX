using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PhotoX.Storage;

namespace PhotoX.App.ViewModels;

/// <summary>Lazily loaded folder in the built-in explorer (FR-001).</summary>
public sealed partial class FolderNodeViewModel : ViewModelBase
{
    private bool _loaded;

    public FolderNodeViewModel(string path, string? name = null)
    {
        Path = path;
        var leaf = System.IO.Path.GetFileName(path.TrimEnd(System.IO.Path.DirectorySeparatorChar));
        Name = name ?? (string.IsNullOrEmpty(leaf) ? path : leaf);
        // A placeholder child shows the expander arrow until the real children load; folders without subfolders get none.
        if (FolderScanner.HasSubFolders(path)) Children.Add(Placeholder);
        else _loaded = true;
    }

    private static FolderNodeViewModel Placeholder { get; } = new(true);

    private FolderNodeViewModel(bool placeholder)
    {
        Path = string.Empty;
        Name = "…";
        _loaded = placeholder;
    }

    public string Path { get; }

    public string Name { get; }

    public ObservableCollection<FolderNodeViewModel> Children { get; } = [];

    [ObservableProperty]
    public partial bool IsExpanded { get; set; }

    partial void OnIsExpandedChanged(bool value)
    {
        if (value) EnsureLoaded();
    }

    public void EnsureLoaded()
    {
        if (_loaded) return;
        _loaded = true;
        Children.Clear();
        foreach (var d in FolderScanner.SubFolders(Path))
        {
            Children.Add(new FolderNodeViewModel(d));
        }
    }
}

public sealed record LocationItem(string Name, string Path, bool IsRemovable);

/// <summary>
/// A subfolder shown as a card in a gallery, before the photos (prompt_08 §1). Double-click (or Enter) opens it in the
/// workspace's explorer, which also selects it in the tree.
/// </summary>
public sealed partial class FolderItemViewModel(string path, ExplorerViewModel explorer) : ViewModelBase
{
    public string Path { get; } = path;

    public string Name { get; } = ExplorerViewModel.FolderName(path);

    [RelayCommand]
    public void Open() => explorer.Navigate(Path);
}
