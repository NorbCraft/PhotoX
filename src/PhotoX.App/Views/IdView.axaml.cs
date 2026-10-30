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
        PanelWidth.Apply(Root.ColumnDefinitions[3], AppServices.Current?.Settings.IdPanelWidth ?? 360);
    }

    private void OnSplitterDragCompleted(object? sender, VectorEventArgs e) =>
        PanelWidth.Save(s => s.IdPanelWidth = Root.ColumnDefinitions[3].ActualWidth);

    private IdViewModel? Vm => DataContext as IdViewModel;

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        if (Vm is not { } vm) return;
        vm.PickFolder = PickFolderAsync;
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
