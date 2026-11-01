using Avalonia.Headless.XUnit;
using Avalonia.Input;
using ImageMagick;
using PhotoX.App.Services;
using PhotoX.App.ViewModels;
using PhotoX.Core.Collage;
using PhotoX.Core.Geometry;
using PhotoX.Core.Id;
using PhotoX.Tests;

namespace PhotoX.App.Tests;

/// <summary>prompt_04: Collages, the shared explorer, Duplicate image, ID sizes and editor refinements.</summary>
public class Prompt04Tests
{
    private static string Order(TempDir dir, int count, int w = 900, int h = 600)
    {
        var order = Path.Combine(dir.Path, "order");
        Directory.CreateDirectory(order);
        for (var i = 1; i <= count; i++) Fixtures.WriteJpeg(order, $"P{i}.jpg", i % 2 == 0 ? h : w, i % 2 == 0 ? w : h);
        return order;
    }

    [AvaloniaFact]
    public async Task Window_title_shows_the_version()
    {
        using var dir = new TempDir();
        var (window, shell) = UiHelpers.CreateWindow(Path.Combine(dir.Path, "cfg"));
        Assert.Matches(@"^PhotoX \d+\.\d+\.\d+$", window.Title);
        Assert.Equal($"PhotoX {shell.Version}", window.Title);
        window.Close();
        await Task.CompletedTask;
    }

    [AvaloniaFact]
    public async Task Duplicate_image_copies_next_to_the_photo_and_never_overwrites()
    {
        using var dir = new TempDir();
        var order = Order(dir, 2);
        File.WriteAllText(Path.Combine(order, "P1_copy.jpg.txt"), "unrelated"); // not a photo, not touched
        var (window, shell) = UiHelpers.CreateWindow(Path.Combine(dir.Path, "cfg"));
        await shell.Browse.OpenFolderAsync(order);
        var p1 = shell.Browse.AllPhotos.Single(p => p.Name == "P1.jpg");
        var original = Fixtures.Sha(p1.Path);

        await shell.Browse.DuplicateCommand.ExecuteAsync(p1);
        await shell.Browse.DuplicateCommand.ExecuteAsync(p1);

        Assert.Equal(original, Fixtures.Sha(Path.Combine(order, "P1_copy.jpg")));
        Assert.Equal(original, Fixtures.Sha(Path.Combine(order, "P1_copy_2.jpg")));
        Assert.Equal(original, Fixtures.Sha(p1.Path));
        Assert.Equal(["P1.jpg", "P1_copy.jpg", "P1_copy_2.jpg", "P2.jpg"], shell.Browse.AllPhotos.Select(p => p.Name));
        Assert.Equal("P1_copy_2.jpg", shell.Browse.SelectedPhoto?.Name);
        Assert.All(shell.Browse.AllPhotos, p => Assert.Equal(PhotoState.ToDo, p.State));
        window.Close();
    }

    [AvaloniaFact]
    public async Task Editor_orientation_switch_gives_the_largest_crop_and_reset_restores_it()
    {
        using var dir = new TempDir();
        var order = Order(dir, 1, 900, 600);
        var (window, shell) = UiHelpers.CreateWindow(Path.Combine(dir.Path, "cfg"));
        await shell.Browse.OpenFolderAsync(order);
        shell.Browse.StartEditingCommand.Execute(null);
        var editor = shell.Editor;
        await UiHelpers.WaitUntil(() => editor is { Preview: not null, IsLoading: false });

        // Shrink the crop, then switch: the new orientation gets its largest crop, and so does switching back.
        editor.CommitCrop(new Avalonia.Rect(100, 100, 300, 200));
        editor.SetOrientationCommand.Execute(CropOrientation.Portrait);
        Assert.Equal(1.0, editor.Recipe.Crop.Height, 3);                // 2:3 portrait in a 3:2 photo: full height
        editor.SetOrientationCommand.Execute(CropOrientation.Landscape);
        Assert.Equal(1.0, editor.Recipe.Crop.Width, 3);                 // the whole 3:2 photo
        Assert.Equal(1.0, editor.Recipe.Crop.Height, 3);

        // + / − keys change exposure in 1/6 EV steps; Reset clears tone adjustments and, since prompt_07, the crop too.
        editor.CommitCrop(new Avalonia.Rect(50, 40, 300, 200));
        var crop = editor.Recipe.Crop;
        Assert.True(editor.HandleKey(Key.OemPlus, KeyModifiers.None));
        Assert.True(editor.HandleKey(Key.Add, KeyModifiers.None));
        Assert.Equal(2 / 6.0, editor.Recipe.Exposure, 3);
        Assert.True(editor.HandleKey(Key.OemMinus, KeyModifiers.None));
        Assert.Equal(1 / 6.0, editor.Recipe.Exposure, 3);
        editor.Tone.Contrast = 30;
        editor.Tone.LevelsBlack = 0.1;
        editor.ResetAdjustmentsCommand.Execute(null);
        Assert.False(editor.Recipe.HasToneAdjustments);
        Assert.NotEqual(crop, editor.Recipe.Crop);
        Assert.Equal(new Core.Geometry.NormalizedRect(0, 0, 1, 1), editor.Recipe.Crop);   // the largest 10×15 crop of a 3:2 photo
        editor.UndoCommand.Execute(null);                              // one undo step brings them all back
        Assert.Equal(crop, editor.Recipe.Crop);
        Assert.Equal(30, editor.Recipe.Contrast);
        Assert.Equal(0.1, editor.Recipe.LevelsBlack, 3);
        window.Close();
    }

    /// <summary>prompt_05 §7: the explorer sits under the formats; a click on a photo loads it.</summary>
    [AvaloniaFact]
    public async Task Id_photos_choose_the_portrait_with_the_built_in_explorer()
    {
        using var dir = new TempDir();
        var order = Order(dir, 3, 600, 800);
        var (window, shell) = UiHelpers.CreateWindow(Path.Combine(dir.Path, "cfg"));
        await shell.Browse.OpenFolderAsync(order);
        shell.ShowIdCommand.Execute(null);
        var id = shell.Id;
        Assert.False(id.HasSource);
        await UiHelpers.WaitUntil(() => id.Picker.Photos.Count == 3 && id.Picker.Photos.All(p => p.Thumbnail is not null));
        Assert.Equal(order, id.Picker.Explorer.CurrentFolder);        // starts in the order open in Photos
        Assert.Null(id.Picker.SelectedPhoto);                         // nothing loads until the operator clicks
        UiHelpers.Capture(window, "id-explorer-1366x768");

        id.Picker.SelectedPhoto = id.Picker.Photos[1];                 // a click on a photo
        await UiHelpers.WaitUntil(() => id.Preview is not null && id.HasSource);
        Assert.Equal("P2.jpg", id.SourceName);
        Assert.True(id.ShowFraming);
        window.Close();
    }

    [AvaloniaFact]
    public async Task Id_sizes_frame_with_their_ratio_custom_sizes_are_remembered_and_9x12_has_no_guides()
    {
        using var dir = new TempDir();
        var order = Order(dir, 1, 600, 800);
        var (window, shell) = UiHelpers.CreateWindow(Path.Combine(dir.Path, "cfg"));
        var id = shell.Id;
        await id.LoadSourceAsync(Path.Combine(order, "P1.jpg"));

        id.SelectSizeCommand.Execute(id.Sizes.Single(s => s.Size.Id == "2x2in"));
        Assert.Equal(IdViewModel.PreviewFrameHeight, id.PreviewFrameWidth, 6);   // square frame for 2 × 2 in
        Assert.Equal(0m, id.MarginMm);                                          // four fit only edge to edge
        Assert.Equal(4, id.Copies);
        Assert.True(id.IsEdgeToEdge);
        id.SelectSizeCommand.Execute(id.Sizes.Single(s => s.Size.Id == "3x4"));
        Assert.Equal(IdViewModel.PreviewFrameHeight * 3 / 4, id.PreviewFrameWidth, 6);
        Assert.Equal(3m, id.MarginMm);
        Assert.True(id.HasGuides);
        id.SelectSizeCommand.Execute(id.Sizes.Single(s => s.Size.Id == "9x12"));
        Assert.False(id.HasGuides);
        Assert.False(id.CanAutoAlign);

        // + New size: centimetres, kept apart from the built-in sizes, remembered, removable.
        id.NewSizeWidth = 2.5m;
        id.NewSizeHeight = 3m;
        id.AddCustomSizeCommand.Execute(null);
        var custom = Assert.Single(id.CustomSizes);
        Assert.Equal("2.5 × 3 cm", custom.Label);
        Assert.True(custom.IsSelected);
        Assert.Equal(25.0 / 30.0 * IdViewModel.PreviewFrameHeight, id.PreviewFrameWidth, 6);
        Assert.Single(AppServices.Current.Settings.CustomIdSizes);
        Assert.DoesNotContain(id.Sizes, s => s.Size.IsCustom);
        id.NewSizeWidth = 0.5m;
        id.AddCustomSizeCommand.Execute(null);
        Assert.NotNull(id.NewSizeError);
        id.RemoveCustomSizeCommand.Execute(custom);
        Assert.Empty(id.CustomSizes);
        Assert.Empty(AppServices.Current.Settings.CustomIdSizes);
        Assert.False(custom.IsSelected && id.SelectedSize == custom);
        window.Close();
    }

    [AvaloniaFact]
    public async Task Id_space_compares_auto_adjust_is_undoable_and_sliders_reset()
    {
        using var dir = new TempDir();
        var order = Order(dir, 1, 600, 800);
        var (window, shell) = UiHelpers.CreateWindow(Path.Combine(dir.Path, "cfg"));
        shell.ShowIdCommand.Execute(null);
        var id = shell.Id;
        await id.LoadSourceAsync(Path.Combine(order, "P1.jpg"));
        await UiHelpers.WaitUntil(() => id.Preview is not null);

        Assert.True(id.HandleKey(Key.Space, KeyModifiers.None));
        Assert.True(id.ShowBefore);
        Assert.True(id.HandleKeyUp(Key.Space));
        Assert.False(id.ShowBefore);

        await UiHelpers.WaitUntil(() => id.Tone.Histogram is not null);
        await id.AutoAdjustCommand.ExecuteAsync(null);
        var adjusted = id.Adjustments.Tone;
        Assert.True(adjusted.HasToneAdjustments);
        id.UndoCommand.Execute(null);
        Assert.False(id.Adjustments.Tone.HasToneAdjustments);

        id.RotateBy(5);
        id.ZoomBy(1.5);
        id.ResetRotation();
        id.ResetZoom();
        Assert.Equal(0, id.Rotation);
        Assert.Equal(100, id.Zoom, 1);
        window.Close();
    }

    [AvaloniaFact]
    public async Task Collage_places_frames_swaps_and_saves_a_print_ready_jpg()
    {
        using var dir = new TempDir();
        var order = Order(dir, 4);
        var (window, shell) = UiHelpers.CreateWindow(Path.Combine(dir.Path, "cfg"));
        await shell.Browse.OpenFolderAsync(order);
        shell.ShowCollagesCommand.Execute(null);
        var c = shell.Collage;
        await UiHelpers.WaitUntil(() => c.Picker.Photos.Count == 4);

        c.SetTemplateCommand.Execute(c.Templates.Single(t => t.Template.Id == "1-big-2-below"));
        foreach (var p in c.Picker.Photos.Take(3)) await c.PlaceAsync(p.Path);
        Assert.Equal(3, c.Scene!.Photos.Count(p => p is not null));
        UiHelpers.Capture(window, "collage-1366x768");

        // Framing inside a slot: zoom in, pan, and the crop stays inside the photo with the slot's ratio.
        c.SelectSlot(0);
        c.Zoom(0, 2);
        c.Pan(0, 5, 5);
        var crop = c.Scene!.Photos[0]!.Crop;
        Assert.InRange(crop.X, 0, 1 - crop.Width + 1e-9);
        Assert.InRange(crop.Y, 0, 1 - crop.Height + 1e-9);
        Assert.Equal(0, crop.X, 6); // panned fully to the left edge
        c.Swap(0, 2);
        Assert.Equal(2, c.SelectedSlot);

        c.OutputFolder = Path.Combine(dir.Path, "out");
        var path = await c.SaveAsync();
        Assert.NotNull(path);
        Assert.Matches(@"^Collage_\d{8}_\d{4}\.jpg$", Path.GetFileName(path!));
        using var img = new MagickImage(path!);
        Assert.Equal((1200u, 1800u), (img.Width, img.Height));          // 10×15 at 300 ppi
        Assert.Equal(300, img.Density.X, 0);
        using var pixels = img.GetPixels();
        var gap = pixels.GetPixel(600, 3).ToColor()!;                 // inside the 3 mm outer margin: white
        Assert.True(gap.R > 64000 && gap.G > 64000 && gap.B > 64000);
        var slot = CollageLayout.ToPixels(CollageLayout.Slots(c.Template, 101.6, 152.4, 3)[0], 300);
        var inside = pixels.GetPixel(slot.X + slot.Width / 2, slot.Y + slot.Height / 2).ToColor()!;
        Assert.False(inside.R > 64000 && inside.G > 64000 && inside.B > 64000); // photo content
        foreach (var p in Directory.GetFiles(order)) Assert.DoesNotContain("Collage_", p);
        window.Close();
    }
}
