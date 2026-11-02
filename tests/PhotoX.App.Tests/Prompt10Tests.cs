using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using PhotoX.App.Services;
using PhotoX.App.ViewModels;
using PhotoX.App.Views;
using PhotoX.Storage;
using PhotoX.Tests;

namespace PhotoX.App.Tests;

/// <summary>
/// prompt_10 through the UI: the keyboard workflow of the editor (Page Up / Page Down, the arrow keys move the crop,
/// Enter saves and goes on), Previous and Next, the crop label, Before/After with Space, the Delete dialog's keys, the
/// gallery's place after deleting and after editing, ↑ / ↓ in the grid, the thumbnail size buttons, Escape in ID Photos
/// and copies in the print preview (TS-25).
/// </summary>
public class Prompt10Tests
{
    private static string Order(TempDir dir, int count, int width = 1000, int height = 600)
    {
        var order = Path.Combine(dir.Path, "order");
        Directory.CreateDirectory(order);
        for (var i = 1; i <= count; i++) Fixtures.WriteJpeg(order, $"IMG_{i:000}.jpg", width, height);
        return order;
    }

    private static T Find<T>(Visual root, string name)
        where T : Control => root.GetVisualDescendants().OfType<T>().First(c => c.Name == name);

    private static void Click(Window window, Control control, int clicks = 1)
    {
        var p = control.TranslatePoint(new Point(control.Bounds.Width / 2, control.Bounds.Height / 2), window)!.Value;
        for (var i = 0; i < clicks; i++)
        {
            window.MouseDown(p, MouseButton.Left);
            window.MouseUp(p, MouseButton.Left);
        }

        Dispatcher.UIThread.RunJobs();
    }

    private static object? Focused(Window window) => window.FocusManager?.GetFocusedElement();

    private static double GalleryOffset(Window window) => Find<ListBox>(window, "Gallery").GetVisualDescendants().OfType<ScrollViewer>().First().Offset.Y;

    [AvaloniaFact]
    public async Task The_editor_works_from_the_keyboard_arrows_move_the_crop_and_enter_saves_and_goes_on()
    {
        using var dir = new TempDir();
        var order = Order(dir, 4);                                                        // 1000 × 600: a 10×15 crop can move sideways only
        var (window, shell) = UiHelpers.CreateWindow(Path.Combine(dir.Path, "cfg"), 1366, 768);
        await shell.Browse.OpenFolderAsync(order);
        shell.Browse.StartEditingCommand.Execute(null);
        var editor = shell.Editor;
        await UiHelpers.WaitUntil(() => editor is { Preview: not null, IsLoading: false });
        var start = editor.Recipe.Crop;
        Assert.True(editor.ShowCrop);

        UiHelpers.Tap(window, PhysicalKey.ArrowRight);                                   // → moves the crop, not to the next photo
        Assert.Equal("IMG_001.jpg", editor.Current!.Name);
        Assert.True(editor.Recipe.Crop.X > start.X);
        Assert.Equal((start.Width, start.Height, start.Y), (editor.Recipe.Crop.Width, editor.Recipe.Crop.Height, editor.Recipe.Crop.Y));
        var moved = editor.Recipe.Crop;
        UiHelpers.Tap(window, PhysicalKey.ArrowUp);                                      // no room above or below: nothing moves
        UiHelpers.Tap(window, PhysicalKey.ArrowDown);
        Assert.Equal(moved, editor.Recipe.Crop);
        for (var i = 0; i < 200; i++) editor.MoveCrop(1, 0);                              // held →: stops at the edge of the photo
        Assert.Equal(1, editor.Recipe.Crop.X + editor.Recipe.Crop.Width, 6);
        Assert.False(editor.MoveCrop(1, 0));
        UiHelpers.Tap(window, PhysicalKey.ArrowLeft);
        Assert.True(editor.Recipe.Crop.X + editor.Recipe.Crop.Width < 1);
        editor.Undo();                                                                    // a burst of key presses is one undo step
        Assert.Equal(start, editor.Recipe.Crop);
        editor.Redo();
        var saved = editor.Recipe.Crop;
        Assert.True(saved.X > start.X);

        // The crop label: size in cm and ppi, no pixels.
        Assert.Matches(@"^10 × 15 cm  ·  \d+ ppi$", editor.CropInfo);
        UiHelpers.Capture(window, "p10-editor");

        UiHelpers.Tap(window, PhysicalKey.Enter);                                         // Enter: save with this crop, then the next photo
        await UiHelpers.WaitUntil(() => editor.Current!.Name == "IMG_002.jpg" && !editor.IsBusy);
        var record = new OrderStore(order).Load().Find("IMG_001.jpg")!;
        Assert.Equal(saved, record.Recipe!.Crop);

        UiHelpers.Tap(window, PhysicalKey.PageDown);                                      // Page Down / Page Up change the photo
        await UiHelpers.WaitUntil(() => editor.Current!.Name == "IMG_003.jpg" && !editor.IsBusy);
        UiHelpers.Tap(window, PhysicalKey.PageUp);
        await UiHelpers.WaitUntil(() => editor.Current!.Name == "IMG_002.jpg" && !editor.IsBusy);
        Click(window, Find<Button>(window, "NextButton"));                                // and the Previous / Next buttons
        await UiHelpers.WaitUntil(() => editor.Current!.Name == "IMG_003.jpg" && !editor.IsBusy);
        Click(window, Find<Button>(window, "NextButton"));
        await UiHelpers.WaitUntil(() => editor.Current!.Name == "IMG_004.jpg" && !editor.IsBusy);
        Click(window, Find<Button>(window, "PreviousButton"));
        await UiHelpers.WaitUntil(() => editor.Current!.Name == "IMG_003.jpg" && !editor.IsBusy);

        UiHelpers.Tap(window, PhysicalKey.Escape);                                        // Escape: the gallery, on the photo open last
        Assert.True(shell.IsBrowsing);
        Assert.Equal("IMG_003.jpg", shell.Browse.SelectedPhoto?.Name);
        window.Close();
    }

    [AvaloniaFact]
    public async Task Back_from_the_editor_the_gallery_selects_and_shows_the_photo_open_last()
    {
        using var dir = new TempDir();
        var (window, shell) = UiHelpers.CreateWindow(Path.Combine(dir.Path, "cfg"), 1366, 768);
        var browse = shell.Browse;
        await browse.OpenFolderAsync(Order(dir, 40, 300, 200));
        Assert.Equal(0, GalleryOffset(window));
        shell.Browse.StartEditingCommand.Execute(null);                                   // photo 1 …
        await UiHelpers.WaitUntil(() => shell.Editor is { Preview: not null, IsLoading: false });
        await shell.Editor.OpenItemAsync(browse.AllPhotos[35]);                            // … then far down the folder
        UiHelpers.Tap(window, PhysicalKey.PageDown);
        await UiHelpers.WaitUntil(() => shell.Editor.Current!.Name == "IMG_037.jpg" && !shell.Editor.IsBusy);

        shell.Editor.BackToFolderCommand.Execute(null);
        await UiHelpers.WaitUntil(() => GalleryOffset(window) > 0);
        Assert.Equal("IMG_037.jpg", browse.SelectedPhoto?.Name);
        var gallery = Find<ListBox>(window, "Gallery");
        var card = gallery.ContainerFromItem(browse.SelectedPhoto!)!;
        var top = card.TranslatePoint(default, gallery)!.Value.Y;
        Assert.InRange(top, 0, gallery.Bounds.Height - card.Bounds.Height);                // in view
        window.Close();
    }

    [AvaloniaFact]
    public async Task Before_after_looks_pressed_while_space_is_held_as_with_the_mouse()
    {
        using var dir = new TempDir();
        var (window, shell) = UiHelpers.CreateWindow(Path.Combine(dir.Path, "cfg"), 1366, 768);
        await shell.Browse.OpenFolderAsync(Order(dir, 1));
        shell.Browse.StartEditingCommand.Execute(null);
        await UiHelpers.WaitUntil(() => shell.Editor is { Preview: not null, IsLoading: false });
        var button = Find<Button>(window.GetVisualDescendants().OfType<EditorView>().Single(), "BeforeButton");

        window.KeyPressQwerty(PhysicalKey.Space, RawInputModifiers.None);
        Dispatcher.UIThread.RunJobs();
        Assert.True(shell.Editor.ShowBefore);
        Assert.Contains("active", button.Classes);                                         // pressed look, as with the mouse
        UiHelpers.Capture(window, "p10-before-space");
        window.KeyReleaseQwerty(PhysicalKey.Space, RawInputModifiers.None);
        Dispatcher.UIThread.RunJobs();
        Assert.False(shell.Editor.ShowBefore);
        Assert.DoesNotContain("active", button.Classes);

        var p = button.TranslatePoint(new Point(button.Bounds.Width / 2, button.Bounds.Height / 2), window)!.Value;
        window.MouseDown(p, MouseButton.Left);                                            // held with the mouse: the same state
        Dispatcher.UIThread.RunJobs();
        Assert.True(shell.Editor.ShowBefore);
        Assert.Contains("active", button.Classes);
        window.MouseUp(p, MouseButton.Left);
        Dispatcher.UIThread.RunJobs();
        Assert.False(shell.Editor.ShowBefore);
        window.Close();
    }

    [AvaloniaFact]
    public async Task The_delete_dialog_owns_the_keyboard_and_the_gallery_keeps_its_place()
    {
        using var dir = new TempDir();
        var order = Order(dir, 40, 300, 200);
        var (window, shell) = UiHelpers.CreateWindow(Path.Combine(dir.Path, "cfg"), 1366, 768);
        var browse = shell.Browse;
        await browse.OpenFolderAsync(order);
        await UiHelpers.WaitUntil(() => browse.AllPhotos.All(p => p.Thumbnail is not null));
        var gallery = Find<ListBox>(window, "Gallery");
        gallery.SelectedItem = browse.AllPhotos[29];                                       // far down the folder
        gallery.ScrollIntoView(29);
        Dispatcher.UIThread.RunJobs();
        gallery.ContainerFromIndex(29)!.Focus(NavigationMethod.Tab);
        var offset = GalleryOffset(window);
        Assert.True(offset > 200);

        UiHelpers.Tap(window, PhysicalKey.Delete);
        await UiHelpers.WaitUntil(() => shell.Dialog is not null && Focused(window) is Button);
        Assert.Equal("cancel", ((Button)Focused(window)!).CommandParameter);              // Cancel is the safe default
        UiHelpers.Tap(window, PhysicalKey.ArrowRight);
        Assert.Equal("delete", ((Button)Focused(window)!).CommandParameter);
        UiHelpers.Tap(window, PhysicalKey.ArrowLeft);
        UiHelpers.Tap(window, PhysicalKey.ArrowDown);                                     // the gallery behind does not move
        Assert.Equal("cancel", ((Button)Focused(window)!).CommandParameter);
        Assert.Equal("IMG_030.jpg", browse.SelectedPhoto?.Name);
        UiHelpers.Capture(window, "p10-delete-dialog");
        UiHelpers.Tap(window, PhysicalKey.Escape);                                        // Escape cancels
        await UiHelpers.WaitUntil(() => shell.Dialog is null);
        Assert.Equal(40, browse.AllPhotos.Count);
        Assert.Equal(40, Directory.GetFiles(order).Length);

        UiHelpers.Tap(window, PhysicalKey.Delete);                                        // → then Enter deletes
        await UiHelpers.WaitUntil(() => shell.Dialog is not null && Focused(window) is Button);
        UiHelpers.Tap(window, PhysicalKey.ArrowRight);
        UiHelpers.Tap(window, PhysicalKey.Enter);
        await UiHelpers.WaitUntil(() => shell.Dialog is null && browse.AllPhotos.Count == 39);
        Dispatcher.UIThread.RunJobs();
        Assert.Equal("IMG_031.jpg", browse.SelectedPhoto?.Name);                            // the photo after it, in view
        Assert.InRange(GalleryOffset(window), offset - 250, offset + 250);                  // no jump to the top
        Assert.True(File.Exists(Path.Combine(order, "Original", "IMG_030 (deleted).jpg")));
        window.Close();
    }

    [AvaloniaFact]
    public async Task Up_and_down_follow_the_grid_and_the_size_buttons_change_the_columns()
    {
        using var dir = new TempDir();
        var (window, shell) = UiHelpers.CreateWindow(Path.Combine(dir.Path, "cfg"), 1366, 768);
        var browse = shell.Browse;
        await browse.OpenFolderAsync(Order(dir, 30, 300, 200));
        var gallery = Find<ListBox>(window, "Gallery");
        Dispatcher.UIThread.RunJobs();
        var width = gallery.ItemsPanelRoot!.Bounds.Width;
        var columns = browse.ColumnsIn(width);
        Assert.True(columns >= 3);

        Click(window, gallery.ContainerFromIndex(1)!);                                     // the second photo of the first row
        UiHelpers.Tap(window, PhysicalKey.ArrowDown);
        Assert.Equal(browse.AllPhotos[1 + columns], browse.SelectedPhoto);                   // the one below it
        UiHelpers.Tap(window, PhysicalKey.ArrowRight);
        Assert.Equal(browse.AllPhotos[2 + columns], browse.SelectedPhoto);
        UiHelpers.Tap(window, PhysicalKey.ArrowUp);
        UiHelpers.Tap(window, PhysicalKey.ArrowUp);                                       // the first row: stays
        Assert.Equal(browse.AllPhotos[2], browse.SelectedPhoto);

        Click(window, Find<Button>(window, "ThumbSmallerButton"));                         // − : one more per row
        Dispatcher.UIThread.RunJobs();
        Assert.Equal(columns + 1, browse.ColumnsIn(width));
        UiHelpers.Tap(window, PhysicalKey.ArrowDown);                                     // ↓ follows the new grid
        Assert.Equal(browse.AllPhotos[2 + columns + 1], browse.SelectedPhoto);
        Click(window, Find<Button>(window, "ThumbLargerButton"));                          // + : one fewer
        Click(window, Find<Button>(window, "ThumbLargerButton"));
        Dispatcher.UIThread.RunJobs();
        Assert.Equal(columns - 1, browse.ColumnsIn(width));
        for (var i = 0; i < 20; i++) browse.ChangeColumns(-1, width);                     // never past the slider's limits
        Assert.Equal(BrowseViewModel.MaxThumbScale, browse.ThumbScale);
        for (var i = 0; i < 40; i++) browse.ChangeColumns(+1, width);
        Assert.Equal(BrowseViewModel.MinThumbScale, browse.ThumbScale);
        UiHelpers.Capture(window, "p10-gallery-small");
        window.Close();
    }

    [AvaloniaFact]
    public void Grid_navigation_picks_the_nearest_card_in_the_next_row()
    {
        // Four columns of 100 × 100 with gaps, the last row shorter.
        var cards = Enumerable.Range(0, 10).Select(i => new Rect(i % 4 * 110, i / 4 * 110, 100, 100)).ToList();
        Assert.Equal(5, GridNavigation.Neighbour(cards, 1, 1));
        Assert.Equal(9, GridNavigation.Neighbour(cards, 7, 1));                            // the last row is shorter: the nearest one
        Assert.Equal(-1, GridNavigation.Neighbour(cards, 9, 1));
        Assert.Equal(1, GridNavigation.Neighbour(cards, 9, -2));
        Assert.Equal(-1, GridNavigation.Neighbour(cards, 2, -1));
    }

    [AvaloniaFact]
    public async Task Escape_in_id_photos_clears_the_photo_but_first_closes_a_dialog()
    {
        using var dir = new TempDir();
        var order = Order(dir, 2, 800, 1200);
        var hashes = Directory.GetFiles(order).Order().Select(Fixtures.Sha).ToList();
        var (window, shell) = UiHelpers.CreateWindow(Path.Combine(dir.Path, "cfg"), 1366, 768);
        await shell.Browse.OpenFolderAsync(order);
        shell.ShowIdCommand.Execute(null);
        var id = shell.Id;
        await id.LoadSourceAsync(Path.Combine(order, "IMG_001.jpg"));
        Assert.True(id.HasSource);

        var dialog = new DialogViewModel("Test", "A dialog over ID Photos");
        dialog.Buttons.Add(new DialogButton("ok", "OK", true));
        var shown = shell.ShowDialogAsync(dialog);
        UiHelpers.Tap(window, PhysicalKey.Escape);                                        // the dialog first …
        Assert.Null(await shown);
        Assert.True(id.HasSource);

        UiHelpers.Tap(window, PhysicalKey.Escape);                                        // … then Clear
        await UiHelpers.WaitUntil(() => !id.HasSource);
        Assert.True(id.IsSelectStep);
        Assert.Null(id.Framing);
        Assert.Equal(hashes, Directory.GetFiles(order).Order().Select(Fixtures.Sha).ToList());
        window.Close();
    }

    [AvaloniaFact]
    public async Task Every_photo_shows_in_the_print_preview_and_its_copies_change_there()
    {
        using var dir = new TempDir();
        var order = Order(dir, 30, 300, 200);
        var hashes = Directory.GetFiles(order).Order().Select(Fixtures.Sha).ToList();
        var (window, shell) = UiHelpers.CreateWindow(Path.Combine(dir.Path, "cfg"), 1366, 768);
        var browse = shell.Browse;
        await browse.OpenFolderAsync(order);
        browse.SetQuantity([browse.AllPhotos[29]], 0);                                    // left out before Print: not listed

        browse.PrintAllCommand.Execute(null);
        var print = shell.Print;
        Assert.Equal((29, 29, 29), (print.Previews.Count, print.PhotoCount, print.TotalCopies));   // all of them, as pages
        await UiHelpers.WaitUntil(() => print.Previews.All(e => e.Page is not null), 60000);
        var list = Find<ListBox>(window, "PreviewList");
        Assert.False(print.HasCopiesTargets);

        list.SelectedItems!.Add(print.Previews[0]);                                        // one page: + twice
        Click(window, Find<Button>(window, "PageCopiesPlus"));
        Click(window, Find<Button>(window, "PageCopiesPlus"));
        Assert.Equal(("3", 31), (print.CopiesValueText, print.TotalCopies));
        Assert.Equal(3, browse.AllPhotos[0].Quantity);                                    // the same quantity as in Photos
        foreach (var e in print.Previews.Skip(1).Take(4)) list.SelectedItems.Add(e);       // five pages, mixed: 3 and 1
        Assert.Equal("Mixed (1–3) · for 5 photos", print.CopiesNote);
        print.SetSelectedCopies(0);                                                       // 0 leaves them out but keeps them listed
        Assert.Equal((29, 24, 24), (print.Previews.Count, print.PhotoCount, print.TotalCopies));
        Assert.True(print.Previews[0].IsExcluded);
        Assert.Equal(0, browse.AllPhotos[4].Quantity);
        UiHelpers.Capture(window, "p10-print-copies");
        Click(window, Find<Button>(window, "PageCopiesPlus"));                            // and back
        Assert.Equal((29, 29), (print.PhotoCount, print.TotalCopies));

        list.SelectedItems.Clear();
        list.SelectAll();                                                                 // Select all: 2 copies of everything
        Assert.Equal(29, list.SelectedItems.Count);
        list.Focus();
        UiHelpers.Tap(window, PhysicalKey.Digit2);
        Assert.Equal((29, 58), (print.PhotoCount, print.TotalCopies));
        Assert.Equal("29 photos · 58 copies on 10 × 15 cm", print.SummaryText);

        print.PrintCommand.Execute(null);
        await UiHelpers.WaitUntil(() => UiHelpers.Printer.Submitted.Count == 29, 60000);
        Assert.Equal(58, UiHelpers.Printer.Submitted.Sum(s => s.Copies));                  // no global multiplier
        Assert.Equal(0, browse.AllPhotos[29].Quantity);
        Assert.Equal(hashes, Directory.GetFiles(order).Order().Select(Fixtures.Sha).ToList());
        window.Close();
    }
}
