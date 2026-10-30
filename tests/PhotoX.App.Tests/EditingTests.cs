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
        await shell.Editor.AutoCorrectCommand.ExecuteAsync(null);
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

    [AvaloniaFact]
    public async Task Finish_order_erase_by_default_after_saving_pending_edits()
    {
        using var dir = new TempDir();
        var (window, shell, order) = await OpenEditor(dir);
        shell.Editor.Tone.ExposurePlusCommand.Execute(null);
        await UiHelpers.WaitUntil(() => shell.Editor.IsDirty);
        Assert.True(await shell.Editor.SaveCurrentAsync());          // P1 saved
        await shell.Editor.NextAsync();
        shell.Editor.Tone.ExposureMinusCommand.Execute(null);             // P2 unsaved manual edit
        shell.Editor.BackToFolderCommand.Execute(null);

        var finish = shell.FinishOrderAsync();
        await UiHelpers.WaitUntil(() => shell.Dialog is not null);
        Assert.Contains("P2.jpg", shell.Dialog!.Message);           // unsaved edits are not discarded silently
        UiHelpers.Capture(window, "finish-unsaved");
        shell.Dialog.PressCommand.Execute("save");
        await UiHelpers.WaitUntil(() => shell.Dialog is { HasChoices: true });
        Assert.Equal("erase", shell.Dialog!.SelectedChoice!.Id);     // erase is the default
        Assert.True(shell.Dialog.Choices[0].IsSelected);
        UiHelpers.Capture(window, "finish-confirm");
        shell.Dialog.PressCommand.Execute("finish");
        await UiHelpers.WaitUntil(() => shell.Dialog is { HasChoices: false, Buttons.Count: 1 });
        shell.Dialog!.PressCommand.Execute("ok");
        await finish;

        Assert.False(Directory.Exists(Path.Combine(order, "Original")));
        using (var p2 = new MagickImage(Path.Combine(order, "P2.jpg")))
        {
            Assert.Equal(800u, p2.Width); // finished photo kept (cropped to 3:2)
        }

        Assert.Equal(3, shell.Browse.AllPhotos.Count);              // every photo is still in the gallery
        Assert.All(shell.Browse.AllPhotos, p => Assert.Equal(PhotoState.ToDo, p.State));
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

    [AvaloniaFact]
    public async Task Finish_order_keep_deletes_nothing()
    {
        using var dir = new TempDir();
        var (window, shell, order) = await OpenEditor(dir, 3);
        Directory.CreateDirectory(Path.Combine(order, "Original"));
        File.WriteAllText(Path.Combine(order, "Original", "P1.jpg"), "customer's own file");
        Assert.True(await shell.Editor.SaveCurrentAsync());
        await shell.Editor.NextAsync();
        Assert.True(await shell.Editor.SaveCurrentAsync());
        shell.Editor.BackToFolderCommand.Execute(null);
        var before = Directory.GetFiles(order, "*", SearchOption.AllDirectories).ToDictionary(f => f, Fixtures.Sha);

        var finish = shell.FinishOrderAsync();
        await UiHelpers.WaitUntil(() => shell.Dialog is { HasChoices: true });
        shell.Dialog!.SelectedChoice = shell.Dialog.Choices.Single(c => c.Id == "keep");
        shell.Dialog.PressCommand.Execute("finish");
        await UiHelpers.WaitUntil(() => shell.Dialog is { HasChoices: false });
        shell.Dialog!.PressCommand.Execute("ok");
        await finish;

        Assert.Equal(before, Directory.GetFiles(order, "*", SearchOption.AllDirectories).ToDictionary(f => f, Fixtures.Sha));
        Assert.Equal(3, shell.Browse.AllPhotos.Count);
        Assert.Equal(2, shell.Browse.EditedCount);
        Assert.Equal("customer's own file", File.ReadAllText(Path.Combine(order, "Original", "P1.jpg")));
        Assert.True(File.Exists(Path.Combine(order, "Original", "P1 (2).jpg")));
        window.Close();
    }
}
