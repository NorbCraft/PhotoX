using ImageMagick;
using PhotoX.Core.Editing;
using PhotoX.Core.Geometry;
using PhotoX.Core.Orders;
using PhotoX.Storage;

namespace PhotoX.Tests;

/// <summary>TS-07 / TS-08: saving exactly the selected crop, temporary originals, collisions and failures.</summary>
public class SaveTests
{
    private static EditRecipe Crop(double x, double y, double w, double h) =>
        new() { Orientation = CropOrientation.Landscape, Crop = new NormalizedRect(x, y, w, h) };

    [Fact]
    public void First_save_writes_exact_crop_and_preserves_original()
    {
        using var dir = new TempDir();
        var path = Fixtures.WritePng(dir.Path, "photo.png", 600, 400, depth: 16);
        var originalHash = Fixtures.Sha(path);
        var recipe = Crop(0.25, 0.1, 0.5, 0.5);

        var result = new PhotoSaver().Save(path, recipe, FileStamp.Of(path)!.Value);

        Assert.Equal(SaveOutcome.Saved, result.Outcome);
        using var saved = new MagickImage(path);
        Assert.Equal(300u, saved.Width);
        Assert.Equal(200u, saved.Height);
        // The saved top-left pixel encodes the source coordinate (150, 40): the crop was not moved or enlarged.
        using var px = saved.GetPixels();
        var c = px.GetPixel(0, 0).ToColor()!;
        Assert.InRange(c.R * 599.0 / 65535, 149.5, 150.5);
        Assert.InRange(c.G * 399.0 / 65535, 39.5, 40.5);

        var backup = Path.Combine(dir.Path, "Original", "photo.png");
        Assert.True(File.Exists(backup));
        Assert.Equal(originalHash, Fixtures.Sha(backup));
        var sidecar = new OrderStore(dir.Path).Load();
        Assert.Equal(SavedState.Edited, sidecar.Find("photo.png")!.State);
        Assert.Equal("photo.png", sidecar.Find("PHOTO.PNG")!.Backup);
        Assert.Empty(Directory.GetFiles(dir.Path, "*.photox-tmp", SearchOption.AllDirectories));
    }

    [Fact]
    public void Later_saves_keep_the_original_and_render_from_it()
    {
        using var dir = new TempDir();
        var path = Fixtures.WriteJpeg(dir.Path, "IMG_1.jpg", 600, 400);
        var originalHash = Fixtures.Sha(path);
        var saver = new PhotoSaver();

        Assert.True(saver.Save(path, Crop(0, 0, 0.5, 0.5), FileStamp.Of(path)!.Value).Succeeded);
        var second = Crop(0.5, 0.5, 0.5, 0.5) with { Exposure = 0.5 };
        Assert.True(saver.Save(path, second, FileStamp.Of(path)!.Value).Succeeded);

        Assert.Equal(originalHash, Fixtures.Sha(Path.Combine(dir.Path, "Original", "IMG_1.jpg")));
        // The second crop lies outside the first one, so it can only come from the original.
        using var saved = new MagickImage(path);
        Assert.Equal(300u, saved.Width);
        Assert.Equal(200u, saved.Height);
        using var pixels = saved.GetPixels();
        var c = pixels.GetPixel(0, 0).ToColor()!;
        Assert.True(c.R > 30000, "top-left of the second crop must come from the right half of the original");
        Assert.Single(Directory.GetFiles(Path.Combine(dir.Path, "Original"), "*.jpg"));
    }

    [Fact]
    public void Unchanged_photo_is_marked_reviewed_without_writing()
    {
        using var dir = new TempDir();
        var path = Fixtures.WriteJpeg(dir.Path, "a.jpg", 600, 400); // already 3:2
        var hash = Fixtures.Sha(path);
        var result = new PhotoSaver().Save(path, Crop(0, 0, 1, 1), FileStamp.Of(path)!.Value);
        Assert.Equal(SaveOutcome.Reviewed, result.Outcome);
        Assert.Equal(hash, Fixtures.Sha(path));
        Assert.False(File.Exists(Path.Combine(dir.Path, "Original", "a.jpg")));
        Assert.Equal(SavedState.Reviewed, new OrderStore(dir.Path).Load().Find("a.jpg")!.State);
    }

    [Fact]
    public void Unknown_file_in_Original_is_never_overwritten()
    {
        using var dir = new TempDir();
        var path = Fixtures.WriteJpeg(dir.Path, "IMG_2.jpg", 600, 400);
        Directory.CreateDirectory(Path.Combine(dir.Path, "Original"));
        var foreign = Path.Combine(dir.Path, "Original", "img_2.JPG");
        File.WriteAllText(foreign, "unrelated content");
        var originalHash = Fixtures.Sha(path);

        var result = new PhotoSaver().Save(path, Crop(0.1, 0.1, 0.5, 0.5), FileStamp.Of(path)!.Value);

        Assert.Equal(SaveOutcome.Saved, result.Outcome);
        Assert.Equal("unrelated content", File.ReadAllText(foreign));
        var backup = Path.Combine(dir.Path, "Original", "IMG_2 (2).jpg");
        Assert.Equal(originalHash, Fixtures.Sha(backup));
        Assert.Equal("IMG_2 (2).jpg", new OrderStore(dir.Path).Load().Find("IMG_2.jpg")!.Backup);
    }

    [Fact]
    public void Changed_externally_is_refused_and_file_untouched()
    {
        using var dir = new TempDir();
        var path = Fixtures.WriteJpeg(dir.Path, "b.jpg", 600, 400);
        var stamp = FileStamp.Of(path)!.Value;
        File.SetLastWriteTimeUtc(path, DateTime.UtcNow.AddMinutes(-5));
        var hash = Fixtures.Sha(path);
        var result = new PhotoSaver().Save(path, Crop(0, 0, 0.5, 0.5), stamp);
        Assert.Equal(SaveError.ChangedExternally, result.Error);
        Assert.Equal(hash, Fixtures.Sha(path));
    }

    [Fact]
    public void Read_only_file_is_refused()
    {
        using var dir = new TempDir();
        var path = Fixtures.WriteJpeg(dir.Path, "c.jpg", 600, 400);
        File.SetAttributes(path, FileAttributes.ReadOnly);
        var result = new PhotoSaver().Save(path, Crop(0, 0, 0.5, 0.5), FileStamp.Of(path)!.Value);
        Assert.Equal(SaveError.ReadOnlyFile, result.Error);
        Assert.False(Directory.Exists(Path.Combine(dir.Path, "Original")));
    }

    [Fact]
    public void Read_only_folder_fails_cleanly()
    {
        if (OperatingSystem.IsWindows()) return; // folder permissions are tested on Linux
        using var dir = new TempDir();
        var path = Fixtures.WriteJpeg(dir.Path, "d.jpg", 600, 400);
        var hash = Fixtures.Sha(path);
        File.SetUnixFileMode(dir.Path, UnixFileMode.UserRead | UnixFileMode.UserExecute);
        try
        {
            var result = new PhotoSaver().Save(path, Crop(0, 0, 0.5, 0.5), FileStamp.Of(path)!.Value);
            Assert.Equal(SaveOutcome.Failed, result.Outcome);
            Assert.Equal(SaveError.AccessDenied, result.Error);
            Assert.Equal(hash, Fixtures.Sha(path));
        }
        finally
        {
            File.SetUnixFileMode(dir.Path, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        }
    }

    [Fact]
    public void Failure_before_replace_leaves_photo_and_order_unchanged()
    {
        using var dir = new TempDir();
        var path = Fixtures.WriteJpeg(dir.Path, "e.jpg", 600, 400);
        var hash = Fixtures.Sha(path);
        var saver = new PhotoSaver { BeforeReplace = _ => throw new IOException("simulated write failure") };
        var result = saver.Save(path, Crop(0, 0, 0.5, 0.5), FileStamp.Of(path)!.Value);
        Assert.Equal(SaveOutcome.Failed, result.Outcome);
        Assert.Equal(hash, Fixtures.Sha(path));
        Assert.Empty(Directory.GetFiles(dir.Path, "*.photox-tmp", SearchOption.AllDirectories));
        Assert.Null(new OrderStore(dir.Path).Load().Find("e.jpg"));
        Assert.False(File.Exists(Path.Combine(dir.Path, "Original", "e.jpg")));
    }

    [Fact]
    public void Missing_file_fails()
    {
        using var dir = new TempDir();
        var path = Fixtures.WriteJpeg(dir.Path, "f.jpg", 60, 40);
        var stamp = FileStamp.Of(path)!.Value;
        File.Delete(path);
        Assert.Equal(SaveError.FileMissing, new PhotoSaver().Save(path, Crop(0, 0, 0.5, 0.5), stamp).Error);
    }
}
