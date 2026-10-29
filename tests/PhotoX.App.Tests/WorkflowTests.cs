using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using ImageMagick;
using PhotoX.App.Localization;
using PhotoX.App.ViewModels;
using PhotoX.Core.Orders;
using PhotoX.Storage;
using PhotoX.Tests;

namespace PhotoX.App.Tests;

/// <summary>TS-07 / TS-18: the vertical workflow through the real UI, headless.</summary>
public class WorkflowTests
{
    private static string MakeOrder(TempDir dir, int count = 5)
    {
        var order = Path.Combine(dir.Path, "Comenzi", "Popescu Maria");
        Directory.CreateDirectory(order);
        for (var i = 1; i <= count; i++)
        {
            Fixtures.WriteJpeg(order, $"IMG_{i:000}.jpg", i % 2 == 0 ? 600 : 800, i % 2 == 0 ? 800 : 600);
        }

        return order;
    }

    [AvaloniaFact]
    public async Task Open_crop_adjust_enter_saves_exact_crop_and_opens_next()
    {
        using var dir = new TempDir();
        var order = MakeOrder(dir);
        var (window, shell) = UiHelpers.CreateWindow(Path.Combine(dir.Path, "cfg"));
        shell.Browse.OpenFolder(order);
        await UiHelpers.WaitUntil(() => shell.Browse.AllPhotos.All(p => p.Thumbnail is not null));
        UiHelpers.Capture(window, "browse-1366x768");

        // Enter in Browse starts editing the first photo to do.
        UiHelpers.Tap(window, PhysicalKey.Enter);
        await UiHelpers.WaitUntil(() => shell.CurrentPage is EditorViewModel { Preview: not null, IsLoading: false });
        var editor = shell.Editor;
        Assert.Equal("IMG_001.jpg", editor.Current!.Name);
        Assert.False(editor.IsDirty); // default crop is not an unsaved manual edit

        // Default 10×15 crop on a 800×600 landscape photo: largest centred 3:2 rectangle.
        Assert.Equal(1.0, editor.Recipe.Crop.Width, 6);
        Assert.Equal(600.0 / (800 / 1.5), 1 / editor.Recipe.Crop.Height, 3);

        // The operator moves and shrinks the crop, then raises exposure.
        var pw = editor.PreviewWidth;
        var ph = editor.PreviewHeight;
        editor.CommitCrop(new Avalonia.Rect(pw * 0.30, ph * 0.20, pw * 0.45, pw * 0.45 / 1.5));
        editor.ExposurePlusCommand.Execute(null);
        Assert.True(editor.IsDirty);
        var selected = editor.Recipe.Crop;
        UiHelpers.Capture(window, "editor-1366x768");

        UiHelpers.Tap(window, PhysicalKey.Enter);
        await UiHelpers.WaitUntil(() => editor.Current!.Name == "IMG_002.jpg" && !editor.IsBusy);

        // The saved file has exactly the selected crop and the original is preserved.
        var saved = Path.Combine(order, "IMG_001.jpg");
        using (var img = new MagickImage(saved))
        {
            Assert.Equal((uint)Math.Round((selected.X + selected.Width) * 800) - (uint)Math.Round(selected.X * 800), img.Width);
            Assert.Equal((uint)Math.Round((selected.Y + selected.Height) * 600) - (uint)Math.Round(selected.Y * 600), img.Height);
        }

        Assert.True(File.Exists(Path.Combine(order, "Original", "IMG_001.jpg")));
        Assert.Equal(PhotoState.Edited, shell.Browse.AllPhotos[0].State);
        var record = new OrderStore(order).Load().Find("IMG_001.jpg")!;
        Assert.Equal(selected, record.Recipe!.Crop);

        // Reopening restores the saved crop and exposure (not the default crop).
        await editor.PreviousAsync();
        Assert.Equal(selected, editor.Recipe.Crop);
        Assert.Equal(1.0 / 6.0, editor.Recipe.Exposure, 3);
        window.Close();
    }

    [AvaloniaFact]
    public async Task Skip_never_saves_and_keeps_manual_edits_as_unsaved()
    {
        using var dir = new TempDir();
        var order = MakeOrder(dir, 3);
        var hash = Fixtures.Sha(Path.Combine(order, "IMG_001.jpg"));
        var (window, shell) = UiHelpers.CreateWindow(Path.Combine(dir.Path, "cfg"));
        shell.Browse.OpenFolder(order);
        shell.Browse.StartEditingCommand.Execute(null);
        await UiHelpers.WaitUntil(() => shell.Editor is { Preview: not null, IsLoading: false });

        // Skipping with only the default crop writes nothing and leaves the photo "to do".
        UiHelpers.Tap(window, PhysicalKey.ArrowRight);
        await UiHelpers.WaitUntil(() => shell.Editor.Current!.Name == "IMG_002.jpg" && !shell.Editor.IsBusy);
        Assert.Equal(hash, Fixtures.Sha(Path.Combine(order, "IMG_001.jpg")));
        Assert.Equal(PhotoState.ToDo, shell.Browse.AllPhotos[0].State);

        // A manual edit followed by Next is kept in memory as unsaved, not written.
        shell.Editor.ExposureMinusCommand.Execute(null);
        UiHelpers.Tap(window, PhysicalKey.ArrowRight);
        await UiHelpers.WaitUntil(() => shell.Editor.Current!.Name == "IMG_003.jpg" && !shell.Editor.IsBusy);
        Assert.Equal(PhotoState.Unsaved, shell.Browse.AllPhotos[1].State);
        Assert.False(Directory.Exists(Path.Combine(order, "Original")));

        // Last photo: Next does not wrap around.
        UiHelpers.Tap(window, PhysicalKey.ArrowRight);
        await UiHelpers.WaitUntil(() => !shell.Editor.IsBusy);
        Assert.Equal("IMG_003.jpg", shell.Editor.Current!.Name);
        window.Close();
    }

    [AvaloniaFact]
    public async Task Save_failure_keeps_photo_and_edits_and_does_not_advance()
    {
        using var dir = new TempDir();
        var order = MakeOrder(dir, 2);
        var (window, shell) = UiHelpers.CreateWindow(Path.Combine(dir.Path, "cfg"));
        shell.Browse.OpenFolder(order);
        shell.Browse.StartEditingCommand.Execute(null);
        await UiHelpers.WaitUntil(() => shell.Editor is { Preview: not null, IsLoading: false });
        shell.Editor.ExposurePlusCommand.Execute(null);

        File.SetAttributes(Path.Combine(order, "IMG_001.jpg"), FileAttributes.ReadOnly);
        UiHelpers.Tap(window, PhysicalKey.Enter);
        await UiHelpers.WaitUntil(() => !shell.Editor.IsBusy && shell.Editor.HasError);

        Assert.Equal("IMG_001.jpg", shell.Editor.Current!.Name);
        Assert.True(shell.Editor.IsDirty);
        Assert.Equal(Localizer.Instance["ErrReadOnlyFile"], shell.Editor.ErrorMessage);
        UiHelpers.Capture(window, "editor-save-error");
        File.SetAttributes(Path.Combine(order, "IMG_001.jpg"), FileAttributes.Normal);
        window.Close();
    }

    [AvaloniaFact]
    public async Task Held_enter_auto_repeat_saves_only_once()
    {
        using var dir = new TempDir();
        var order = MakeOrder(dir, 4);
        var (window, shell) = UiHelpers.CreateWindow(Path.Combine(dir.Path, "cfg"));
        shell.Browse.OpenFolder(order);
        shell.Browse.StartEditingCommand.Execute(null);
        await UiHelpers.WaitUntil(() => shell.Editor is { Preview: not null, IsLoading: false });

        // Key down three times without key up = auto-repeat.
        window.KeyPress(Key.Enter, RawInputModifiers.None, PhysicalKey.Enter, null);
        window.KeyPress(Key.Enter, RawInputModifiers.None, PhysicalKey.Enter, null);
        window.KeyPress(Key.Enter, RawInputModifiers.None, PhysicalKey.Enter, null);
        await UiHelpers.WaitUntil(() => shell.Editor.Current!.Name == "IMG_002.jpg" && !shell.Editor.IsBusy);
        await Task.Delay(200);
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        Assert.Equal("IMG_002.jpg", shell.Editor.Current!.Name);
        Assert.Single(new OrderStore(order).Load().Photos);
        window.KeyRelease(Key.Enter, RawInputModifiers.None, PhysicalKey.Enter, null);
        window.Close();
    }
}
