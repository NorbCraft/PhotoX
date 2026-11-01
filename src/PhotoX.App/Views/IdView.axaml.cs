using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Platform.Storage;
using PhotoX.App.Services;
using PhotoX.App.ViewModels;

namespace PhotoX.App.Views;

public sealed partial class IdView : UserControl
{
    public IdView()
    {
        InitializeComponent();
        Canvas.Moved += (dx, dy) => Vm?.MoveBy(dx, dy);
        Canvas.Zoomed += f => Vm?.ZoomBy(f);
        Canvas.Rotated += d => Vm?.RotateBy(d);
        ApplyLeftWidth();
        ApplyRightWidth();
        // Before / after shows the photo without adjustments only while the button is held, like Space.
        BeforeButton.PropertyChanged += (_, e) =>
        {
            if (e.Property == Button.IsPressedProperty && Vm is { } vm) vm.ShowBefore = BeforeButton.IsPressed;
        };
    }

    private void ApplyLeftWidth() => PanelWidth.Apply(Root.ColumnDefinitions[0], "id-left", 290, PanelWidth.MinLeft);

    // At least 350 px: Back, Save and Print of the sheet keep their icon and whole label (DEC-076).
    private void ApplyRightWidth() => PanelWidth.Apply(Root.ColumnDefinitions[4], AppServices.Current?.Settings.IdPanelWidth ?? 360, min: 350);

    /// <summary>Step 1 has no editing sidebar: the thumbnails take the whole width right of the tree (prompt_08 §2).</summary>
    private void UpdateRightPanel()
    {
        if (Vm is { } vm) PanelWidth.Show(Root.ColumnDefinitions[4], RightPanel, RightSplitter, !vm.IsSelectStep, ApplyRightWidth);
    }

    private void OnSplitterDragCompleted(object? sender, VectorEventArgs e) =>
        PanelWidth.Save(s => s.IdPanelWidth = Root.ColumnDefinitions[4].ActualWidth);

    private void OnLeftSplitterDragCompleted(object? sender, VectorEventArgs e) => PanelWidth.Save("id-left", Root.ColumnDefinitions[0]);

    private IdViewModel? Vm => DataContext as IdViewModel;

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        if (Vm is not { } vm) return;
        vm.PickFolder = PickFolderAsync;
        vm.SizeChosen += () => FormatButton.Flyout?.Hide();
        vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(IdViewModel.IsSelectStep)) UpdateRightPanel();
        };
        UpdateRightPanel();
        PanelWidth.FollowTree(vm.Picker.Explorer, Root.ColumnDefinitions[0], TreePanel, TreeSplitter, ApplyLeftWidth);
    }

    private void OnZoomDoubleTapped(object? sender, TappedEventArgs e) => Vm?.ResetZoom();

    private void OnRotationDoubleTapped(object? sender, TappedEventArgs e) => Vm?.ResetRotation();

    private async Task<string?> PickFolderAsync()
    {
        var storage = TopLevel.GetTopLevel(this)?.StorageProvider;
        if (storage is null) return null;
        var folders = await storage.OpenFolderPickerAsync(new FolderPickerOpenOptions { AllowMultiple = false });
        return folders.Count > 0 ? folders[0].TryGetLocalPath() : null;
    }
}
