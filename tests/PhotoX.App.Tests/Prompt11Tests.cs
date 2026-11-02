using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using ImageMagick;
using PhotoX.App.ViewModels;
using PhotoX.App.Views;
using PhotoX.Core.Formats;
using PhotoX.Core.Printing;
using PhotoX.Printing;
using PhotoX.Tests;

namespace PhotoX.App.Tests;

/// <summary>
/// prompt_11 through the UI: the Citizen driver's papers chosen with their bleed and the preview as the cut print,
/// Escape in Settings, the editor's Space / Backspace / # keys, Backspace in the gallery keeping the keyboard there,
/// and one selection look for every way of moving (TS-26).
/// </summary>
public class Prompt11Tests
{
    private static string Order(TempDir dir, int count, int width = 900, int height = 600)
    {
        var order = Path.Combine(dir.Path, "order");
        Directory.CreateDirectory(order);
        for (var i = 1; i <= count; i++) Fixtures.WriteJpeg(order, $"IMG_{i:000}.jpg", width, height);
        return order;
    }

    private static T Find<T>(Visual root, string name)
        where T : Control => root.GetVisualDescendants().OfType<T>().First(c => c.Name == name);

    private static object? Focused(Window window) => window.FocusManager?.GetFocusedElement();

    [AvaloniaFact]
    public async Task The_citizen_paper_is_chosen_with_its_bleed_and_the_preview_shows_the_cut_print()
    {
        using var dir = new TempDir();
        var order = Order(dir, 2);
        var (window, shell) = UiHelpers.CreateWindow(Path.Combine(dir.Path, "cfg"), 1366, 768);
        UiHelpers.Printer.CitizenDriverSizes = true;                                       // the driver's own sizes, with bleed
        await shell.Browse.OpenFolderAsync(order);
        shell.Browse.PrintAllCommand.Execute(null);
        var print = shell.Print;
        Assert.Equal("(6x4)", print.SelectedPaper?.Paper.Name);                             // found by itself, no "no matching paper"
        Assert.True(print.PaperMatches);
        Assert.False(print.HasNoMatchingPaper);
        Assert.Null(print.PaperNote);
        print.SetSizeCommand.Execute(PrintFormats.P15x20);
        Assert.Equal("(6x8)", print.SelectedPaper?.Paper.Name);
        print.SetSizeCommand.Execute(PrintFormats.P10x15);

        // The preview is the print after the cut: a 3:2 photo in Crop is covered edge to edge, at the print size's ratio.
        using var photo = new MagickImage(Path.Combine(order, "IMG_001.jpg"));
        using var crop = PrintViewModel.FinishedPreview(photo, PrintFit.Crop, PrintFormats.P10x15, print.SelectedPaper!.Paper, 340);
        Assert.Equal((340u, 510u), (crop.Width, crop.Height));
        using var whole = PrintViewModel.FinishedPreview(photo, PrintFit.Fit, PrintFormats.P10x15, print.SelectedPaper!.Paper, 340);
        Assert.Equal((340u, 510u), (whole.Width, whole.Height));
        await UiHelpers.WaitUntil(() => print.Previews.All(p => p.Page is not null));
        UiHelpers.Capture(window, "p11-print-citizen");

        print.PrintCommand.Execute(null);
        await UiHelpers.WaitUntil(() => UiHelpers.Printer.Submitted.Count == 2);
        Assert.All(UiHelpers.Printer.Submitted, s => Assert.Equal((1844, 1240), (s.Page.Width, s.Page.Height)));   // the whole driver page
        window.Close();
    }

    [AvaloniaFact]
    public async Task Escape_closes_settings_like_its_close_button()
    {
        using var dir = new TempDir();
        var (window, shell) = UiHelpers.CreateWindow(Path.Combine(dir.Path, "cfg"), 1366, 768);
        shell.OpenSettingsCommand.Execute(null);
        Assert.NotNull(shell.Settings);
        shell.Settings!.SetThemeCommand.Execute("dark");                                  // applied and saved as it changes
        UiHelpers.Tap(window, PhysicalKey.Escape);
        Assert.Null(shell.Settings);
        Assert.Equal("dark", PhotoX.App.Services.AppServices.Current.Settings.Theme);
        Assert.True(shell.IsBrowsing);                                                    // nothing else happened
        App.ApplyTheme("light");
        window.Close();
    }

    [AvaloniaFact]
    public async Task Space_backspace_and_the_hash_key_drive_the_editor()
    {
        using var dir = new TempDir();
        var order = Order(dir, 3, 1000, 600);
        var (window, shell) = UiHelpers.CreateWindow(Path.Combine(dir.Path, "cfg"), 1366, 768);
        await shell.Browse.OpenFolderAsync(order);
        shell.Browse.StartEditingCommand.Execute(null);
        var editor = shell.Editor;
        await UiHelpers.WaitUntil(() => editor is { Preview: not null, IsLoading: false });

        UiHelpers.Tap(window, PhysicalKey.Space);                                         // Space: next photo, nothing saved
        await UiHelpers.WaitUntil(() => editor.Current!.Name == "IMG_002.jpg" && !editor.IsBusy);
        Assert.False(editor.ShowBefore);
        UiHelpers.Tap(window, PhysicalKey.Backspace);                                     // Backspace: previous photo
        await UiHelpers.WaitUntil(() => editor.Current!.Name == "IMG_001.jpg" && !editor.IsBusy);
        Assert.False(Directory.Exists(Path.Combine(order, "Original")));

        var start = editor.Recipe.Crop;
        UiHelpers.Tap(window, PhysicalKey.ArrowRight);                                    // the arrows still move the crop
        Assert.True(editor.Recipe.Crop.X > start.X);

        var button = Find<Button>(window.GetVisualDescendants().OfType<EditorView>().Single(), "BeforeButton");
        window.KeyPressQwerty(PhysicalKey.Backslash, RawInputModifiers.None);              // # held: the original …
        Dispatcher.UIThread.RunJobs();
        Assert.True(editor.ShowBefore);
        Assert.Contains("active", button.Classes);
        window.KeyPressQwerty(PhysicalKey.Backslash, RawInputModifiers.None);              // … auto-repeat changes nothing
        window.KeyReleaseQwerty(PhysicalKey.Backslash, RawInputModifiers.None);            // released: the edit again
        Dispatcher.UIThread.RunJobs();
        Assert.False(editor.ShowBefore);
        Assert.DoesNotContain("active", button.Classes);

        UiHelpers.Tap(window, PhysicalKey.Enter);                                         // Enter: save and go on
        await UiHelpers.WaitUntil(() => editor.Current!.Name == "IMG_002.jpg" && !editor.IsBusy);
        Assert.True(File.Exists(Path.Combine(order, "Original", "IMG_001.jpg")));
        UiHelpers.Tap(window, PhysicalKey.Escape);                                        // Escape: the gallery
        Assert.True(shell.IsBrowsing);
        window.Close();
    }

    [AvaloniaFact]
    public async Task Backspace_in_the_gallery_goes_up_and_keeps_the_keyboard_in_the_gallery()
    {
        using var dir = new TempDir();
        var order = Order(dir, 3);
        foreach (var sub in new[] { "Album", "Retouch" })
        {
            Directory.CreateDirectory(Path.Combine(order, sub));
            Fixtures.WriteJpeg(Path.Combine(order, sub), "R1.jpg", 600, 400);
        }

        var (window, shell) = UiHelpers.CreateWindow(Path.Combine(dir.Path, "cfg"), 1366, 768);
        var browse = shell.Browse;
        await browse.OpenFolderAsync(order);
        var gallery = Find<ListBox>(window, "Gallery");
        gallery.SelectedItem = browse.GalleryItems[1];                                     // the "Retouch" card …
        gallery.ContainerFromIndex(1)!.Focus(NavigationMethod.Pointer);
        UiHelpers.Tap(window, PhysicalKey.Enter);                                         // … opened
        await UiHelpers.WaitUntil(() => browse.CurrentFolder == Path.Combine(order, "Retouch") && !browse.IsScanning);
        Dispatcher.UIThread.RunJobs();

        UiHelpers.Tap(window, PhysicalKey.Backspace);                                     // up again
        await UiHelpers.WaitUntil(() => browse.CurrentFolder == order && browse.AllPhotos.Count == 3);
        await UiHelpers.WaitUntil(() => Focused(window) is ListBoxItem);
        Assert.Equal("Retouch", (browse.SelectedGalleryItem as FolderItemViewModel)?.Name);  // the folder just left
        Assert.Same(gallery, ((ListBoxItem)Focused(window)!).FindAncestorOfType<ListBox>()); // the keyboard is in the gallery
        UiHelpers.Tap(window, PhysicalKey.ArrowRight);                                    // so → moves through the cards
        Assert.Equal("IMG_001.jpg", browse.SelectedPhoto?.Name);
        Assert.Equal(order, browse.CurrentFolder);                                        // and not through the tree
        window.Close();
    }

    [AvaloniaTheory]
    [InlineData("light")]
    [InlineData("dark")]
    public async Task Every_way_of_moving_shows_the_same_rounded_blue_selection(string theme)
    {
        using var dir = new TempDir();
        var (window, shell) = UiHelpers.CreateWindow(Path.Combine(dir.Path, "cfg"), 1366, 768);
        var browse = shell.Browse;
        await browse.OpenFolderAsync(Order(dir, 12, 300, 200));
        App.ApplyTheme(theme);
        try
        {
            var gallery = Find<ListBox>(window, "Gallery");
            Dispatcher.UIThread.RunJobs();
            var first = gallery.ContainerFromIndex(0)!;
            var p = first.TranslatePoint(new Point(first.Bounds.Width / 2, first.Bounds.Height / 2), window)!.Value;
            window.MouseDown(p, MouseButton.Left);                                        // mouse
            window.MouseUp(p, MouseButton.Left);
            foreach (var key in new[] { PhysicalKey.ArrowRight, PhysicalKey.ArrowDown, PhysicalKey.ArrowLeft, PhysicalKey.ArrowUp, PhysicalKey.ArrowDown })
            {
                UiHelpers.Tap(window, key);
                Dispatcher.UIThread.RunJobs();
                var item = (ListBoxItem)Focused(window)!;
                Assert.True(item.IsSelected);
                Assert.Null(item.FocusAdorner);                                           // no black focus frame on top
                var card = item.GetVisualDescendants().OfType<Border>().First(b => b.Classes.Contains("card"));
                Assert.Equal(((ISolidColorBrush)window.FindResource("AccentBrush")!).Color, ((ISolidColorBrush)card.BorderBrush!).Color);
                Assert.Equal(new CornerRadius(9), card.CornerRadius);
            }

            UiHelpers.Capture(window, $"p11-selection-{theme}");
        }
        finally
        {
            App.ApplyTheme("light");
        }

        window.Close();
    }
}
