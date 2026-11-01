using Avalonia.Headless.XUnit;
using ImageMagick;
using PhotoX.App.ViewModels;
using PhotoX.App.Views;
using PhotoX.Core.Editing;
using PhotoX.Storage;
using PhotoX.Tests;

namespace PhotoX.App.Tests;

/// <summary>TS-01 / TS-04 / TS-09 through the UI: rotation, composition, 13×18, Auto Correct, Finish Order.</summary>
public class EditingTests
{
    private static async Task<(MainWindow Window, ShellViewModel Shell, string Order)> OpenEditor(TempDir dir, int count = 3, int w = 800, int h = 600)
    {
        var order = Path.Combine(dir.Path, "order");
        Directory.CreateDirectory(order);
        for (var i = 1; i <= count; i++) Fixtures.WriteJpeg(order, $"P{i}.jpg", w, h);
        var (window, shell) = UiHelpers.CreateWindow(Path.Combine(dir.Path, "cfg"));
        await shell.Browse.OpenFolderAsync(order);
        shell.Browse.StartEditingCommand.Execute(null);
        await UiHelpers.WaitUntil(() => shell.Editor is { Preview: not null, IsLoading: false });
        return ((MainWindow)window, shell, order);
    }

    [AvaloniaFact]
    public async Task Rotate_right_saves_portrait_output()
    {
        using var dir = new TempDir();
        var (window, shell, order) = await OpenEditor(dir);
        shell.Editor.RotateRightCommand.Execute(null);
        Assert.True(shell.Editor.IsPortrait);
        Assert.True(await shell.Editor.SaveCurrentAsync());
        using var img = new MagickImage(Path.Combine(order, "P1.jpg"));
        Assert.True(img.Height > img.Width);
        Assert.Equal(2.0 / 3.0, img.Width / (double)img.Height, 2);
        window.Close();
    }

    [AvaloniaFact]
    public async Task Whole_photo_with_borders_and_13x18_canvas_have_planned_sizes()
    {
        using var dir = new TempDir();
        var (window, shell, order) = await OpenEditor(dir);
        shell.Editor.SetCompositionCommand.Execute(Composition.WholeWithBorder);
        Assert.False(shell.Editor.ShowCrop);
        UiHelpers.Capture(window, "editor-borders");
        Assert.True(await shell.Editor.SaveCurrentAsync());
        using (var img = new MagickImage(Path.Combine(order, "P1.jpg")))
        {
            Assert.Equal((900u, 600u), (img.Width, img.Height)); // 800×600 → 3:2 canvas, no scaling
            using var px = img.GetPixels();
            var c = px.GetPixel(10, 300).ToColor()!; // left border is white
            Assert.True(c.R > 64000 && c.G > 64000 && c.B > 64000);
        }

        await shell.Editor.NextAsync();
        shell.Editor.SetFormatCommand.Execute("13x18");
        Assert.True(await shell.Editor.SaveCurrentAsync());
        var plan = RenderPlan.Create(800, 600, shell.Editor.Recipe);
        using (var img = new MagickImage(Path.Combine(order, "P2.jpg")))
        {
            Assert.Equal(((uint)plan.CanvasWidth, (uint)plan.CanvasHeight), (img.Width, img.Height));
            Assert.Equal(0.75, Math.Min(img.Width, img.Height) / (double)Math.Max(img.Width, img.Height), 2);
        }

        window.Close();
    }

    [AvaloniaFact]
    public async Task Auto_correct_sets_adjustable_values_and_histogram_is_live()
    {
        using var dir = new TempDir();
        var (window, shell, _) = await OpenEditor(dir);
        Assert.NotNull(shell.Editor.Tone.Histogram);
        await shell.Editor.AutoAdjustCommand.ExecuteAsync(null);
        var r = shell.Editor.Recipe;
        Assert.InRange(r.Exposure, -1, 1);
        Assert.True(shell.Editor.CanUndo);
        shell.Editor.UndoCommand.Execute(null);
        Assert.Equal(0, shell.Editor.Recipe.Exposure);
        shell.Editor.Tone.Tint = 30;
        shell.Editor.Tone.Temperature = -20;
        shell.Editor.Straighten = 3;
        await UiHelpers.WaitUntil(() => shell.Editor.Recipe.Straighten == 3);
        UiHelpers.Capture(window, "editor-adjusted-1366x768");
        Assert.True(await shell.Editor.SaveCurrentAsync());
        window.Close();
    }

    /// <summary>
    /// prompt_05 §5, prompt_08 §5: Discard Originals (for the whole folder) saves pending edits after a clear warning,
    /// then deletes only PhotoX's originals.
    /// </summary>
    [AvaloniaFact]
    public async Task Save_changes_saves_pending_edits_and_deletes_only_photox_originals()
    {
        using var dir = new TempDir();
        var (window, shell, order) = await OpenEditor(dir);
        shell.Editor.Tone.ExposurePlusCommand.Execute(null);
        await UiHelpers.WaitUntil(() => shell.Editor.IsDirty);
        Assert.True(await shell.Editor.SaveCurrentAsync());          // P1 saved
        await shell.Editor.NextAsync();
        shell.Editor.Tone.ExposureMinusCommand.Execute(null);        // P2 unsaved manual edit
        shell.Editor.BackToFolderCommand.Execute(null);
        File.WriteAllText(Path.Combine(order, "Original", "notes.txt"), "customer notes");

        var save = shell.DiscardOriginalsAsync();
        await UiHelpers.WaitUntil(() => shell.Dialog is not null);
        Assert.Contains("P2.jpg", shell.Dialog!.Message);           // the unsaved photo is named
        Assert.Contains("permanently", shell.Dialog.Message);       // and the deletion is explained
        Assert.Contains("whole folder", shell.Dialog.Message);
        UiHelpers.Capture(window, "save-changes-warning");
        shell.Dialog.PressCommand.Execute("discard");
        await UiHelpers.WaitUntil(() => shell.Dialog is { Buttons.Count: 1 });
        shell.Dialog!.PressCommand.Execute("ok");
        await save;

        Assert.Equal(["notes.txt"], Directory.GetFiles(Path.Combine(order, "Original")).Select(Path.GetFileName)); // only PhotoX's files went
        using (var p2 = new MagickImage(Path.Combine(order, "P2.jpg")))
        {
            Assert.Equal(800u, p2.Width); // the saved edit is kept (cropped to 3:2)
        }

        Assert.Equal(3, shell.Browse.AllPhotos.Count);              // every photo is still in the gallery
        Assert.All(shell.Browse.AllPhotos, p => Assert.Null(p.PendingRecipe));
        Assert.All(Directory.GetFiles(order), f => Assert.Equal(0, (int)(File.GetAttributes(f) & FileAttributes.Hidden)));
        window.Close();
    }

    /// <summary>prompt_04 §7: a 9×13 cm custom photo keeps its size and is centred on a white 10×15 sheet.</summary>
    [AvaloniaFact]
    public async Task Custom_size_is_centred_on_the_smallest_sheet()
    {
        using var dir = new TempDir();
        var (window, shell, order) = await OpenEditor(dir, 1);
        shell.Editor.CustomWidth = 9;
        shell.Editor.CustomHeight = 13;
        shell.Editor.ApplyCustomSizeCommand.Execute(null);
        Assert.Equal("custom", shell.Editor.FormatId);
        Assert.Equal(13.0 / 9.0, shell.Editor.CropRatio, 6); // the photo is landscape, so the crop is 13:9
        Assert.True(await shell.Editor.SaveCurrentAsync());
        using var img = new MagickImage(Path.Combine(order, "P1.jpg"));
        Assert.Equal(152.4 / 101.6, img.Width / (double)img.Height, 2); // the 10×15 sheet, landscape
        var plan = Core.Editing.RenderPlan.Create(800, 600, shell.Editor.Recipe);
        Assert.Equal(130.0 / 152.4, plan.Crop.Width / (double)plan.CanvasWidth, 2); // 13 cm of 15.24 cm: not enlarged
        Assert.Equal(90.0 / 101.6, plan.Crop.Height / (double)plan.CanvasHeight, 2);
        using var pixels = img.GetPixels();
        Assert.True(pixels.GetPixel(2, 2).ToColor()!.R > 250 * 257); // white around the photo
        window.Close();
    }

    [AvaloniaFact]
    public async Task Heic_file_is_listed_with_an_explanation()
    {
        using var dir = new TempDir();
        var order = Path.Combine(dir.Path, "order");
        Directory.CreateDirectory(order);
        File.WriteAllBytes(Path.Combine(order, "phone.heic"), [0, 0, 0, 24, (byte)'f', (byte)'t', (byte)'y', (byte)'p', (byte)'h', (byte)'e', (byte)'i', (byte)'c']);
        var (window, shell) = UiHelpers.CreateWindow(Path.Combine(dir.Path, "cfg"));
        await shell.Browse.OpenFolderAsync(order);
        var item = Assert.Single(shell.Browse.AllPhotos);
        Assert.Equal(PhotoState.UnsupportedHeic, item.State);
        Assert.Contains("JPEG", item.ErrorText);
        Assert.False(item.IsOpenable);
        window.Close();
        await Task.CompletedTask;
    }

    /// <summary>prompt_05 §5: cancelling Save Changes deletes nothing; Restore Original brings the original back after a warning.</summary>
    [AvaloniaFact]
    public async Task Cancelled_save_changes_deletes_nothing_and_restore_brings_the_original_back()
    {
        using var dir = new TempDir();
        var (window, shell, order) = await OpenEditor(dir, 3);
        Directory.CreateDirectory(Path.Combine(order, "Original"));
        File.WriteAllText(Path.Combine(order, "Original", "P1.jpg"), "customer's own file");
        var originalP1 = Fixtures.Sha(Path.Combine(order, "P1.jpg"));
        Assert.True(await shell.Editor.SaveCurrentAsync());
        await shell.Editor.NextAsync();
        Assert.True(await shell.Editor.SaveCurrentAsync());
        shell.Editor.BackToFolderCommand.Execute(null);
        var before = Directory.GetFiles(order, "*", SearchOption.AllDirectories).ToDictionary(f => f, Fixtures.Sha);

        var save = shell.DiscardOriginalsAsync();
        await UiHelpers.WaitUntil(() => shell.Dialog is not null);
        shell.Dialog!.PressCommand.Execute("cancel");
        await save;
        Assert.Equal(before, Directory.GetFiles(order, "*", SearchOption.AllDirectories).ToDictionary(f => f, Fixtures.Sha));

        var p1 = shell.Browse.AllPhotos.Single(p => p.Name == "P1.jpg");
        var restore = shell.RestoreOriginalAsync([p1]);
        await UiHelpers.WaitUntil(() => shell.Dialog is not null);
        Assert.Contains("P1.jpg", shell.Dialog!.Message);
        shell.Dialog.PressCommand.Execute("restore");
        await restore;

        Assert.Equal(originalP1, Fixtures.Sha(Path.Combine(order, "P1.jpg")));                         // the exact original
        Assert.Equal("customer's own file", File.ReadAllText(Path.Combine(order, "Original", "P1.jpg"))); // never touched
        Assert.False(File.Exists(Path.Combine(order, "Original", "P1 (2).jpg")));                      // PhotoX's copy went
        Assert.Equal(PhotoState.ToDo, p1.State);
        Assert.Equal(PhotoState.Edited, shell.Browse.AllPhotos.Single(p => p.Name == "P2.jpg").State);
        window.Close();
    }
}
