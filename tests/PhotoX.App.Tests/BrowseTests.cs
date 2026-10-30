using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using PhotoX.App.Services;
using PhotoX.App.ViewModels;
using PhotoX.Storage;
using PhotoX.Tests;

namespace PhotoX.App.Tests;

/// <summary>prompt_03 §2 and §5: recent orders, folder tree, selection, filters and the end of an order.</summary>
public class BrowseTests
{
    [AvaloniaFact]
    public async Task Recent_orders_show_four_and_fold_away()
    {
        using var dir = new TempDir();
        var (window, shell) = UiHelpers.CreateWindow(Path.Combine(dir.Path, "cfg"));
        for (var i = 1; i <= 6; i++)
        {
            var order = Path.Combine(dir.Path, $"Order {i}");
            Directory.CreateDirectory(order);
            await shell.Browse.OpenFolderAsync(order);
        }

        Assert.Equal(["Order 6", "Order 5", "Order 4", "Order 3"], shell.Browse.Explorer.RecentFolders.Select(Path.GetFileName));
        shell.Browse.Explorer.ToggleRecentCommand.Execute(null);
        Assert.True(shell.Browse.Explorer.RecentCollapsed);
        Assert.True(AppServices.Current.Settings.RecentCollapsed); // remembered
        window.Close();
    }

    [AvaloniaFact]
    public async Task Folders_without_subfolders_have_no_arrow_and_a_click_opens_and_expands()
    {
        using var dir = new TempDir();
        var root = Path.Combine(dir.Path, "Comenzi");
        Directory.CreateDirectory(Path.Combine(root, "Popescu", "Prints"));
        Directory.CreateDirectory(Path.Combine(root, "Ionescu"));
        Directory.CreateDirectory(Path.Combine(root, "Ionescu", "Original")); // never shown as part of an order
        Fixtures.WriteJpeg(Path.Combine(root, "Popescu"), "a.jpg", 300, 200);
        var (window, shell) = UiHelpers.CreateWindow(Path.Combine(dir.Path, "cfg"));
        var node = new FolderNodeViewModel(root);
        node.EnsureLoaded();
        var popescu = node.Children.Single(c => c.Name == "Popescu");
        var ionescu = node.Children.Single(c => c.Name == "Ionescu");
        Assert.Single(popescu.Children);   // placeholder: an arrow is shown
        Assert.Empty(ionescu.Children);    // only "Original" inside: no arrow

        shell.Browse.Explorer.FolderRoots.Clear();
        shell.Browse.Explorer.FolderRoots.Add(node);
        node.IsExpanded = true;
        shell.Browse.Explorer.SelectedFolderNode = popescu;
        Assert.True(popescu.IsExpanded);
        Assert.Equal("Prints", Assert.Single(popescu.Children).Name);
        await UiHelpers.WaitUntil(() => shell.Browse.AllPhotos.Count == 1);
        Assert.Equal(Path.Combine(root, "Popescu"), shell.Browse.CurrentFolder);
        UiHelpers.Capture(window, "browse-tree-selected");
        window.Close();
    }

    [AvaloniaFact]
    public async Task Gallery_shows_the_selected_photo_and_filters_keep_the_cards()
    {
        using var dir = new TempDir();
        var order = Path.Combine(dir.Path, "order");
        Directory.CreateDirectory(order);
        for (var i = 1; i <= 4; i++) Fixtures.WriteJpeg(order, $"P{i}.jpg", 300, 200);
        new PhotoSaver().Save(Path.Combine(order, "P2.jpg"), new Core.Editing.EditRecipe { Exposure = 0.5 }, FileStamp.Of(Path.Combine(order, "P2.jpg"))!.Value);
        var (window, shell) = UiHelpers.CreateWindow(Path.Combine(dir.Path, "cfg"));
        await shell.Browse.OpenFolderAsync(order);
        await UiHelpers.WaitUntil(() => true);
        var gallery = window.GetVisualDescendants().OfType<ListBox>().Single(l => l.Name == "Gallery");
        Assert.Equal("P1.jpg", ((PhotoItemViewModel)gallery.SelectedItem!).Name); // the list shows the selection
        var cards = shell.Browse.VisiblePhotos.ToList();

        shell.Browse.SetFilterCommand.Execute(PhotoFilter.ToDo);
        Assert.Equal(["P1.jpg", "P3.jpg", "P4.jpg"], shell.Browse.VisiblePhotos.Select(p => p.Name));
        shell.Browse.SetFilterCommand.Execute(PhotoFilter.All);
        Assert.Equal(cards, shell.Browse.VisiblePhotos);                          // same items, same order
        UiHelpers.Capture(window, "browse-selected");
        window.Close();
    }

    [AvaloniaFact]
    public async Task Main_button_becomes_finish_order_when_every_photo_is_done()
    {
        using var dir = new TempDir();
        var order = Path.Combine(dir.Path, "order");
        Directory.CreateDirectory(order);
        Fixtures.WriteJpeg(order, "P1.jpg", 300, 200);
        var (window, shell) = UiHelpers.CreateWindow(Path.Combine(dir.Path, "cfg"));
        await shell.Browse.OpenFolderAsync(order);
        Assert.False(shell.Browse.IsOrderDone);
        new PhotoSaver().Save(Path.Combine(order, "P1.jpg"), new Core.Editing.EditRecipe { Exposure = 0.5 }, FileStamp.Of(Path.Combine(order, "P1.jpg"))!.Value);
        shell.Browse.RefreshStates();
        Assert.True(shell.Browse.IsOrderDone);

        shell.Browse.PrimaryActionCommand.Execute(null);  // = Finish Order
        await UiHelpers.WaitUntil(() => shell.Dialog is { HasChoices: true });
        UiHelpers.Capture(window, "browse-finish-from-primary");
        shell.Dialog!.PressCommand.Execute("cancel");
        window.Close();
    }
}
