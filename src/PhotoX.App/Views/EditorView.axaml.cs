using Avalonia.Controls;
using Avalonia.Input;
using PhotoX.App.ViewModels;

namespace PhotoX.App.Views;

public sealed partial class EditorView : UserControl
{
    public EditorView()
    {
        InitializeComponent();
        Canvas.CropCommitted += (_, rect) => (DataContext as EditorViewModel)?.CommitCrop(rect);
    }

    private void OnExposureDoubleTapped(object? sender, TappedEventArgs e) => (DataContext as EditorViewModel)?.ResetExposureCommand.Execute(null);
}
