using System.ComponentModel;
using Avalonia.Controls;
using Avalonia;
using Avalonia.Input;
using Avalonia.Platform.Storage;
using PhotoX.App.ViewModels;

namespace PhotoX.App.Views;

public sealed partial class CollageView : UserControl
{
    public CollageView()
    {
        InitializeComponent();
        Canvas.SlotSelected += i => Vm?.SelectSlot(i);
        Canvas.Panned += (i, dx, dy) => Vm?.Pan(i, dx, dy);
        Canvas.Zoomed += (i, f) => Vm?.Zoom(i, f);
        Canvas.SlotReset += i => Vm?.ResetSlot(i);
        Canvas.Swapped += (a, b) => Vm?.Swap(a, b);
        Canvas.BoundaryMoved += (column, index, delta) => Vm?.MoveBoundary(column, index, delta);
        Canvas.PhotoDropped += (slot, path) => _ = Vm?.DropAsync(slot, path);
        ApplyLeftWidth();
        ApplyThumbsWidth();
        ApplySettingsWidth();
    }

    private void ApplyLeftWidth() => PanelWidth.Apply(Root.ColumnDefinitions[0], "collage-left", 260, PanelWidth.MinLeft);

    /// <summary>The thumbnail panel: at its narrowest, one column of cards and its button label on two lines.</summary>
    private void ApplyThumbsWidth() => PanelWidth.Apply(Root.ColumnDefinitions[2], "collage-thumbs", 280, 200);

    private void ApplySettingsWidth() => PanelWidth.Apply(Root.ColumnDefinitions[6], "collage-right", 320);

    /// <summary>
    /// A folded panel keeps only its strip (prompt_08 §3): the column shrinks to the strip and its splitter goes; unfolded,
    /// the panel gets its remembered width back.
    /// </summary>
    private static void Fold(ColumnDefinition column, Control splitter, bool folded, Action restore)
    {
        splitter.IsVisible = !folded;
        if (!folded)
        {
            restore();
            return;
        }

        column.MinWidth = 0;
        column.Width = GridLength.Auto;
    }

    private void UpdateFolds()
    {
        if (Vm is not { } vm) return;
        Fold(Root.ColumnDefinitions[2], ThumbsSplitter, vm.ThumbsCollapsed, ApplyThumbsWidth);
        Fold(Root.ColumnDefinitions[6], SettingsSplitter, vm.SettingsCollapsed, ApplySettingsWidth);
    }

    /// <summary>The photo menu opens only on a frame with a photo (prompt_06 §6).</summary>
    private void OnSlotMenuOpening(object? sender, CancelEventArgs e)
    {
        if (Vm is not { } vm) return;
        var slot = Canvas.ContextSlot >= 0 ? Canvas.ContextSlot : vm.SelectedSlot;
        if (!vm.HasPhotoAt(slot)) e.Cancel = true;
    }

    private void OnLeftSplitterDragCompleted(object? sender, VectorEventArgs e) => PanelWidth.Save("collage-left", Root.ColumnDefinitions[0]);

    private void OnThumbsSplitterDragCompleted(object? sender, VectorEventArgs e) => PanelWidth.Save("collage-thumbs", Root.ColumnDefinitions[2]);

    private void OnRightSplitterDragCompleted(object? sender, VectorEventArgs e) => PanelWidth.Save("collage-right", Root.ColumnDefinitions[6]);

    private CollageViewModel? Vm => DataContext as CollageViewModel;

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        if (Vm is not { } vm) return;
        vm.PickFolder = PickFolderAsync;
        vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(CollageViewModel.ThumbsCollapsed) or nameof(CollageViewModel.SettingsCollapsed)) UpdateFolds();
        };
        UpdateFolds();
        PanelWidth.FollowTree(vm.Picker.Explorer, Root.ColumnDefinitions[0], TreePanel, TreeSplitter, ApplyLeftWidth);
    }

    private async Task<string?> PickFolderAsync()
    {
        var storage = TopLevel.GetTopLevel(this)?.StorageProvider;
        if (storage is null) return null;
        var folders = await storage.OpenFolderPickerAsync(new FolderPickerOpenOptions { AllowMultiple = false });
        return folders.Count > 0 ? folders[0].TryGetLocalPath() : null;
    }
}
