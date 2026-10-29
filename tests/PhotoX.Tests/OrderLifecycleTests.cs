using PhotoX.Core.Editing;
using PhotoX.Core.Geometry;
using PhotoX.Storage;

namespace PhotoX.Tests;

/// <summary>TS-09 (Finish Order) and TS-11 (thumbnail cache).</summary>
public class OrderLifecycleTests
{
    private static EditRecipe Crop => new() { Orientation = CropOrientation.Landscape, Crop = new NormalizedRect(0.1, 0.1, 0.5, 0.5) };

    [Fact]
    public void Erase_removes_only_known_originals_history_temps_and_cache()
    {
        using var dir = new TempDir();
        var a = Fixtures.WriteJpeg(dir.Path, "a.jpg", 300, 200);
        var b = Fixtures.WriteJpeg(dir.Path, "b.jpg", 300, 200);
        var id = Fixtures.WriteJpeg(dir.Path, "ID_20261029_1845.jpg", 300, 200);
        var saver = new PhotoSaver();
        Assert.True(saver.Save(a, Crop, FileStamp.Of(a)!.Value).Succeeded);
        Assert.True(saver.Save(b, Crop, FileStamp.Of(b)!.Value).Succeeded);
        var finishedA = Fixtures.Sha(a);
        var unknown = Path.Combine(dir.Path, "Original", "customer-notes.txt");
        File.WriteAllText(unknown, "keep me");
        File.WriteAllText(Path.Combine(dir.Path, ".x.jpg.photox-tmp"), "stray");
        var cache = new ThumbnailCache(Path.Combine(dir.Path, "..", Path.GetFileName(dir.Path) + "-cache"));
        cache.Put(FolderScanner.Scan(dir.Path)[0], [1, 2, 3]);

        var result = OrderCleanup.Erase(dir.Path, cache);

        Assert.Equal(2, result.DeletedOriginals);
        Assert.Equal(1, result.DeletedTemporary);
        Assert.True(result.Complete);
        Assert.False(result.OriginalFolderRemoved); // unknown file keeps the folder
        Assert.Equal("keep me", File.ReadAllText(unknown));
        Assert.False(File.Exists(Path.Combine(dir.Path, "Original", "a.jpg")));
        Assert.False(File.Exists(Path.Combine(dir.Path, "Original", ".photox-order.json")));
        Assert.Equal(finishedA, Fixtures.Sha(a)); // finished photo kept
        Assert.True(File.Exists(id));             // ID export kept
        Assert.Null(cache.TryGet(FolderScanner.Scan(dir.Path)[0]));
        cache.Clear();
    }

    [Fact]
    public void Erase_removes_empty_original_folder_and_later_edits_start_from_saved_photo()
    {
        using var dir = new TempDir();
        var a = Fixtures.WriteJpeg(dir.Path, "a.jpg", 300, 200);
        Assert.True(new PhotoSaver().Save(a, Crop, FileStamp.Of(a)!.Value).Succeeded);
        var result = OrderCleanup.Erase(dir.Path, null);
        Assert.True(result.OriginalFolderRemoved);
        Assert.False(Directory.Exists(Path.Combine(dir.Path, "Original")));

        // A later save starts from the remaining (already cropped) photo and preserves it as the new original.
        var hash = Fixtures.Sha(a);
        Assert.True(new PhotoSaver().Save(a, Crop, FileStamp.Of(a)!.Value).Succeeded);
        Assert.Equal(hash, Fixtures.Sha(Path.Combine(dir.Path, "Original", "a.jpg")));
    }

    [Fact]
    public void Keep_mode_is_simply_not_calling_erase_and_nothing_changes()
    {
        using var dir = new TempDir();
        var a = Fixtures.WriteJpeg(dir.Path, "a.jpg", 300, 200);
        Assert.True(new PhotoSaver().Save(a, Crop, FileStamp.Of(a)!.Value).Succeeded);
        Assert.True(File.Exists(Path.Combine(dir.Path, "Original", "a.jpg")));
        Assert.True(File.Exists(Path.Combine(dir.Path, "Original", ".photox-order.json")));
    }

    [Fact]
    public void Thumbnail_cache_hits_invalidates_on_change_and_prunes_to_size()
    {
        using var dir = new TempDir();
        var a = Fixtures.WriteJpeg(dir.Path, "a.jpg", 300, 200);
        var cache = new ThumbnailCache(Path.Combine(dir.Path, "cache"), maxBytes: 2500);
        var entry = FolderScanner.Scan(dir.Path).Single();
        Assert.Null(cache.TryGet(entry));
        cache.Put(entry, new byte[1000]);
        Assert.Equal(1000, cache.TryGet(entry)!.Length);
        Assert.Null(cache.TryGet(entry with { Length = entry.Length + 1 })); // changed file → miss

        for (var i = 0; i < 5; i++) cache.Put(entry with { Length = i }, new byte[1000]);
        cache.Prune();
        Assert.True(cache.SizeBytes() <= 2500);
        cache.Clear();
        Assert.Equal(0, cache.SizeBytes());
    }
}
