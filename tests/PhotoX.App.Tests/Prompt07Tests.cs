using Avalonia.Headless.XUnit;
using Avalonia.Input;
using ImageMagick;
using PhotoX.App.ViewModels;
using PhotoX.Core.Formats;
using PhotoX.Printing;
using PhotoX.Tests;

namespace PhotoX.App.Tests;

/// <summary>prompt_07 through the UI, with a fake printer (TS-22).</summary>
public class Prompt07Tests
{
    private static string Order(TempDir dir, int count, int w = 900, int h = 600)
    {
        var order = Path.Combine(dir.Path, "order");
        Directory.CreateDirectory(order);
        for (var i = 1; i <= count; i++) Fixtures.WriteJpeg(order, $"P{i}.jpg", w, h);
        return order;
    }

    private static MagickImage PageImage(PhotoX.Imaging.Printing.PageBitmap page)
    {
        var rows = new byte[page.Width * 3 * page.Height];
        for (var y = 0; y < page.Height; y++) Buffer.BlockCopy(page.Bgr, y * page.Stride, rows, y * page.Width * 3, page.Width * 3);
        return new MagickImage(rows, new PixelReadSettings((uint)page.Width, (uint)page.Height, StorageType.Char, PixelMapping.BGR));
    }

    /// <summary>
    /// Mean brightness of the left and right 5 % of a portrait page: white borders read close to 1. (A landscape photo
    /// is turned upright on the page, so its borders are at the sides.)
    /// </summary>
    private static (double Left, double Right) EdgeBrightness(MagickImage page)
    {
        double Mean(int x)
        {
            using var band = (MagickImage)page.Clone();
            band.Crop(new MagickGeometry(x, 0, page.Width / 20, page.Height));
            band.ColorSpace = ColorSpace.Gray;
            return band.Statistics().GetChannel(PixelChannel.Gray)!.Mean / 65535;
        }

        return (Mean(0), Mean((int)(page.Width * 19 / 20)));
    }

    [AvaloniaFact]
    public async Task The_editor_prints_the_unsaved_edit_and_escape_returns_to_it()
    {
        using var dir = new TempDir();
        var order = Order(dir, 2);
        var hashes = Directory.GetFiles(order).Order().Select(Fixtures.Sha).ToList();
        var (window, shell) = UiHelpers.CreateWindow(Path.Combine(dir.Path, "cfg"), 1440, 900);
        await shell.Browse.OpenFolderAsync(order);
        shell.Browse.EditPhotoCommand.Execute(shell.Browse.AllPhotos[0]);
        var editor = shell.Editor;
        await UiHelpers.WaitUntil(() => editor is { Preview: not null, IsLoading: false });
        editor.CommitCrop(new Avalonia.Rect(0, 0, 300, 200));                // a manual crop (top-left third) and an exposure change, not saved
        editor.Tone.Exposure = 0.5;
        var recipe = editor.Recipe;

        editor.PrintCommand.Execute(null);
        Assert.True(shell.IsPrintWorkspace);
        Assert.Equal(recipe, shell.Browse.AllPhotos[0].PendingRecipe);     // the current edit, kept in memory
        Assert.False(shell.Print.WholePhoto);                               // Crop is the default
        shell.Print.PrintCommand.Execute(null);
        await UiHelpers.WaitUntil(() => UiHelpers.Printer.Submitted.Count == 1);

        // The printed page is the manual crop (not a new automatic crop of the whole photo), brightened.
        using (var page = PageImage(UiHelpers.Printer.Submitted[0].Page))
        using (var part = new MagickImage(Path.Combine(order, "P1.jpg")))
        using (var whole = new MagickImage(Path.Combine(order, "P1.jpg")))
        {
            part.Crop(new MagickGeometry(0, 0, 300, 200));
            foreach (var img in new[] { part, whole })
            {
                img.Rotate(90);
                img.Resize(new MagickGeometry(page.Width, page.Height) { IgnoreAspectRatio = true });
                img.Evaluate(Channels.All, EvaluateOperator.Multiply, 1.4);
            }

            Assert.True(page.Compare(part, ErrorMetric.MeanAbsolute) < page.Compare(whole, ErrorMetric.MeanAbsolute) / 3);
        }

        Assert.Equal(hashes, Directory.GetFiles(order).Order().Select(Fixtures.Sha).ToList()); // nothing was written
        UiHelpers.Tap(window, PhysicalKey.Escape);                         // Escape: back to the editor, edit intact
        Assert.True(shell.IsEditing);
        Assert.Equal("P1.jpg", editor.Current!.Name);
        Assert.Equal(recipe, editor.Recipe);
        window.Close();
    }

    [AvaloniaFact]
    public async Task Untouched_photos_print_cropped_by_default_or_whole_with_borders()
    {
        using var dir = new TempDir();
        var order = Order(dir, 1, 1200, 600);                               // 2:1, far from the 2:3 paper
        var (window, shell) = UiHelpers.CreateWindow(Path.Combine(dir.Path, "cfg"), 1440, 900);
        await shell.Browse.OpenFolderAsync(order);
        shell.Browse.PrintAllCommand.Execute(null);
        Assert.True(shell.Print.HasPhotos);
        shell.Print.PrintCommand.Execute(null);                             // Crop
        shell.Browse.PrintAllCommand.Execute(null);
        Assert.False(shell.Print.WholePhoto);                               // every new print starts with Crop
        await UiHelpers.WaitUntil(() => shell.Print.Previews.All(p => p.Page is not null));
        var cropPreview = shell.Print.Previews[0].Page;
        shell.Print.SetWholeCommand.Execute(true);
        await UiHelpers.WaitUntil(() => shell.Print.Previews[0].Page is { } page && page != cropPreview);
        UiHelpers.Capture(window, "print-whole");
        using (var ms = new MemoryStream())
        {
            shell.Print.Previews[0].Page!.Save(ms, new Avalonia.Media.Imaging.PngBitmapEncoderOptions());                       // the preview shows the borders too
            using var preview = new MagickImage(ms.ToArray());
            var (l, r) = EdgeBrightness(preview);
            Assert.True(l > 0.97 && r > 0.97, "the preview shows Whole");
        }

        shell.Print.PrintCommand.Execute(null);
        await UiHelpers.WaitUntil(() => UiHelpers.Printer.Submitted.Count == 2);

        using var cropped = PageImage(UiHelpers.Printer.Submitted[0].Page);
        using var whole = PageImage(UiHelpers.Printer.Submitted[1].Page);
        Assert.True(EdgeBrightness(cropped).Left < 0.95, "Crop fills the paper");
        var (left, right) = EdgeBrightness(whole);
        Assert.True(left > 0.99 && right > 0.99, "Whole leaves white borders");

        // ID sheets and collages keep their layout: no Crop | Whole.
        shell.ShowIdCommand.Execute(null);
        await shell.Id.LoadSourceAsync(Path.Combine(order, "P1.jpg"));
        shell.Id.ContinueToSheetCommand.Execute(null);
        shell.Id.PrintSheetCommand.Execute(null);
        Assert.False(shell.Print.HasPhotos);
        window.Close();
    }

    [AvaloniaFact]
    public async Task The_calibration_sheet_returns_to_the_photos_that_were_waiting()
    {
        using var dir = new TempDir();
        var order = Order(dir, 3);
        var (window, shell) = UiHelpers.CreateWindow(Path.Combine(dir.Path, "cfg"), 1440, 900);
        await shell.Browse.OpenFolderAsync(order);
        shell.Browse.PrintAllCommand.Execute(null);
        var p = shell.Print;
        p.CopiesPlusCommand.Execute(null);
        p.SetWholeCommand.Execute(true);
        p.SetSizeCommand.Execute(PrintFormats.P15x20);

        p.LoadCalibrationCommand.Execute(null);
        Assert.True(p.IsCalibration);
        Assert.Equal(1, p.PhotoCount);
        Assert.Equal("Cancel calibration", p.ClearText);
        UiHelpers.Tap(window, PhysicalKey.Escape);                         // Escape (Back) cancels the calibration only
        Assert.True(shell.IsPrintWorkspace);
        Assert.False(p.IsCalibration);
        Assert.Equal((3, 2, true, "15x20"), (p.PhotoCount, p.Copies, p.WholePhoto, p.SelectedSize.Id));

        p.LoadCalibrationCommand.Execute(null);
        p.SetSizeCommand.Execute(PrintFormats.P10x15);
        p.PrintCommand.Execute(null);                                       // printing the sheet also returns to the photos
        await UiHelpers.WaitUntil(() => UiHelpers.Printer.Submitted.Count == 1);
        Assert.Equal((1200, 1800), (UiHelpers.Printer.Submitted[0].Page.Width, UiHelpers.Printer.Submitted[0].Page.Height));
        Assert.Equal((3, 2, true, "15x20"), (p.PhotoCount, p.Copies, p.WholePhoto, p.SelectedSize.Id));

        UiHelpers.Tap(window, PhysicalKey.Escape);                         // and Escape leaves Printing for Photos
        Assert.True(shell.IsBrowsing);
        Assert.Equal(3, shell.Print.PhotoCount);                            // the prepared print is kept
        window.Close();
    }

    [AvaloniaFact]
    public async Task Statistics_are_folded_when_print_opens_and_all_time_shows_its_start()
    {
        using var dir = new TempDir();
        var order = Order(dir, 1);
        var (window, shell) = UiHelpers.CreateWindow(Path.Combine(dir.Path, "cfg"), 1440, 900);
        await shell.Browse.OpenFolderAsync(order);
        shell.Browse.PrintAllCommand.Execute(null);
        Assert.False(shell.Print.StatisticsOpen);
        shell.Print.PrintCommand.Execute(null);
        await UiHelpers.WaitUntil(() => shell.Print.History.Tiles.Count == 4 && shell.Print.History.Tiles[3].Prints == "1", 10000);
        Assert.Equal($"All time, since {DateTime.Now:dd.MM.yyyy}", shell.Print.History.Tiles[3].Title);
        shell.Print.ToggleStatisticsCommand.Execute(null);
        Assert.True(shell.Print.StatisticsOpen);
        UiHelpers.Capture(window, "printing-statistics-open");
        shell.Browse.PrintAllCommand.Execute(null);
        Assert.False(shell.Print.StatisticsOpen);                           // folded again for the next customer
        window.Close();
    }

    [AvaloniaFact]
    public async Task A_printer_without_the_size_gets_no_paper_and_a_clear_message()
    {
        using var dir = new TempDir();
        var order = Order(dir, 1);
        var (window, shell) = UiHelpers.CreateWindow(Path.Combine(dir.Path, "cfg"), 1440, 900);
        await shell.Browse.OpenFolderAsync(order);
        shell.Browse.PrintAllCommand.Execute(null);
        var p = shell.Print;
        p.SelectedPrinter = "Microsoft Print to PDF";
        p.SetSizeCommand.Execute(PrintFormats.P15x20);
        Assert.True(p.HasNoMatchingPaper);
        Assert.Null(p.SelectedPaper);                                       // never a wrong size silently
        Assert.False(p.CanPrint);
        Assert.Contains("6 × 8 in", p.NoMatchingPaperText);
        p.SelectedPrinter = "Citizen CY-02";
        Assert.False(p.HasNoMatchingPaper);
        Assert.Equal("(6x8)", p.SelectedPaper!.Paper.Name);
        window.Close();
    }

    [AvaloniaFact]
    public async Task Id_photos_clear_the_photo_and_a_click_loads_it_again()
    {
        using var dir = new TempDir();
        var order = Order(dir, 2, 800, 1200);
        var hashes = Directory.GetFiles(order).Order().Select(Fixtures.Sha).ToList();
        var (window, shell) = UiHelpers.CreateWindow(Path.Combine(dir.Path, "cfg"), 1440, 900);
        await shell.Browse.OpenFolderAsync(order);
        shell.ShowIdCommand.Execute(null);
        var id = shell.Id;
        await UiHelpers.WaitUntil(() => id.Picker.Photos.Count == 2);
        id.Picker.SelectedPhoto = id.Picker.Photos[0];
        await UiHelpers.WaitUntil(() => id.HasSource && id.Preview is not null);
        id.Tone.Exposure = 0.4;
        id.RotateRightCommand.Execute(null);
        UiHelpers.Capture(window, "id-before-clear");

        await id.ClearPhotoCommand.ExecuteAsync(null);
        Assert.False(id.HasSource);
        Assert.Null(id.Preview);
        Assert.Null(id.Framing);
        Assert.False(id.CanUndo);
        Assert.Equal(0, id.Tone.Exposure);
        Assert.Null(id.Picker.SelectedPhoto);
        UiHelpers.Capture(window, "id-cleared");
        Assert.Equal(hashes, Directory.GetFiles(order).Order().Select(Fixtures.Sha).ToList());

        id.Picker.SelectedPhoto = id.Picker.Photos[0];                      // the same photo loads again, fresh
        await UiHelpers.WaitUntil(() => id.HasSource && id.Preview is not null);
        Assert.Equal(0, id.Framing!.QuarterTurns);
        window.Close();
    }

    [AvaloniaFact]
    public async Task The_9x13_format_is_saved_centred_on_a_10x15_sheet()
    {
        using var dir = new TempDir();
        var order = Order(dir, 1, 1800, 2700);
        var (window, shell) = UiHelpers.CreateWindow(Path.Combine(dir.Path, "cfg"), 1440, 900);
        await shell.Browse.OpenFolderAsync(order);
        shell.Browse.EditPhotoCommand.Execute(shell.Browse.AllPhotos[0]);
        var editor = shell.Editor;
        await UiHelpers.WaitUntil(() => editor is { Preview: not null, IsLoading: false });
        editor.SetFormatCommand.Execute("9x13");
        Assert.Equal(90.0 / 130.0, editor.Recipe.CropRatio, 6);
        UiHelpers.Capture(window, "editor-9x13");
        await editor.SaveCommand.ExecuteAsync(null);
        using var saved = new MagickImage(Path.Combine(order, "P1.jpg"));
        Assert.Equal(2.0 / 3.0, saved.Width / (double)saved.Height, 2);     // the 10×15 sheet
        using var px = saved.GetPixels();
        Assert.True(px.GetPixel(2, 2)!.ToColor()!.R > 64000);               // white margin around the 9×13 photo
        window.Close();
    }

    [AvaloniaFact]
    public void New_settings_start_in_light_mode()
    {
        using var dir = new TempDir();
        var services = new Services.AppServices(new PhotoX.Storage.AppPaths(dir.Path), new FakePrintBackend());
        Assert.Equal("light", services.Settings.Theme);
    }
}
