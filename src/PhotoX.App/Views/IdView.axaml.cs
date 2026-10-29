using Avalonia;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
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
    }

    private IdViewModel? Vm => DataContext as IdViewModel;

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        if (Vm is not { } vm) return;
        vm.PickPhoto = PickPhotoAsync;
        vm.PickFolder = PickFolderAsync;
    }

    private async Task<string?> PickPhotoAsync()
    {
        var storage = TopLevel.GetTopLevel(this)?.StorageProvider;
        if (storage is null) return null;
        var files = await storage.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            AllowMultiple = false,
            FileTypeFilter = [new FilePickerFileType("JPEG / PNG") { Patterns = ["*.jpg", "*.jpeg", "*.JPG", "*.JPEG", "*.png", "*.PNG"] }],
        });
        return files.Count > 0 ? files[0].TryGetLocalPath() : null;
    }

    private async Task<string?> PickFolderAsync()
    {
        var storage = TopLevel.GetTopLevel(this)?.StorageProvider;
        if (storage is null) return null;
        var folders = await storage.OpenFolderPickerAsync(new FolderPickerOpenOptions { AllowMultiple = false });
        return folders.Count > 0 ? folders[0].TryGetLocalPath() : null;
    }
}
