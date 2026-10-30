using Avalonia;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using ImageMagick;
using PhotoX.App.Controls;
using PhotoX.App.ViewModels;
using PhotoX.Tests;

namespace PhotoX.App.Tests;

/// <summary>prompt_03 §4: the applied crop shows the saved result; double-click applies without saving; undo works.</summary>
public class CropPreviewTests
{
    private static async Task<(Avalonia.Controls.Window Window, ShellViewModel Shell, string Order)> Open(TempDir dir, int count = 2)
    {
        var order = Path.Combine(dir.Path, "order");
        Directory.CreateDirectory(order);
        // The same textured photos on every run and platform: a random plasma made the preview comparison flaky
        // (one Windows CI run measured 4.5 %). A light blur keeps the structure but drops pixel-level noise, which
        // the screen-sized preview cannot show either way.
        MagickNET.SetRandomSeed(20261030);
        try
        {
            for (var i = 1; i <= count; i++)
            {
                using var image = new MagickImage("plasma:", new MagickReadSettings { Width = 900, Height = 600 });
                image.Blur(0, 1.5);
                image.Write(Path.Combine(order, $"P{i}.jpg"), MagickFormat.Jpeg);
            }
        }
        finally
        {
            MagickNET.ResetRandomSeed();
        }

        var (window, shell) = UiHelpers.CreateWindow(Path.Combine(dir.Path, "cfg"), 1366, 768);
        await shell.Browse.OpenFolderAsync(order);
        shell.Browse.StartEditingCommand.Execute(null);
        await UiHelpers.WaitUntil(() => shell.Editor is { Preview: not null, IsLoading: false });
        return (window, shell, order);
    }

    [AvaloniaFact]
    public async Task Saved_crop_is_shown_as_the_result_and_matches_the_file()
    {
        using var dir = new TempDir();
        var (window, shell, order) = await Open(dir);
        var editor = shell.Editor;
        editor.Straighten = 6;
        editor.CommitCrop(new Rect(editor.CropRect.X + 40, editor.CropRect.Y + 30, editor.CropRect.Width * 0.6, editor.CropRect.Height * 0.6));
        editor.Tone.Exposure = 0.5;
        editor.Tone.Saturation = 40;
        await UiHelpers.WaitUntil(() => !editor.IsBusy);
        Assert.True(editor.ShowCrop);

        Assert.True(await editor.SaveCurrentAsync());
        Assert.True(editor.CropApplied);
        Assert.True(editor.ShowResult);
        Assert.False(editor.ShowCrop);                       // the crop frame is gone
        await UiHelpers.WaitUntil(() => !editor.IsBusy);
        var shot = UiHelpers.Capture(window, "editor-crop-applied");

        // Compare the on-screen result with the saved photo at the same size.
        var canvas = window.GetVisualDescendants().OfType<PhotoCanvas>().Single();
        var dest = canvas.ImageDestination();
        var origin = canvas.TranslatePoint(new Point(dest.X, dest.Y), window)!.Value;
        using var screen = new MagickImage(shot);
        screen.Crop(new MagickGeometry((int)Math.Ceiling(origin.X) + 2, (int)Math.Ceiling(origin.Y) + 2, (uint)dest.Width - 4, (uint)dest.Height - 4));
        screen.ResetPage();
        using var saved = new MagickImage(Path.Combine(order, "P1.jpg"));
        Assert.Equal(dest.Width / dest.Height, saved.Width / (double)saved.Height, 2); // fitted with the output's ratio
        saved.Resize(new MagickGeometry((uint)dest.Width, (uint)dest.Height) { IgnoreAspectRatio = true });
        saved.Crop(new MagickGeometry(2, 2, screen.Width, screen.Height));
        saved.ResetPage();
        screen.ColorSpace = ColorSpace.sRGB;
        screen.Alpha(AlphaOption.Off);
        var error = saved.Compare(screen, ErrorMetric.MeanAbsolute);
        // A misplaced crop is what this guards against: compare with the saved photo shifted by 6 % of its width.
        // Resampling differs between platforms (1.3 % on Linux, up to 4.5 % on Windows CI), so the check is relative.
        using var shifted = (MagickImage)saved.Clone();
        var dx = (int)(saved.Width * 0.06);
        shifted.Crop(new MagickGeometry(dx, 0, saved.Width - (uint)dx, saved.Height));
        shifted.ResetPage();
        shifted.Extent(saved.Width, saved.Height, Gravity.East);
        var misplaced = shifted.Compare(screen, ErrorMetric.MeanAbsolute);
        Assert.True(error < 0.07 && error < misplaced * 0.6, $"preview differs from the saved photo by {error:P1} (a 6 % shift: {misplaced:P1})");
        window.Close();
    }

    [AvaloniaFact]
    public async Task Double_click_applies_the_crop_without_saving_and_undo_returns_to_the_frame()
    {
        using var dir = new TempDir();
        var (window, shell, order) = await Open(dir);
        var editor = shell.Editor;
        var before = Fixtures.Sha(Path.Combine(order, "P1.jpg"));
        editor.ApplyCropCommand.Execute(null);               // what a double-click inside the frame does
        Assert.True(editor.ShowResult);
        Assert.False(editor.ShowCrop);
        Assert.Equal(before, Fixtures.Sha(Path.Combine(order, "P1.jpg")));
        Assert.False(Directory.Exists(Path.Combine(order, "Original")));

        editor.Tone.Exposure = 0.3;                          // tone changes keep the applied view
        await UiHelpers.WaitUntil(() => editor.Recipe.Exposure > 0);
        Assert.True(editor.CropApplied);
        editor.UndoCommand.Execute(null);
        editor.UndoCommand.Execute(null);
        Assert.False(editor.CropApplied);
        Assert.True(editor.ShowCrop);

        editor.ApplyCropCommand.Execute(null);
        editor.SetFormatCommand.Execute("15x20");            // geometry changes return to the crop frame
        Assert.True(editor.ShowCrop);

        await editor.NextAsync();
        Assert.False(editor.CropApplied);                    // the next photo opens with the crop tool
        window.Close();
    }

    [AvaloniaFact]
    public async Task Save_and_next_becomes_close_when_the_order_is_done()
    {
        using var dir = new TempDir();
        var (window, shell, _) = await Open(dir);
        var editor = shell.Editor;
        Assert.False(editor.IsCloseMode);
        await editor.SaveAndNextAsync();
        await UiHelpers.WaitUntil(() => editor.Current!.Name == "P2.jpg" && !editor.IsBusy);
        await editor.SaveAndNextAsync();                     // last photo: saved, stays, button turns into Close
        await UiHelpers.WaitUntil(() => !editor.IsBusy);
        Assert.True(editor.IsCloseMode);
        Assert.Equal("P2.jpg", editor.Current!.Name);
        UiHelpers.Capture(window, "editor-close-mode");
        await editor.SaveAndNextAsync();                     // Close: back to the gallery
        Assert.IsType<BrowseViewModel>(shell.CurrentPage);
        Assert.All(shell.Browse.AllPhotos, p => Assert.True(p.IsDone));
        window.Close();
    }
}
