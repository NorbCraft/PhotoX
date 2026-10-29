using Avalonia.Controls;
using Avalonia.Input;
using PhotoX.App.ViewModels;

namespace PhotoX.App.Views;

public sealed partial class BrowseView : UserControl
{
    public BrowseView() => InitializeComponent();

    private void OnPhotoDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (sender is Control { DataContext: PhotoItemViewModel item } && DataContext is BrowseViewModel vm)
        {
            vm.EditPhotoCommand.Execute(item);
        }
    }
}
