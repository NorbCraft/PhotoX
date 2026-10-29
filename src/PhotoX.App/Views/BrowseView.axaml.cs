using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using PhotoX.App.ViewModels;

namespace PhotoX.App.Views;

public sealed partial class BrowseView : UserControl
{
    private bool _selectedByThisPress;

    public BrowseView()
    {
        InitializeComponent();
        // A click on a folder selects and expands it (view model); a further click on the selected folder folds it.
        Folders.AddHandler(PointerPressedEvent, (_, _) => _selectedByThisPress = false, RoutingStrategies.Tunnel);
        Folders.SelectionChanged += (_, _) => _selectedByThisPress = true;
        Folders.Tapped += OnFolderTapped;
    }

    private void OnFolderTapped(object? sender, TappedEventArgs e)
    {
        if (e.Source is not Visual source || source.FindAncestorOfType<ToggleButton>(includeSelf: true) is not null) return; // the arrow toggles itself
        if (source.FindAncestorOfType<TreeViewItem>(includeSelf: true)?.DataContext is not FolderNodeViewModel node) return;
        if (!_selectedByThisPress && DataContext is BrowseViewModel { SelectedFolderNode: { } selected } && selected == node && node.Children.Count > 0)
        {
            node.IsExpanded = !node.IsExpanded;
        }
    }

    private void OnPhotoDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (sender is Control { DataContext: PhotoItemViewModel item } && DataContext is BrowseViewModel vm)
        {
            vm.EditPhotoCommand.Execute(item);
        }
    }
}
