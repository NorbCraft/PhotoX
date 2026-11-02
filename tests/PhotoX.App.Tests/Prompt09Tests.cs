using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Presenters;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using PhotoX.App.Controls;
using PhotoX.App.ViewModels;
using PhotoX.App.Views;
using PhotoX.Core.Formats;
using PhotoX.Printing;
using PhotoX.Tests;
using Path = System.IO.Path;

namespace PhotoX.App.Tests;

/// <summary>
/// prompt_09 through the UI: requested copies per photo (thumbnails, sidebar, several photos at once, keys,
/// persistence), Print all and Print selected with those copies, the print preview, ID step 1 clicks, the collage
/// hint, the workspace highlight and the folder tree button (TS-24).
/// </summary>
public class Prompt09Tests(ITestOutputHelper output)
{
    private static string Order(TempDir dir, int count, string prefix = "IMG_", int width = 900, int height = 600)
    {
        var order = Path.Combine(dir.Path, "order");
        Directory.CreateDirectory(order);
        for (var i = 1; i <= count; i++) Fixtures.WriteJpeg(order, $"{prefix}{i:000}.jpg", width, height);
        return order;
    }

    private static T Find<T>(Visual root, string name)
        where T : Control => root.GetVisualDescendants().OfType<T>().First(c => c.Name == name);

    /// <summary>A real mouse click (or double-click) in the middle of <paramref name="control"/>.</summary>
    private static void Click(Window window, Control control, int clicks = 1)
    {
        var p = control.TranslatePoint(new Point(control.Bounds.Width / 2, control.Bounds.Height / 2), window)!.Value;
        for (var i = 0; i < clicks; i++)
        {
            window.MouseDown(p, MouseButton.Left);
            window.MouseUp(p, MouseButton.Left);
        }

        window.MouseMove(new Point(window.Width / 2, window.Height - 4));                 // away again, no hover
        Dispatcher.UIThread.RunJobs();
    }

    private static Color Resource(Window window, string key) =>
        window.TryFindResource(key, window.ActualThemeVariant, out var value) && value is ISolidColorBrush brush ? brush.Color : throw new KeyNotFoundException(key);

    private static Border QuantityBadge(Window window, PhotoItemViewModel photo) =>
        window.GetVisualDescendants().OfType<Border>().First(b => b.Classes.Contains("quantity") && b.DataContext == photo);

    [AvaloniaFact]
    public async Task Copies_are_set_per_photo_or_for_the_selection_and_shown_on_the_thumbnails()
    {
        using var dir = new TempDir();
        var order = Order(dir, 5);
        var before = Directory.GetFileSystemEntries(order).Order().ToList();
        var cfg = Path.Combine(dir.Path, "cfg");
        var (window, shell) = UiHelpers.CreateWindow(cfg, 1366, 768);
        var browse = shell.Browse;
        await browse.OpenFolderAsync(order);
        await UiHelpers.WaitUntil(() => browse.AllPhotos.All(p => p.Thumbnail is not null));
        var photos = browse.AllPhotos.ToList();
        var gallery = Find<ListBox>(window, "Gallery");

        Assert.All(photos, p => Assert.Equal((1, "×1"), (p.Quantity, p.QuantityText)));   // one copy each by default
        Assert.Equal("Selected for print: 5", browse.SelectedForPrintText);
        Assert.Equal("Printed: 0 copies", browse.PrintedText);
        Assert.Equal("×1", ((TextBlock)QuantityBadge(window, photos[0]).Child!).Text);
        Assert.Equal(Avalonia.Layout.HorizontalAlignment.Left, QuantityBadge(window, photos[0]).HorizontalAlignment);   // lower left

        // One photo: + and − in the sidebar, down to 0 (left out), never below.
        gallery.SelectedItem = photos[0];
        Assert.Equal("1", browse.QuantityValueText);
        Click(window, Find<Button>(window, "QuantityPlus"));
        Assert.Equal((2, "×2"), (photos[0].Quantity, ((TextBlock)QuantityBadge(window, photos[0]).Child!).Text));
        Assert.Equal("2", Find<TextBlock>(window, "QuantityValue").Text);
        for (var i = 0; i < 3; i++) Click(window, Find<Button>(window, "QuantityMinus"));
        Assert.Equal(0, photos[0].Quantity);
        Assert.Contains("excluded", QuantityBadge(window, photos[0]).Classes);
        Assert.Equal("Selected for print: 4", browse.SelectedForPrintText);                  // photos, not copies

        // Several photos (Ctrl/Shift + click): one value for all of them.
        gallery.SelectedItems!.Clear();
        foreach (var p in photos.Skip(1).Take(3)) gallery.SelectedItems.Add(p);
        Assert.Equal(3, browse.SelectionCount);
        Assert.Equal(("1", "for 3 photos"), (browse.QuantityValueText, browse.QuantityNote));
        browse.QuantityPlusCommand.Execute(null);
        Assert.Equal([0, 2, 2, 2, 1], photos.Select(p => p.Quantity));

        // Different values: shown as mixed; + starts from the lowest and sets all of them alike.
        gallery.SelectedItems.Clear();
        gallery.SelectedItems.Add(photos[0]);
        gallery.SelectedItems.Add(photos[1]);
        Assert.True(browse.IsQuantityMixed);
        Assert.Equal(("–", "Mixed (0–2) · for 2 photos"), (browse.QuantityValueText, browse.QuantityNote));
        UiHelpers.Capture(window, "p09-sidebar-mixed");
        browse.QuantityPlusCommand.Execute(null);
        Assert.Equal([1, 1], photos.Take(2).Select(p => p.Quantity));
        Assert.False(browse.IsQuantityMixed);

        // Select all, 0 for all, then only the wanted photos back to 1 or more (the keys 0–9, + and −).
        gallery.SelectAll();
        Assert.Equal(5, browse.SelectionCount);
        UiHelpers.Tap(window, PhysicalKey.Digit0);
        Assert.All(photos, p => Assert.Equal(0, p.Quantity));
        Assert.Equal("Selected for print: 0", browse.SelectedForPrintText);
        gallery.SelectedItems!.Clear();
        gallery.SelectedItems.Add(photos[2]);
        UiHelpers.Tap(window, PhysicalKey.Digit3);
        UiHelpers.Tap(window, PhysicalKey.NumPadAdd);
        gallery.SelectedItems.Clear();
        gallery.SelectedItems.Add(photos[4]);
        UiHelpers.Tap(window, PhysicalKey.Digit2);
        UiHelpers.Tap(window, PhysicalKey.Minus);
        Assert.Equal([0, 0, 4, 0, 1], photos.Select(p => p.Quantity));
        Assert.Equal("Selected for print: 2", browse.SelectedForPrintText);
        UiHelpers.Capture(window, "p09-photos-quantities");
        App.ApplyTheme("dark");
        UiHelpers.Capture(window, "p09-photos-quantities-dark");
        App.ApplyTheme("light");

        // Nothing is written to the customer's folder, and the quantities come back after a restart.
        Assert.Equal(before, Directory.GetFileSystemEntries(order).Order().ToList());
        window.Close();
        var (window2, shell2) = UiHelpers.CreateWindow(cfg, 1366, 768);
        await shell2.Browse.OpenFolderAsync(order);
        Assert.Equal([0, 0, 4, 0, 1], shell2.Browse.AllPhotos.Select(p => p.Quantity));
        Assert.Equal("Selected for print: 2", shell2.Browse.SelectedForPrintText);
        window2.Close();
    }

    [AvaloniaFact]
    public async Task Print_all_and_print_selected_send_each_photo_with_its_own_copies()
    {
        using var dir = new TempDir();
        var order = Order(dir, 5);
        var hashes = Directory.GetFiles(order).Order().Select(Fixtures.Sha).ToList();
        var (window, shell) = UiHelpers.CreateWindow(Path.Combine(dir.Path, "cfg"), 1366, 768);
        var browse = shell.Browse;
        await browse.OpenFolderAsync(order);
        var p = browse.AllPhotos.ToList();
        browse.SetQuantity([p[1]], 3);                                                    // the example of prompt_09 §5
        browse.SetQuantity([p[2]], 0);
        browse.SetQuantity([p[3]], 2);

        browse.PrintAllCommand.Execute(null);
        var print = shell.Print;
        Assert.True(shell.IsPrintWorkspace);
        Assert.Equal((4, 7), (print.PhotoCount, print.TotalCopies));
        Assert.Equal("4 photos · 7 copies on 10 × 15 cm", print.SummaryText);
        Assert.Equal(["IMG_001.jpg ×1", "IMG_002.jpg ×3", "IMG_004.jpg ×2", "IMG_005.jpg ×1"], print.Previews.Select(e => $"{e.Name} {e.CopiesText}"));
        await UiHelpers.WaitUntil(() => print.Previews.All(e => e.Page is not null));
        Assert.Equal(4, window.GetVisualDescendants().OfType<PrintView>().Single().GetVisualDescendants().OfType<Border>().Count(b => b.Classes.Contains("quantity") && b.IsEffectivelyVisible));
        UiHelpers.Capture(window, "p09-print-preview");

        print.SetSizeCommand.Execute(PrintFormats.P15x20);                                // the quantities stay with another size
        Assert.Equal((4, 7), (print.PhotoCount, print.TotalCopies));
        print.SetSizeCommand.Execute(PrintFormats.P10x15);

        // Back in Photos, a quantity changes while the photos wait: Printing follows, without a second Print.
        print.BackCommand.Execute(null);
        browse.SetQuantity([p[2]], 1);
        shell.ShowPrintingCommand.Execute(null);
        Assert.Equal((5, 8), (print.PhotoCount, print.TotalCopies));
        browse.SetQuantity([p[2]], 0);
        browse.SetQuantity([p[0]], 2);
        Assert.Equal((4, 8), (print.PhotoCount, print.TotalCopies));
        Assert.Equal("×2", print.Previews[0].CopiesText);
        browse.SetQuantity([p[0]], 1);

        print.PrintCommand.Execute(null);
        await UiHelpers.WaitUntil(() => UiHelpers.Printer.Submitted.Count == 4);
        Assert.Equal([1, 3, 2, 1], UiHelpers.Printer.Submitted.OrderBy(s => s.DocumentName).Select(s => s.Copies));
        Assert.Equal(7, UiHelpers.Printer.Submitted.Sum(s => s.Copies));                   // no second, global multiplier
        await UiHelpers.WaitUntil(() => browse.PrintedCount == 7);
        Assert.Equal([1, 3, 0, 2, 1], p.Select(x => x.PrintCount));                        // printed counters …
        Assert.Equal([1, 3, 0, 2, 1], p.Select(x => x.Quantity));                          // … and the requested copies stay as they were
        Assert.Equal("Printed: 7 copies", browse.PrintedText);

        // Print selected: only the selected photos, each with its copies; a photo at 0 is left out.
        print.BackCommand.Execute(null);
        browse.SetSelection([p[1], p[2]]);
        browse.PrintSelectedCommand.Execute(null);
        Assert.Equal(["IMG_002.jpg ×3"], print.Previews.Select(e => $"{e.Name} {e.CopiesText}"));
        print.ClearCommand.Execute(null);

        // Only photos at 0: nothing opens, and the operator is told why.
        print.BackCommand.Execute(null);
        browse.SetSelection([p[2]]);
        browse.PrintSelectedCommand.Execute(null);
        await UiHelpers.WaitUntil(() => shell.Dialog is not null);
        Assert.Equal("Nothing to print", shell.Dialog!.Title);
        Assert.True(shell.IsBrowsing);
        shell.Dialog.PressCommand.Execute("ok");
        Assert.Equal(hashes, Directory.GetFiles(order).Order().Select(Fixtures.Sha).ToList());
        window.Close();
    }

    [AvaloniaFact]
    public async Task Every_photo_is_listed_with_its_copies_and_hundreds_update_at_once()
    {
        using var dir = new TempDir();
        var order = Order(dir, 300, width: 60, height: 40);
        var (window, shell) = UiHelpers.CreateWindow(Path.Combine(dir.Path, "cfg"), 1366, 768);
        var browse = shell.Browse;
        await browse.OpenFolderAsync(order);
        var gallery = Find<ListBox>(window, "Gallery");
        await UiHelpers.WaitUntil(() => browse.AllPhotos.All(p => p.Thumbnail is not null), 60000);

        var sw = Stopwatch.StartNew();
        gallery.SelectAll();                                                              // Ctrl + A
        Dispatcher.UIThread.RunJobs();
        var selectAll = sw.ElapsedMilliseconds;
        sw.Restart();
        browse.SetSelectedQuantity(0);                                                    // all 300 left out …
        Dispatcher.UIThread.RunJobs();
        var allToZero = sw.ElapsedMilliseconds;
        gallery.SelectedItems!.Clear();
        foreach (var photo in browse.AllPhotos.Take(25)) gallery.SelectedItems.Add(photo);
        sw.Restart();
        browse.SetSelectedQuantity(2);                                                    // … then 25 of them at 2 copies
        Dispatcher.UIThread.RunJobs();
        output.WriteLine($"Select all 300: {selectAll} ms; 300 photos to 0: {allToZero} ms; 25 photos to 2: {sw.ElapsedMilliseconds} ms");
        Assert.InRange(allToZero, 0, 1500);                                               // generous for slow CI machines
        Assert.Equal(25, browse.SelectedForPrintCount);

        browse.PrintAllCommand.Execute(null);
        var print = shell.Print;
        Assert.Equal((25, 50), (print.PhotoCount, print.TotalCopies));
        Assert.Equal(25, print.Previews.Count);                                           // every photo as a page (prompt_10 §6)
        Assert.All(print.Previews, e => Assert.Equal("×2", e.CopiesText));
        window.Close();
    }

    [AvaloniaFact]
    public async Task Id_photos_select_with_a_click_and_open_with_a_double_click()
    {
        using var dir = new TempDir();
        var order = Order(dir, 3, "P", 800, 1200);
        var (window, shell) = UiHelpers.CreateWindow(Path.Combine(dir.Path, "cfg"), 1366, 768);
        await shell.Browse.OpenFolderAsync(order);
        shell.ShowIdCommand.Execute(null);
        var id = shell.Id;
        await UiHelpers.WaitUntil(() => id.Picker.Photos.Count == 3 && id.Picker.Photos.All(p => p.Thumbnail is not null));
        var grid = Find<PickerGrid>(window, "SelectGrid");
        Border Card(PickerPhoto photo) => grid.GetVisualDescendants().OfType<Border>().First(b => b.Classes.Contains("card") && b.DataContext == photo);

        // No permanent hint above the thumbnails.
        Assert.DoesNotContain(window.GetVisualDescendants().OfType<TextBlock>(), t => t.IsEffectivelyVisible && (t.Text ?? "").StartsWith("Click a photo", StringComparison.Ordinal));
        UiHelpers.Capture(window, "p09-id-step1");

        Click(window, Card(id.Picker.Photos[1]));                                         // a click selects only
        Assert.Equal(id.Picker.Photos[1], id.Picker.SelectedPhoto);
        Assert.True(id.IsSelectStep);
        Assert.Null(id.SourcePath);

        Click(window, Card(id.Picker.Photos[2]), clicks: 2);                              // a double-click opens framing
        await UiHelpers.WaitUntil(() => id.ShowFraming && id.Preview is not null);
        Assert.EndsWith("P003.jpg", id.SourcePath);
        Assert.False(id.IsSelectStep);

        id.ShowSelectStepCommand.Execute(null);
        Click(window, Card(id.Picker.Photos[0]));                                         // back in step 1, a click still only selects
        Assert.True(id.IsSelectStep);
        Assert.EndsWith("P003.jpg", id.SourcePath);
        UiHelpers.Tap(window, PhysicalKey.Enter);                                         // Enter opens the selected photo
        await UiHelpers.WaitUntil(() => id.ShowFraming && id.SourcePath!.EndsWith("P001.jpg", StringComparison.Ordinal));
        window.Close();
    }

    /// <summary>
    /// Found while releasing 0.12.0 (a crash of the Windows test run): sheet previews were rendered from the portrait
    /// without the render gate, and a printed ID sheet kept the workspace's images, so Clear or another photo could
    /// free them while they were read.
    /// </summary>
    [AvaloniaFact]
    public async Task Id_sheet_previews_and_prints_survive_clearing_or_replacing_the_photo()
    {
        using var dir = new TempDir();
        var order = Order(dir, 2, "P", 800, 1200);
        var (window, shell) = UiHelpers.CreateWindow(Path.Combine(dir.Path, "cfg"), 1366, 768);
        var id = shell.Id;
        for (var i = 0; i < 4; i++)
        {
            await id.LoadSourceAsync(Path.Combine(order, "P001.jpg"));
            id.ContinueToSheetCommand.Execute(null);                                      // sheet previews start …
            id.CopiesMinusCommand.Execute(null);
            await id.ClearPhotoAsync();                                                   // … and the photo goes at once
            Assert.False(id.HasSource);
        }

        await id.LoadSourceAsync(Path.Combine(order, "P001.jpg"));
        id.ContinueToSheetCommand.Execute(null);
        id.PrintSheetCommand.Execute(null);                                               // the sheet waits in Printing …
        await id.LoadSourceAsync(Path.Combine(order, "P002.jpg"));                         // … while another photo replaces it
        await id.ClearPhotoAsync();
        await UiHelpers.WaitUntil(() => shell.Print.Previews.Single().Page is not null);
        shell.Print.PrintCommand.Execute(null);
        await UiHelpers.WaitUntil(() => UiHelpers.Printer.Submitted.Count == 1);
        var page = UiHelpers.Printer.Submitted.Single().Page;
        Assert.Equal((1200, 1800), (page.Width, page.Height));
        window.Close();
    }

    [AvaloniaFact]
    public async Task Collages_show_no_drag_hint_and_still_take_dragged_photos()
    {
        using var dir = new TempDir();
        var order = Order(dir, 2);
        var (window, shell) = UiHelpers.CreateWindow(Path.Combine(dir.Path, "cfg"), 1366, 768);
        await shell.Browse.OpenFolderAsync(order);
        shell.ShowCollagesCommand.Execute(null);
        var c = shell.Collage;
        await UiHelpers.WaitUntil(() => c.Picker.Photos.Count == 2);
        Dispatcher.UIThread.RunJobs();
        var texts = window.GetVisualDescendants().OfType<CollageView>().Single().GetVisualDescendants().OfType<TextBlock>().Where(t => t.IsEffectivelyVisible).Select(t => t.Text ?? "").ToList();
        Assert.DoesNotContain(texts, t => t.Contains("into a frame", StringComparison.Ordinal));          // the drag hint is gone
        Assert.True(c.Picker.CanDrag);
        await c.PlaceAsync(c.Picker.Photos[0].Path);                                       // what a drop does
        Assert.NotNull(c.Scene);
        UiHelpers.Capture(window, "p09-collage");
        window.Close();
    }

    [AvaloniaFact]
    public async Task The_open_workspace_stays_highlighted_when_clicked_again()
    {
        using var dir = new TempDir();
        var (window, shell) = UiHelpers.CreateWindow(Path.Combine(dir.Path, "cfg"), 1366, 768);
        await shell.Browse.OpenFolderAsync(Order(dir, 1));
        var ws = window.GetVisualDescendants().OfType<ChoiceButton>().Where(b => b.Classes.Contains("ws")).ToList();
        Assert.Equal(4, ws.Count);                                                        // Photos, ID Photos, Collages, Printing
        var (photos, idPhotos, collages) = (ws[0], ws[1], ws[2]);

        void Expect(ChoiceButton active)
        {
            Assert.All(ws, b => Assert.Equal(b == active, b.IsChecked == true));
            var presenter = active.GetVisualDescendants().OfType<ContentPresenter>().First(c => c.Name == "PART_ContentPresenter");
            Assert.Equal(Resource(window, "AccentBrush"), ((ISolidColorBrush)presenter.Background!).Color);  // the blue highlight
        }

        Click(window, photos);                                                            // the open workspace, clicked again
        Expect(photos);
        Assert.True(shell.IsBrowsing);
        Click(window, idPhotos);
        Expect(idPhotos);
        Click(window, idPhotos);
        Click(window, idPhotos);
        Expect(idPhotos);
        Assert.True(shell.IsIdWorkspace);
        Click(window, collages);
        Click(window, collages);
        Expect(collages);
        Click(window, photos);
        Expect(photos);

        var english = window.GetVisualDescendants().OfType<ChoiceButton>().First(b => b.Classes.Contains("lang") && (string?)b.Content == "EN");
        Click(window, english);                                                           // the language switch behaves alike
        Assert.True(english.IsChecked);
        window.Close();
    }

    [AvaloniaTheory]
    [InlineData("light")]
    [InlineData("dark")]
    public async Task The_hidden_tree_button_is_grey_and_the_shown_one_blue(string theme)
    {
        using var dir = new TempDir();
        var (window, shell) = UiHelpers.CreateWindow(Path.Combine(dir.Path, "cfg"), 1366, 768);
        await shell.Browse.OpenFolderAsync(Order(dir, 1));
        App.ApplyTheme(theme);
        try
        {
            var toggle = Find<ToggleButton>(window, "TreeToggle");
            var icon = toggle.GetVisualDescendants().OfType<Avalonia.Controls.Shapes.Path>().First();
            var presenter = toggle.GetVisualDescendants().OfType<ContentPresenter>().First(c => c.Name == "PART_ContentPresenter");
            Assert.True(toggle.IsChecked);
            Assert.Equal(Resource(window, "AccentBrush"), ((ISolidColorBrush)icon.Stroke!).Color);

            Click(window, toggle);                                                        // the tree hides
            Assert.False(shell.Browse.Explorer.IsTreeVisible);
            Assert.False(toggle.IsChecked);
            Assert.Equal(Resource(window, "Text2Brush"), ((ISolidColorBrush)icon.Stroke!).Color);          // grey, not black
            Assert.Equal(Resource(window, "Panel2Brush"), ((ISolidColorBrush)presenter.Background!).Color); // like an inactive workspace
            UiHelpers.Capture(window, $"p09-tree-hidden-{theme}");

            Click(window, toggle);                                                        // and shows again, blue
            Assert.True(shell.Browse.Explorer.IsTreeVisible);
            Assert.Equal(Resource(window, "AccentBrush"), ((ISolidColorBrush)icon.Stroke!).Color);
            Assert.Equal(Resource(window, "AccentTintBrush"), ((ISolidColorBrush)presenter.Background!).Color);
        }
        finally
        {
            App.ApplyTheme("light");
        }

        window.Close();
    }
}
