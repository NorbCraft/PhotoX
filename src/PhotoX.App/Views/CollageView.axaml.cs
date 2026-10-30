using Avalonia.Controls;
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
    }

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
