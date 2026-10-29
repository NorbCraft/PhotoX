using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using PhotoX.App.Services;
using PhotoX.App.ViewModels;

namespace PhotoX.App.Views;

public sealed partial class EditorView : UserControl
{
    public EditorView()
    {
        InitializeComponent();
        Canvas.CropCommitted += (_, rect) => (DataContext as EditorViewModel)?.CommitCrop(rect);
        Canvas.CropToggleRequested += (_, _) => (DataContext as EditorViewModel)?.ToggleCrop();
        PanelWidth.Apply(Root.ColumnDefinitions[2], AppServices.Current?.Settings.EditorPanelWidth ?? 340);
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        if (DataContext is not EditorViewModel vm) return;
        vm.PropertyChanged += (_, args) =>
        {
            // Keep the current photo visible in the filmstrip.
            if (args.PropertyName == nameof(EditorViewModel.Current) && vm.Current is { } current && vm.Browse.AllPhotos.IndexOf(current) is >= 0 and var i)
            {
                FilmStrip.ScrollIntoView(i);
            }
        };
    }

    private void OnStraightenDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (sender is Slider slider) slider.Value = 0;
    }

    private void OnSplitterDragCompleted(object? sender, VectorEventArgs e) =>
        PanelWidth.Save(s => s.EditorPanelWidth = Root.ColumnDefinitions[2].ActualWidth);
}
