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
        shell.Browse.OpenFolder(order);
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
        Assert.NotNull(shell.Editor.Histogram);
        await shell.Editor.AutoCorrectCommand.ExecuteAsync(null);
        var r = shell.Editor.Recipe;
        Assert.InRange(r.Exposure, -1, 1);
        Assert.True(shell.Editor.CanUndo);
        shell.Editor.UndoCommand.Execute(null);
        Assert.Equal(0, shell.Editor.Recipe.Exposure);
        shell.Editor.Tint = 30;
        shell.Editor.Temperature = -20;
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
        shell.Editor.ExposurePlusCommand.Execute(null);
        await UiHelpers.WaitUntil(() => shell.Editor.IsDirty);
        Assert.True(await shell.Editor.SaveCurrentAsync());          // P1 saved
        await shell.Editor.NextAsync();
        shell.Editor.ExposureMinusCommand.Execute(null);             // P2 unsaved manual edit
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

        Assert.All(shell.Browse.AllPhotos, p => Assert.Equal(PhotoState.ToDo, p.State));
        window.Close();
    }

    [AvaloniaFact]
    public async Task Custom_ratio_is_applied_and_saved()
    {
        using var dir = new TempDir();
        var (window, shell, order) = await OpenEditor(dir, 1);
        shell.Editor.CustomWidth = 9;
        shell.Editor.CustomHeight = 13;
        shell.Editor.ApplyCustomRatioCommand.Execute(null);
        Assert.Equal("custom", shell.Editor.FormatId);
        Assert.True(await shell.Editor.SaveCurrentAsync());
        using var img = new MagickImage(Path.Combine(order, "P1.jpg"));
        Assert.Equal(13.0 / 9.0, img.Width / (double)img.Height, 2);
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
        shell.Browse.OpenFolder(order);
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
        var (window, shell, order) = await OpenEditor(dir, 1);
        Assert.True(await shell.Editor.SaveCurrentAsync());
        shell.Editor.BackToFolderCommand.Execute(null);
        var finish = shell.FinishOrderAsync();
        await UiHelpers.WaitUntil(() => shell.Dialog is { HasChoices: true });
        shell.Dialog!.SelectedChoice = shell.Dialog.Choices.Single(c => c.Id == "keep");
        shell.Dialog.PressCommand.Execute("finish");
        await UiHelpers.WaitUntil(() => shell.Dialog is { HasChoices: false });
        shell.Dialog!.PressCommand.Execute("ok");
        await finish;
        Assert.True(File.Exists(Path.Combine(order, "Original", "P1.jpg")));
        Assert.NotNull(new OrderStore(order).Load().Find("P1.jpg"));
        window.Close();
    }
}
