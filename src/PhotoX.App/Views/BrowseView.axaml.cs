using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using PhotoX.App.Services;
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
        // ↑ and ↓ follow the thumbnail grid; the list itself only knows ← and → in a wrapping panel (prompt_10 §7).
        Gallery.AddHandler(KeyDownEvent, OnGalleryKeyDown, RoutingStrategies.Tunnel);
    }

    private void OnGalleryKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key is not (Key.Up or Key.Down) || (e.KeyModifiers & ~KeyModifiers.Shift) != KeyModifiers.None) return;
        e.Handled = true;
        var count = Gallery.ItemCount;
        var cards = Enumerable.Range(0, count).Select(i => Gallery.ContainerFromIndex(i)?.Bounds ?? default).ToList();
        var from = Gallery.IndexFromContainer(TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement() as Control ?? Gallery);
        if (from < 0) from = Gallery.SelectedIndex;
        var to = GridNavigation.Neighbour(cards, from, e.Key == Key.Down ? 1 : -1);
        if (to < 0) return;
        if (e.KeyModifiers.HasFlag(KeyModifiers.Shift))
        {
            // Shift extends the selection from its anchor, as with ← and →.
            var anchor = Gallery.Selection.AnchorIndex >= 0 ? Gallery.Selection.AnchorIndex : from;
            Gallery.Selection.BeginBatchUpdate();
            Gallery.Selection.Clear();
            Gallery.Selection.SelectRange(Math.Min(anchor, to), Math.Max(anchor, to));
            Gallery.Selection.AnchorIndex = anchor;
            Gallery.Selection.EndBatchUpdate();
        }
        else
        {
            Gallery.SelectedIndex = to;
        }

        Gallery.ScrollIntoView(to);
        Gallery.ContainerFromIndex(to)?.Focus(NavigationMethod.Pointer);    // like ← / →: no focus frame over the blue border
    }

    private bool _focusInGallery;

    /// <summary>No control has the keyboard (the focused card was removed with the old folder's cards).</summary>
    private bool IsFocusLost() =>
        TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement() is not Visual v || v is Window || TopLevel.GetTopLevel(v) is null;

    private void OnThumbSmallerClick(object? sender, RoutedEventArgs e) => ChangeColumns(+1);

    private void OnThumbLargerClick(object? sender, RoutedEventArgs e) => ChangeColumns(-1);

    /// <summary>One more or one fewer thumbnail per row in the gallery's current width (prompt_10 §8); the selected photo stays in view.</summary>
    private void ChangeColumns(int delta)
    {
        if (DataContext is not BrowseViewModel vm || Gallery.ItemsPanelRoot is not { } panel) return;
        vm.ChangeColumns(delta, panel.Bounds.Width);
        if (Gallery.SelectedItem is { } selected) Dispatcher.UIThread.Post(() => Gallery.ScrollIntoView(selected), DispatcherPriority.Background);
    }

    private void ApplyLeftWidth() => PanelWidth.Apply(Root.ColumnDefinitions[0], "browse-left", 290, PanelWidth.MinLeft);

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        if (DataContext is not BrowseViewModel vm) return;
        PanelWidth.FollowTree(vm.Explorer, Root.ColumnDefinitions[0], TreePanel, TreeSplitter, ApplyLeftWidth);
        // A new folder empties the gallery, which drops the keyboard focus; when the focus was in the gallery (Backspace,
        // Up, a folder card), it goes back to the selected card, so the arrow keys stay here and not in the tree (prompt_11 §4).
        vm.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(BrowseViewModel.IsScanning) && vm.IsScanning) _focusInGallery = Gallery.IsKeyboardFocusWithin || IsFocusLost();
        };
        vm.FolderLoaded += () =>
        {
            if (!_focusInGallery && !IsFocusLost()) return;
            Dispatcher.UIThread.Post(() =>
            {
                if (Gallery.SelectedItem is { } item)
                {
                    Gallery.ScrollIntoView(item);
                    if (Gallery.ContainerFromItem(item) is { } card) card.Focus(NavigationMethod.Pointer);
                }
                else
                {
                    Gallery.Focus(NavigationMethod.Pointer);
                }
            }, DispatcherPriority.Background);
        };
        // Back from the editor: scroll to the photo that was open last, once the gallery is shown and laid out (prompt_10 §5).
        vm.RevealRequested += item => Dispatcher.UIThread.Post(() =>
        {
            Gallery.ScrollIntoView(item);
            Gallery.ContainerFromItem(item)?.Focus(NavigationMethod.Pointer);
        }, DispatcherPriority.Background);
        vm.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName is nameof(BrowseViewModel.DetailsCollapsed) or nameof(BrowseViewModel.HasSelection)) SizePreview(vm);
        };
        SizePreview(vm);
    }

    /// <summary>
    /// The sidebar's photo preview fills the room left by the information and the actions, up to 190 px; folded away or
    /// without a selection its row takes no room (prompt_09 §3).
    /// </summary>
    private void SizePreview(BrowseViewModel vm) => SideGrid.RowDefinitions[1].MaxHeight = vm.HasSelection && !vm.DetailsCollapsed ? 200 : 0;

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
