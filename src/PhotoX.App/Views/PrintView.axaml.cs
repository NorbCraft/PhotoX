using Avalonia.Controls;
using Avalonia.Input;
using PhotoX.App.ViewModels;

namespace PhotoX.App.Views;

public sealed partial class PrintView : UserControl
{
    public PrintView()
    {
        InitializeComponent();
        PanelWidth.Apply(Root.ColumnDefinitions[0], "print-left", 340);
        PanelWidth.Apply(Root.ColumnDefinitions[4], "print-right", 340);
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        // The driver's own settings dialog needs the window as its owner.
        if (DataContext is PrintViewModel vm) vm.OwnerHandle = () => TopLevel.GetTopLevel(this)?.TryGetPlatformHandle()?.Handle ?? IntPtr.Zero;
    }

    private void OnLeftSplitterDragCompleted(object? sender, VectorEventArgs e) => PanelWidth.Save("print-left", Root.ColumnDefinitions[0]);

    private void OnRightSplitterDragCompleted(object? sender, VectorEventArgs e) => PanelWidth.Save("print-right", Root.ColumnDefinitions[4]);
}
