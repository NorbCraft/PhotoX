using Avalonia.Headless.XUnit;
using ImageMagick;
using PhotoX.App.ViewModels;
using PhotoX.Core.Formats;
using PhotoX.Printing;
using PhotoX.Tests;

namespace PhotoX.App.Tests;

/// <summary>prompt_05 through the UI, with a fake printer: printing, counters, queue, ID and collage printing (TS-20).</summary>
public class Prompt05Tests
{
    private static string Order(TempDir dir, int count)
    {
        var order = Path.Combine(dir.Path, "order");
        Directory.CreateDirectory(order);
        for (var i = 1; i <= count; i++) Fixtures.WriteJpeg(order, $"P{i}.jpg", 900, 600);
        return order;
    }

    [AvaloniaFact]
    public async Task Printing_selected_photos_prints_unsaved_edits_and_counts_accepted_copies()
    {
        using var dir = new TempDir();
        var order = Order(dir, 3);
        var hashes = Directory.GetFiles(order).Order().Select(Fixtures.Sha).ToList();
        var (window, shell) = UiHelpers.CreateWindow(Path.Combine(dir.Path, "cfg"));
        await shell.Browse.OpenFolderAsync(order);
        shell.Browse.StartEditingCommand.Execute(null);
        await UiHelpers.WaitUntil(() => shell.Editor is { Preview: not null, IsLoading: false });
        shell.Editor.CommitCrop(new Avalonia.Rect(0, 0, 300, 200));     // an unsaved crop of P1
        shell.Editor.BackToFolderCommand.Execute(null);
        var p1 = shell.Browse.AllPhotos.Single(p => p.Name == "P1.jpg");
        Assert.NotNull(p1.PendingRecipe);
        Assert.Equal("–", p1.PrintCountText);

        shell.PrintPhotos([p1, shell.Browse.AllPhotos.Single(p => p.Name == "P2.jpg")]);
        var dialog = shell.PrintDialog!;
        Assert.Equal("Citizen CY-02", dialog.SelectedPrinter);              // the Citizen is preferred
        Assert.Equal("(6x4)", dialog.SelectedPaper!.Paper.Name);            // the driver's 10×15 paper
        Assert.True(dialog.PaperMatches);
        dialog.CopiesPlusCommand.Execute(null);
        await UiHelpers.WaitUntil(() => dialog.Previews.All(p => p.Page is not null));
        UiHelpers.Capture(window, "print-dialog");
        dialog.PrintCommand.Execute(null);
        Assert.Null(shell.PrintDialog);
        Assert.True(shell.PrintQueue.IsOpen);

        await UiHelpers.WaitUntil(() => shell.PrintQueue.Jobs.Count == 2 && shell.PrintQueue.Jobs.All(j => j.Job.State == PrintJobState.InQueue));
        await UiHelpers.WaitUntil(() => p1.PrintCount == 2);
        Assert.Equal("2", p1.PrintCountText);
        UiHelpers.Capture(window, "print-queue");
        var submitted = UiHelpers.Printer.Submitted;
        Assert.Equal(2, submitted.Count);
        Assert.All(submitted, s => Assert.Equal((1200, 1800, 2), (s.Page.Width, s.Page.Height, s.Copies)));

        // The unsaved crop (top-left third) was printed: the printed page matches that part of the photo, not the whole photo.
        using (var page = PageImage(submitted.Single(s => s.DocumentName.Contains("P1.jpg")).Page))
        using (var photo = new MagickImage(Path.Combine(order, "P1.jpg")))
        {
            photo.Crop(new MagickGeometry(0, 0, 300, 200));
            photo.Rotate(90);
            photo.Resize(new MagickGeometry(1200, 1800) { IgnoreAspectRatio = true });
            using var whole = new MagickImage(Path.Combine(order, "P1.jpg"));
            whole.Rotate(90);
            whole.Resize(new MagickGeometry(1200, 1800) { IgnoreAspectRatio = true });
            Assert.True(page.Compare(photo, ErrorMetric.MeanAbsolute) < page.Compare(whole, ErrorMetric.MeanAbsolute) / 3);
        }

        Assert.Equal(hashes, Directory.GetFiles(order).Order().Select(Fixtures.Sha).ToList()); // nothing saved or changed
        Assert.False(Directory.Exists(Path.Combine(order, "Original")));
        Assert.NotNull(p1.PendingRecipe);                                    // the edit is still unsaved

        // Cancelling a job in the queue takes its copies back.
        var job = shell.PrintQueue.Jobs.Single(j => j.Name == "P1.jpg");
        shell.PrintQueue.CancelCommand.Execute(job);
        await UiHelpers.WaitUntil(() => job.Job.State == PrintJobState.Canceled && p1.PrintCount == 0);

        // Reopening the order keeps the counters.
        await shell.Browse.OpenFolderAsync(order);
        Assert.Equal(2, shell.Browse.AllPhotos.Single(p => p.Name == "P2.jpg").PrintCount);
        window.Close();
    }

    private static MagickImage PageImage(PhotoX.Imaging.Printing.PageBitmap page)
    {
        var rows = new byte[page.Width * 3 * page.Height];
        for (var y = 0; y < page.Height; y++) Buffer.BlockCopy(page.Bgr, y * page.Stride, rows, y * page.Width * 3, page.Width * 3);
        return new MagickImage(rows, new PixelReadSettings((uint)page.Width, (uint)page.Height, StorageType.Char, PixelMapping.BGR));
    }

    [AvaloniaFact]
    public async Task A_15x20_print_uses_the_plain_6x8_paper_and_a_pdf_printer_needs_a_paper_choice()
    {
        using var dir = new TempDir();
        var order = Order(dir, 1);
        var (window, shell) = UiHelpers.CreateWindow(Path.Combine(dir.Path, "cfg"));
        await shell.Browse.OpenFolderAsync(order);
        shell.Browse.PrintAllCommand.Execute(null);
        var dialog = shell.PrintDialog!;
        dialog.SetSizeCommand.Execute(PrintFormats.P15x20);
        Assert.Equal("(6x8)", dialog.SelectedPaper!.Paper.Name);             // not the two-up variant
        dialog.SelectedPrinter = "Microsoft Print to PDF";
        Assert.Null(dialog.SelectedPaper);                                   // no 15×20 paper: the operator chooses
        Assert.False(dialog.CanPrint);
        dialog.SelectedPaper = dialog.Papers.Single(p => p.Paper.Name == "A4");
        Assert.True(dialog.CanPrint);
        Assert.NotNull(dialog.PaperNote);                                    // centred at true size
        dialog.PrintCommand.Execute(null);
        await UiHelpers.WaitUntil(() => UiHelpers.Printer.Submitted.Count == 1);
        var s = UiHelpers.Printer.Submitted[0];
        Assert.Equal((1800, 2400), (s.Page.Width, s.Page.Height));            // 152.4 × 203.2 mm at 300 dpi
        Assert.Equal(((2480 - 1800) / 2, (3508 - 2400) / 2), (s.X, s.Y));       // centred on A4
        window.Close();
    }

    [AvaloniaFact]
    public async Task Id_sheet_prints_on_10x15_with_its_exact_size()
    {
        using var dir = new TempDir();
        var order = Order(dir, 1);
        var (window, shell) = UiHelpers.CreateWindow(Path.Combine(dir.Path, "cfg"));
        shell.ShowIdCommand.Execute(null);
        var id = shell.Id;
        await id.LoadSourceAsync(Path.Combine(order, "P1.jpg"));
        id.SelectSizeCommand.Execute(id.Sizes.Single(s => s.Size.Id == "3x4"));
        id.ContinueToSheetCommand.Execute(null);
        id.PrintSheetCommand.Execute(null);
        var dialog = shell.PrintDialog!;
        Assert.True(dialog.IsSizeFixed);
        Assert.Equal("10x15", dialog.SelectedSize.Id);
        dialog.PrintCommand.Execute(null);
        await UiHelpers.WaitUntil(() => UiHelpers.Printer.Submitted.Count == 1);
        var s = UiHelpers.Printer.Submitted[0];
        Assert.Equal((1200, 1800, 1), (s.Page.Width, s.Page.Height, s.Copies)); // the 1800 × 1200 sheet, turned, 1:1
        Assert.Contains("3 × 4 cm", s.DocumentName);
        window.Close();
    }

    [AvaloniaFact]
    public async Task Collage_has_two_sheets_shows_slot_sizes_and_adjusts_slots_within_the_sheet()
    {
        using var dir = new TempDir();
        var order = Order(dir, 3);
        var (window, shell) = UiHelpers.CreateWindow(Path.Combine(dir.Path, "cfg"));
        await shell.Browse.OpenFolderAsync(order);
        shell.ShowCollagesCommand.Execute(null);
        var c = shell.Collage;
        Assert.Equal(["10x15", "15x20"], c.Sheets.Select(s => s.Id));
        c.SetTemplateCommand.Execute(c.Templates.Single(t => t.Template.Id == "1-big-2-below"));
        Assert.Equal("9.6 × 9.7 cm", c.Scene!.Labels![0]);                     // the big photo on 10×15 with 3 mm gaps

        c.SelectSlot(1);
        c.SlotWidthCm = 6;                                                   // a custom size for the lower-left photo
        Assert.Equal(60, c.Slots[1].Width, 1);
        Assert.Equal(101.6 - 3 * 3 - 60, c.Slots[2].Width, 1);               // its neighbour takes the rest
        c.SlotHeightCm = 30;                                                 // too tall: limited to the sheet
        Assert.True(c.Slots.All(r => r.Bottom <= 152.4 - 3 + 1e-6 && r.Height >= 10 - 1e-6));
        c.MoveBoundary(column: false, index: 1, deltaMm: -500);              // dragging a gap far away stops at 1 cm
        Assert.True(c.Slots.All(r => r.Height >= 10 - 1e-6));
        c.ResetLayoutCommand.Execute(null);
        Assert.Equal(c.Slots[1].Width, c.Slots[2].Width, 6);

        await UiHelpers.WaitUntil(() => c.Picker.Photos.Count == 3);
        foreach (var p in c.Picker.Photos.Take(3)) await c.PlaceAsync(p.Path);
        UiHelpers.Capture(window, "collage-sizes");
        c.PrintCommand.Execute(null);
        var dialog = shell.PrintDialog!;
        Assert.True(dialog.IsSizeFixed);
        dialog.PrintCommand.Execute(null);
        await UiHelpers.WaitUntil(() => UiHelpers.Printer.Submitted.Count == 1);
        Assert.Equal((1200, 1800), (UiHelpers.Printer.Submitted[0].Page.Width, UiHelpers.Printer.Submitted[0].Page.Height));
        window.Close();
    }
}
