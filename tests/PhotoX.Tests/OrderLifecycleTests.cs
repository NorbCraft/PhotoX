using PhotoX.Core.Editing;
using PhotoX.Core.Geometry;
using PhotoX.Core.Orders;
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

    /// <summary>
    /// An order with three photos (two edited, one reviewed), a customer's own <c>Original/P1.jpg</c> from before
    /// PhotoX, unrelated files in <c>Original/</c> and an unrelated subfolder.
    /// </summary>
    private static (string Order, string[] Photos, Dictionary<string, string> Customer) SavedOrderWithPreExistingOriginals(TempDir dir)
    {
        var order = dir.File("order");
        Directory.CreateDirectory(order);
        var photos = new[] { "P1.jpg", "P2.jpg", "P3.jpg" }.Select(n => Fixtures.WriteJpeg(order, n, 300, 200)).ToArray();
        var original = Path.Combine(order, "Original");
        Directory.CreateDirectory(Path.Combine(original, "Older"));
        Fixtures.WriteJpeg(original, "P1.jpg", 120, 80);                      // the customer's own file, same name
        File.WriteAllText(Path.Combine(original, "notes.txt"), "customer notes");
        Fixtures.WriteJpeg(Path.Combine(original, "Older"), "P2.jpg", 90, 60);
        Directory.CreateDirectory(Path.Combine(order, "Prints"));
        Fixtures.WriteJpeg(Path.Combine(order, "Prints"), "P1.jpg", 100, 100);
        var customer = new[] { "Original/P1.jpg", "Original/notes.txt", "Original/Older/P2.jpg", "Prints/P1.jpg" }
            .ToDictionary(r => r, r => Fixtures.Sha(Path.Combine(order, r)));

        var saver = new PhotoSaver();
        Assert.Equal(SaveOutcome.Saved, saver.Save(photos[0], Crop, FileStamp.Of(photos[0])!.Value).Outcome);
        Assert.Equal(SaveOutcome.Saved, saver.Save(photos[1], Crop, FileStamp.Of(photos[1])!.Value).Outcome);
        Assert.Equal(SaveOutcome.Reviewed, saver.Save(photos[2], new EditRecipe { Crop = NormalizedRect.Full }, FileStamp.Of(photos[2])!.Value).Outcome);
        Assert.True(File.Exists(Path.Combine(original, "P1 (2).jpg")));        // PhotoX never reused the customer's name
        return (order, photos, customer);
    }

    private static Dictionary<string, string> Snapshot(string folder) =>
        Directory.EnumerateFiles(folder, "*", SearchOption.AllDirectories)
            .ToDictionary(f => Path.GetRelativePath(folder, f).Replace('\\', '/'), Fixtures.Sha);

    [Fact]
    public void Saved_photos_are_never_hidden_and_stay_listed()
    {
        using var dir = new TempDir();
        var (order, photos, _) = SavedOrderWithPreExistingOriginals(dir);

        // 0.4.0 bug (prompt_03 §6): on Windows the saved photos inherited the Hidden attribute of the temp file,
        // so the gallery and Explorer no longer showed them.
        foreach (var p in photos) Assert.Equal(0, (int)(File.GetAttributes(p) & FileAttributes.Hidden));
        Assert.Equal(["P1.jpg", "P2.jpg", "P3.jpg"], FolderScanner.Scan(order).Select(e => e.Name));
    }

    [Fact]
    public void Keep_changes_no_file_and_reopening_restores_the_order()
    {
        using var dir = new TempDir();
        var (order, _, _) = SavedOrderWithPreExistingOriginals(dir);
        var before = Snapshot(order);

        // Keep performs no file operation; the order reopens with every photo and its state.
        var entries = FolderScanner.Scan(order);
        Assert.Equal(3, entries.Count);
        var sidecar = new OrderStore(order).Load();
        Assert.Equal(3, sidecar.Photos.Count);
        Assert.Equal(before, Snapshot(order));
    }

    [Fact]
    public void Erase_with_pre_existing_files_deletes_only_photox_backups_and_history()
    {
        using var dir = new TempDir();
        var (order, photos, customer) = SavedOrderWithPreExistingOriginals(dir);
        var finished = photos.ToDictionary(p => p, Fixtures.Sha);

        var result = OrderCleanup.Erase(order, null);

        Assert.True(result.Complete);
        Assert.Equal(2, result.DeletedOriginals);
        Assert.False(result.OriginalFolderRemoved);
        foreach (var (p, sha) in finished) Assert.Equal(sha, Fixtures.Sha(p));                   // finished photos untouched
        foreach (var (rel, sha) in customer) Assert.Equal(sha, Fixtures.Sha(Path.Combine(order, rel))); // customer files untouched
        Assert.Equal(["P1.jpg", "P2.jpg", "P3.jpg"], FolderScanner.Scan(order).Select(e => e.Name));
        Assert.Equal(
            ["Original/Older/P2.jpg", "Original/P1.jpg", "Original/notes.txt", "P1.jpg", "P2.jpg", "P3.jpg", "Prints/P1.jpg"],
            Snapshot(order).Keys.Order(StringComparer.Ordinal));
    }

    [Fact]
    public void Erase_keeps_a_backup_whose_finished_photo_is_missing()
    {
        using var dir = new TempDir();
        var a = Fixtures.WriteJpeg(dir.Path, "a.jpg", 300, 200);
        Assert.True(new PhotoSaver().Save(a, Crop, FileStamp.Of(a)!.Value).Succeeded);
        File.Delete(a); // the backup is now the only copy

        var result = OrderCleanup.Erase(dir.Path, null);

        var backup = Path.Combine(dir.Path, "Original", "a.jpg");
        Assert.True(File.Exists(backup));
        Assert.Equal([backup], result.Remaining);
        Assert.NotNull(new OrderStore(dir.Path).Load().Find("a.jpg")); // still identifiable later
    }

    [Fact]
    public void Erase_keeps_a_backup_replaced_since_photox_made_it()
    {
        using var dir = new TempDir();
        var a = Fixtures.WriteJpeg(dir.Path, "a.jpg", 300, 200);
        Assert.True(new PhotoSaver().Save(a, Crop, FileStamp.Of(a)!.Value).Succeeded);
        var backup = Path.Combine(dir.Path, "Original", "a.jpg");
        File.Delete(backup);
        Fixtures.WriteJpeg(Path.Combine(dir.Path, "Original"), "a.jpg", 640, 480); // someone else's file now

        var result = OrderCleanup.Erase(dir.Path, null);

        Assert.True(File.Exists(backup));
        Assert.Equal(0, result.DeletedOriginals);
    }

    [Fact]
    public void Erase_ignores_sidecar_names_that_point_outside_original()
    {
        using var dir = new TempDir();
        var a = Fixtures.WriteJpeg(dir.Path, "a.jpg", 300, 200);
        var store = new OrderStore(dir.Path);
        var sidecar = new OrderSidecar();
        sidecar.Photos["a.jpg"] = new PhotoRecord { Backup = "../a.jpg", State = SavedState.Edited };
        store.Save(sidecar);

        OrderCleanup.Erase(dir.Path, null);

        Assert.True(File.Exists(a));
    }

    [Fact]
    public void Photos_hidden_by_040_are_made_visible_again()
    {
        Assert.SkipUnless(OperatingSystem.IsWindows(), "The Hidden attribute exists only on Windows.");
        using var dir = new TempDir();
        var a = Fixtures.WriteJpeg(dir.Path, "a.jpg", 300, 200);
        Assert.True(new PhotoSaver().Save(a, Crop, FileStamp.Of(a)!.Value).Succeeded);
        File.SetAttributes(a, File.GetAttributes(a) | FileAttributes.Hidden);
        Assert.Empty(FolderScanner.Scan(dir.Path));

        var store = new OrderStore(dir.Path);
        Assert.Equal(1, store.UnhideRecordedPhotos(store.Load()));
        Assert.Single(FolderScanner.Scan(dir.Path));
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
