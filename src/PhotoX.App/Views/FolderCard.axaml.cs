using Avalonia.Controls;
using PhotoX.App.ViewModels;

namespace PhotoX.App.Views;

public sealed partial class FolderCard : UserControl
{
    public FolderCard()
    {
        InitializeComponent();
        DoubleTapped += (_, e) =>
        {
            if (DataContext is not FolderItemViewModel folder) return;
            e.Handled = true;
            folder.Open();
        };
    }
}
