using System.ComponentModel;
using Avalonia.Controls;
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
        PanelWidth.Apply(Root.ColumnDefinitions[0], "collage-left", 300, PanelWidth.MinLeft);
        PanelWidth.Apply(Root.ColumnDefinitions[4], "collage-right", 320);
    }

    /// <summary>The photo menu opens only on a frame with a photo (prompt_06 §6).</summary>
    private void OnSlotMenuOpening(object? sender, CancelEventArgs e)
    {
        if (Vm is not { } vm) return;
        var slot = Canvas.ContextSlot >= 0 ? Canvas.ContextSlot : vm.SelectedSlot;
        if (!vm.HasPhotoAt(slot)) e.Cancel = true;
    }

    private void OnLeftSplitterDragCompleted(object? sender, VectorEventArgs e) => PanelWidth.Save("collage-left", Root.ColumnDefinitions[0]);

    private void OnRightSplitterDragCompleted(object? sender, VectorEventArgs e) => PanelWidth.Save("collage-right", Root.ColumnDefinitions[4]);

    private CollageViewModel? Vm => DataContext as CollageViewModel;

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        if (Vm is { } vm) vm.PickFolder = PickFolderAsync;
    }

    private async Task<string?> PickFolderAsync()
    {
        var storage = TopLevel.GetTopLevel(this)?.StorageProvider;
        if (storage is null) return null;
        var folders = await storage.OpenFolderPickerAsync(new FolderPickerOpenOptions { AllowMultiple = false });
        return folders.Count > 0 ? folders[0].TryGetLocalPath() : null;
    }
}
