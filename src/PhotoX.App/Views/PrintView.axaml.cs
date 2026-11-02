using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using PhotoX.App.ViewModels;

namespace PhotoX.App.Views;

public sealed partial class PrintView : UserControl
{
    public PrintView()
    {
        InitializeComponent();
        PanelWidth.Apply(Root.ColumnDefinitions[0], "print-left", 340);
        PanelWidth.Apply(Root.ColumnDefinitions[4], "print-right", 340);
        // The selected pages get the Copies control on the right (prompt_10 §6).
        PreviewList.SelectionChanged += (_, _) =>
        {
            if (DataContext is PrintViewModel vm) vm.SetSelection(PreviewList.SelectedItems?.OfType<PrintPreviewItem>() ?? []);
        };
    }

    private void OnSelectAllClick(object? sender, RoutedEventArgs e)
    {
        PreviewList.SelectAll();
        PreviewList.Focus(NavigationMethod.Tab);
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
