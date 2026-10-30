using Avalonia.Controls;
using PhotoX.App.ViewModels;

namespace PhotoX.App.Views;

public sealed partial class PrintDialogView : UserControl
{
    public PrintDialogView() => InitializeComponent();

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        // The driver's own settings dialog needs the window as its owner.
        if (DataContext is PrintDialogViewModel vm) vm.OwnerHandle = () => TopLevel.GetTopLevel(this)?.TryGetPlatformHandle()?.Handle ?? IntPtr.Zero;
    }
}
