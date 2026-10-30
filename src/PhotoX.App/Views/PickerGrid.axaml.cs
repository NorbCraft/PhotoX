using Avalonia.Controls;
using Avalonia.Input;
using PhotoX.App.ViewModels;

namespace PhotoX.App.Views;

public sealed partial class PickerGrid : UserControl
{
    public PickerGrid()
    {
        InitializeComponent();
        Grid.KeyDown += (_, e) =>
        {
            if (e.Key != Key.Enter || DataContext is not PhotoPickerViewModel vm) return;
            vm.Pick(null);
            e.Handled = true;
        };
    }

    private void OnPhotoDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (sender is Control { DataContext: PickerPhoto photo } && DataContext is PhotoPickerViewModel vm) vm.Pick(photo);
    }
}
