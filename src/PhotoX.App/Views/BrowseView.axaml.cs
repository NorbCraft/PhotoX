using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using PhotoX.App.ViewModels;

namespace PhotoX.App.Views;

public sealed partial class BrowseView : UserControl
{
    public BrowseView()
    {
        InitializeComponent();
        ApplyLeftWidth();
        PanelWidth.Apply(Root.ColumnDefinitions[4], "browse-right", 330, 290);
        // The view model gets the whole selection (Ctrl/Shift + click), not only the anchor photo.
        Gallery.SelectionChanged += (_, _) =>
        {
            if (DataContext is BrowseViewModel vm) vm.SetSelection(Gallery.SelectedItems?.OfType<PhotoItemViewModel>() ?? []);
        };
    }

    private void ApplyLeftWidth() => PanelWidth.Apply(Root.ColumnDefinitions[0], "browse-left", 290, PanelWidth.MinLeft);

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        if (DataContext is BrowseViewModel vm) PanelWidth.FollowTree(vm.Explorer, Root.ColumnDefinitions[0], TreePanel, TreeSplitter, ApplyLeftWidth);
    }

    private void OnLeftSplitterDragCompleted(object? sender, VectorEventArgs e) => PanelWidth.Save("browse-left", Root.ColumnDefinitions[0]);

    private void OnRightSplitterDragCompleted(object? sender, VectorEventArgs e) => PanelWidth.Save("browse-right", Root.ColumnDefinitions[4]);

    private void OnThumbSizeDoubleTapped(object? sender, TappedEventArgs e) => (DataContext as BrowseViewModel)?.ResetThumbScale();

    private void OnPhotoDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (sender is Control { DataContext: PhotoItemViewModel item } && DataContext is BrowseViewModel vm)
        {
            vm.EditPhotoCommand.Execute(item);
        }
    }

    // Context menu items live in a popup, so they reach the view model through this view.
    private void OnEditClick(object? sender, RoutedEventArgs e) => Run(sender, (vm, item) => vm.EditPhotoCommand.Execute(item));

    private void OnUseForIdClick(object? sender, RoutedEventArgs e) => Run(sender, (vm, item) => vm.UseForIdCommand.Execute(item));

    private void OnDuplicateClick(object? sender, RoutedEventArgs e) => Run(sender, (vm, item) => vm.DuplicateCommand.Execute(item));

    private void OnPrintClick(object? sender, RoutedEventArgs e) => Run(sender, (vm, item) => vm.PrintSelectedCommand.Execute(item));

    private void OnRestoreClick(object? sender, RoutedEventArgs e) => Run(sender, (vm, item) => vm.RestoreOriginalCommand.Execute(item));

    private void OnDeleteClick(object? sender, RoutedEventArgs e) => Run(sender, (vm, item) => vm.DeleteSelectedCommand.Execute(item));

    private void OnRestoreDeletedClick(object? sender, RoutedEventArgs e) => Run(sender, (vm, item) => vm.RestoreDeletedCommand.Execute(item));

    private void Run(object? sender, Action<BrowseViewModel, PhotoItemViewModel> action)
    {
        if (sender is Control { DataContext: PhotoItemViewModel item } && DataContext is BrowseViewModel vm) action(vm, item);
    }
}
