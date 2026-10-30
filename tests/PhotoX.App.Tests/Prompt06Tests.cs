using Avalonia;
using Avalonia.Headless.XUnit;
using Avalonia.Styling;
using PhotoX.App.ViewModels;
using PhotoX.Core.Formats;
using PhotoX.Core.Id;
using PhotoX.Printing;
using PhotoX.Tests;

namespace PhotoX.App.Tests;

/// <summary>prompt_06 through the UI, with a fake printer (TS-21).</summary>
public class Prompt06Tests
{
    private static string Order(TempDir dir, int count, int w = 900, int h = 600)
    {
        var order = Path.Combine(dir.Path, "order");
        Directory.CreateDirectory(order);
        for (var i = 1; i <= count; i++) Fixtures.WriteJpeg(order, $"P{i}.jpg", w, h);
        return order;
    }

    [AvaloniaFact]
    public async Task Printing_workspace_shows_the_print_then_keeps_the_jobs_in_history_and_statistics()
    {
        using var dir = new TempDir();
        var order = Order(dir, 3);
        var (window, shell) = UiHelpers.CreateWindow(Path.Combine(dir.Path, "cfg"), 1440, 900);
        await shell.Browse.OpenFolderAsync(order);
        var photos = shell.Browse.AllPhotos.ToList();

        shell.PrintPhotos(photos.Take(2).ToList());
        Assert.True(shell.IsPrintWorkspace);                                 // Print opens the Printing workspace
        Assert.Equal(2, shell.Print.PhotoCount);
        Assert.Equal("Photos", shell.Print.OriginName);
        Assert.Empty(UiHelpers.Printer.Submitted);
        shell.Print.CopiesPlusCommand.Execute(null);
        shell.Print.PrintCommand.Execute(null);
        await UiHelpers.WaitUntil(() => shell.Print.Queue.Active.Count == 2 && shell.Print.Queue.Active.All(j => j.Job.State == PrintJobState.InQueue));
        Assert.Equal("2", shell.PrintBadge);

        // One job is cancelled, the other leaves the Windows queue: both move to the history.
        var first = shell.Print.Queue.Active.Single(j => j.Name == "P1.jpg");
        shell.Print.Queue.CancelCommand.Execute(first);
        var second = shell.Print.Queue.Active.Single(j => j.Name == "P2.jpg").Job;
        lock (UiHelpers.Printer.Submitted) UiHelpers.Printer.States.Remove(second.WindowsJobId!.Value);
        await UiHelpers.WaitUntil(() => shell.Print.Queue.Active.Count == 0, 10000);
        await UiHelpers.WaitUntil(() => shell.Print.History.Entries.Count == 2 && shell.Print.History.Tiles.Count == 4);
        Assert.True(shell.Print.History.Entries.Single(e => e.Name == "P2.jpg").IsOk);
        Assert.True(shell.Print.History.Entries.Single(e => e.Name == "P1.jpg").IsMuted);
        Assert.Equal("2", shell.Print.History.Tiles[0].Prints);              // today: the 2 copies of P2 only
        Assert.Contains("10×15: 2", shell.Print.History.Tiles[0].Split);
        Assert.Equal("Printed: 2 copies", shell.Browse.PrintedText);         // the folder's summary in Photos
        UiHelpers.Capture(window, "printing-history");

        // The history is kept for the next session.
        var (window2, shell2) = UiHelpers.CreateWindow(Path.Combine(dir.Path, "cfg"), 1440, 900);
        shell2.ShowPrintingCommand.Execute(null);
        await UiHelpers.WaitUntil(() => shell2.Print.History.Entries.Count == 2);
        window2.Close();
        window.Close();
    }

    [AvaloniaFact]
    public async Task The_calibration_sheet_prints_through_the_same_path_at_the_chosen_size()
    {
        using var dir = new TempDir();
        var (window, shell) = UiHelpers.CreateWindow(Path.Combine(dir.Path, "cfg"), 1440, 900);
        shell.ShowPrintingCommand.Execute(null);
        shell.Print.LoadCalibrationCommand.Execute(null);
        Assert.True(shell.Print.HasItems);
        Assert.False(shell.Print.IsSizeFixed);
        shell.Print.SetSizeCommand.Execute(PrintFormats.P15x20);
        Assert.Equal("(6x8)", shell.Print.SelectedPaper!.Paper.Name);
        shell.Print.PrintCommand.Execute(null);
        await UiHelpers.WaitUntil(() => UiHelpers.Printer.Submitted.Count == 1);
        var page = UiHelpers.Printer.Submitted[0].Page;
        Assert.Equal((1800, 2400), (page.Width, page.Height));                   // the 15×20 sheet, 1:1 at 300 dpi
        window.Close();
    }

    [AvaloniaFact]
    public async Task Id_photos_default_to_3x4_with_eight_and_turn_by_90_degrees()
    {
        using var dir = new TempDir();
        var order = Order(dir, 1, 1200, 800);
        var (window, shell) = UiHelpers.CreateWindow(Path.Combine(dir.Path, "cfg"), 1440, 900);
        var id = shell.Id;
        Assert.Equal("3x4", id.SelectedSize.Size.Id);
        Assert.Equal(8, id.Copies);
        Assert.Equal("5 × 5 cm", id.Sizes.Single(s => s.Size.Id == "2x2in").Label);
        shell.ShowIdCommand.Execute(null);
        await id.LoadSourceAsync(Path.Combine(order, "P1.jpg"));
        await UiHelpers.WaitUntil(() => id.Preview is not null);
        var before = id.Framing!;

        id.RotateRightCommand.Execute(null);
        Assert.Equal(1, id.Framing!.QuarterTurns);
        Assert.Equal(IdFraming.Initial(1200, 800, id.SelectedSize.Size, 1), id.Framing);   // untouched framing: the start framing of the turned photo
        Assert.Equal(100, id.Zoom, 1);
        id.Tone.Exposure = 0.5;                                                // the shared sidebar
        Assert.True(id.Tone.CanUndo);
        id.ResetAdjustmentsCommand.Execute(null);                              // Reset keeps the framing
        Assert.Equal(0, id.Tone.Exposure);
        Assert.Equal(1, id.Framing!.QuarterTurns);
        id.UndoCommand.Execute(null);
        Assert.Equal(0.5, id.Tone.Exposure, 3);
        id.UndoCommand.Execute(null);
        Assert.Equal(0, id.Tone.Exposure);
        Assert.Equal(1, id.Framing!.QuarterTurns);
        id.UndoCommand.Execute(null);
        Assert.Equal(before, id.Framing);                                       // back before the turn
        var shown = id.Preview;
        id.RedoCommand.Execute(null);
        Assert.Equal(1, id.Framing!.QuarterTurns);
        Assert.Equal(0, id.Tone.Exposure);
        await UiHelpers.WaitUntil(() => id.Preview != shown);
        UiHelpers.Capture(window, "id-turned");

        id.ContinueToSheetCommand.Execute(null);
        Assert.True(id.IsSheetStep);
        id.PrintSheetCommand.Execute(null);
        Assert.True(shell.IsPrintWorkspace);
        Assert.Equal("ID Photos", shell.Print.OriginName);
        Assert.Contains("× 8", shell.Print.Previews.Single().Name);
        shell.Print.BackCommand.Execute(null);
        Assert.True(shell.IsIdWorkspace);
        window.Close();
    }

    [AvaloniaFact]
    public async Task Collage_photos_drop_into_frames_and_turn_from_the_menu()
    {
        using var dir = new TempDir();
        var order = Order(dir, 2);
        var (window, shell) = UiHelpers.CreateWindow(Path.Combine(dir.Path, "cfg"), 1440, 900);
        await shell.Browse.OpenFolderAsync(order);
        shell.ShowCollagesCommand.Execute(null);
        var c = shell.Collage;
        Assert.True(c.Picker.CanDrag);
        await c.DropAsync(1, Path.Combine(order, "P2.jpg"));                    // dropped on the second frame
        Assert.True(c.HasPhotoAt(1));
        Assert.False(c.HasPhotoAt(0));
        Assert.Equal(1, c.SelectedSlot);
        await c.DropAsync(1, Path.Combine(order, "P1.jpg"));                    // replaces it
        Assert.Equal(Path.Combine(order, "P1.jpg"), c.PathAt(1));

        await c.RotateRightCommand.ExecuteAsync(null);
        Assert.Equal(1, c.TurnsAt(1));
        await c.RotateLeftCommand.ExecuteAsync(null);
        await c.RotateLeftCommand.ExecuteAsync(null);
        Assert.Equal(3, c.TurnsAt(1));
        c.Swap(1, 0);                                                           // dragged onto another frame
        Assert.Equal(3, c.TurnsAt(0));
        UiHelpers.Capture(window, "collage-turned");
        window.Close();
    }

    [AvaloniaFact]
    public async Task Gallery_thumbnails_resize_and_the_size_is_remembered()
    {
        using var dir = new TempDir();
        var order = Order(dir, 6);
        var (window, shell) = UiHelpers.CreateWindow(Path.Combine(dir.Path, "cfg"), 1440, 900);
        await shell.Browse.OpenFolderAsync(order);
        Assert.Equal(176, shell.Browse.CardWidth);
        shell.Browse.ThumbScale = 1.5;
        Assert.Equal(264, shell.Browse.CardWidth);
        Assert.Equal(198, shell.Browse.ThumbHeight);
        UiHelpers.Capture(window, "browse-large-thumbnails");
        await UiHelpers.WaitUntil(() => File.Exists(AppServices().Paths.SettingsFile) && File.ReadAllText(AppServices().Paths.SettingsFile).Contains("\"thumbnailScale\": 1.5"));
        shell.Browse.ResetThumbScaleCommand.Execute(null);                      // double-click on the slider
        Assert.Equal(176, shell.Browse.CardWidth);
        Assert.Equal("Discard originals", Localization.Localizer.Instance["SaveChanges"]);
        window.Close();
    }

    private static Services.AppServices AppServices() => Services.AppServices.Current;

    /// <summary>Every workspace in dark and light mode, for the visual review (prompt_06 §10).</summary>
    [AvaloniaFact]
    public async Task Every_workspace_renders_in_dark_and_light_mode()
    {
        using var dir = new TempDir();
        var order = Order(dir, 5);
        var (window, shell) = UiHelpers.CreateWindow(Path.Combine(dir.Path, "cfg"), 1440, 900);
        try
        {
            await shell.Browse.OpenFolderAsync(order);
            await UiHelpers.WaitUntil(() => shell.Browse.AllPhotos.All(p => p.Thumbnail is not null));
            shell.Browse.SetSelection(shell.Browse.AllPhotos.Take(2));
            shell.Id.Picker.EnsureStarted(order);
            await shell.Id.LoadSourceAsync(Path.Combine(order, "P1.jpg"));
            await shell.Collage.DropAsync(0, Path.Combine(order, "P2.jpg"));
            await shell.Collage.DropAsync(1, Path.Combine(order, "P3.jpg"));
            shell.Collage.Picker.EnsureStarted(order);
            foreach (var theme in new[] { "dark", "light" })
            {
                AppServices().Settings.Theme = theme;
                App.ApplyTheme(theme);
                Assert.Equal(theme == "light" ? ThemeVariant.Light : ThemeVariant.Dark, Application.Current!.ActualThemeVariant);
                shell.ShowPhotosCommand.Execute(null);
                UiHelpers.Capture(window, $"{theme}-photos");
                shell.Browse.EditPhotoCommand.Execute(shell.Browse.AllPhotos[0]);
                await UiHelpers.WaitUntil(() => shell.Editor is { Preview: not null, IsLoading: false });
                UiHelpers.Capture(window, $"{theme}-editor");
                shell.Editor.BackToFolderCommand.Execute(null);
                shell.ShowIdCommand.Execute(null);
                await UiHelpers.WaitUntil(() => shell.Id.Preview is not null && shell.Id.Picker.Photos.Count == 5);
                UiHelpers.Capture(window, $"{theme}-id");
                shell.Id.ContinueToSheetCommand.Execute(null);
                await UiHelpers.WaitUntil(() => shell.Id.SheetPreview is not null);
                UiHelpers.Capture(window, $"{theme}-id-sheet");
                shell.Id.BackToFramingCommand.Execute(null);
                shell.ShowCollagesCommand.Execute(null);
                await UiHelpers.WaitUntil(() => shell.Collage.Picker.Photos.Count == 5);
                UiHelpers.Capture(window, $"{theme}-collage");
                shell.PrintPhotos(shell.Browse.AllPhotos.Take(3).ToList());
                await UiHelpers.WaitUntil(() => shell.Print.Previews.All(p => p.Page is not null));
                UiHelpers.Capture(window, $"{theme}-printing");
                shell.Print.ClearCommand.Execute(null);
                shell.OpenSettingsCommand.Execute(null);
                UiHelpers.Capture(window, $"{theme}-settings");
                shell.Settings!.CloseCommand.Execute(null);
            }
        }
        finally
        {
            App.ApplyTheme("dark");
            window.Close();
        }
    }
}
