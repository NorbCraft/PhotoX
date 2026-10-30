using PhotoX.Core.Editing;
using PhotoX.Core.Geometry;
using PhotoX.Core.Orders;
using PhotoX.Storage;

namespace PhotoX.Tests;

/// <summary>
/// prompt_05 §5: Save Changes removes only PhotoX's own, verified backups; Restore Original brings the original back
/// safely. Replaces the Finish Order tests: the same customer files must survive (TS-09).
/// </summary>
public class SaveChangesTests
{
    private static EditRecipe Crop => new() { Orientation = CropOrientation.Landscape, Crop = new NormalizedRect(0.1, 0.1, 0.5, 0.5) };

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

    private static readonly string[] All = ["P1.jpg", "P2.jpg", "P3.jpg"];

    [Fact]
    public void Save_changes_removes_only_photox_backups_and_keeps_every_other_file()
    {
        using var dir = new TempDir();
        var (order, photos, customer) = SavedOrderWithPreExistingOriginals(dir);
        var finished = photos.ToDictionary(p => p, Fixtures.Sha);
        File.WriteAllText(Path.Combine(order, ".x.jpg.photox-tmp"), "someone's stray file"); // not one of the named photos

        var result = OrderBackups.RemoveBackups(order, All);

        Assert.True(result.Complete);
        Assert.Equal(All, result.Finalized.Order());
        Assert.False(result.OriginalFolderRemoved);                                              // the customer's files keep it
        foreach (var (p, sha) in finished) Assert.Equal(sha, Fixtures.Sha(p));                   // edited photos untouched
        foreach (var (rel, sha) in customer) Assert.Equal(sha, Fixtures.Sha(Path.Combine(order, rel))); // customer files untouched
        Assert.Equal(
            [".x.jpg.photox-tmp", "Original/Older/P2.jpg", "Original/P1.jpg", "Original/notes.txt", "P1.jpg", "P2.jpg", "P3.jpg", "Prints/P1.jpg"],
            Snapshot(order).Keys.Order(StringComparer.Ordinal));
        Assert.Equal(All, FolderScanner.Scan(order).Select(e => e.Name));
    }

    [Fact]
    public void Save_changes_for_some_photos_keeps_the_others_editable()
    {
        using var dir = new TempDir();
        var (order, _, _) = SavedOrderWithPreExistingOriginals(dir);

        var result = OrderBackups.RemoveBackups(order, ["P2.jpg"]);

        Assert.Equal(["P2.jpg"], result.Finalized);
        Assert.False(File.Exists(Path.Combine(order, "Original", "P2.jpg")));
        Assert.True(File.Exists(Path.Combine(order, "Original", "P1 (2).jpg")));          // P1's backup stays
        var sidecar = new OrderStore(order).Load();
        Assert.NotNull(sidecar.Find("P1.jpg"));
        Assert.Null(sidecar.Find("P2.jpg"));
    }

    [Fact]
    public void Save_changes_removes_an_empty_original_folder_and_later_edits_start_from_the_saved_photo()
    {
        using var dir = new TempDir();
        var a = Fixtures.WriteJpeg(dir.Path, "a.jpg", 300, 200);
        Assert.True(new PhotoSaver().Save(a, Crop, FileStamp.Of(a)!.Value).Succeeded);

        var result = OrderBackups.RemoveBackups(dir.Path, ["a.jpg"]);

        Assert.True(result.OriginalFolderRemoved);
        Assert.False(Directory.Exists(Path.Combine(dir.Path, "Original")));
        var hash = Fixtures.Sha(a);
        Assert.True(new PhotoSaver().Save(a, Crop, FileStamp.Of(a)!.Value).Succeeded);
        Assert.Equal(hash, Fixtures.Sha(Path.Combine(dir.Path, "Original", "a.jpg"))); // the saved photo is the new original
    }

    [Fact]
    public void A_backup_is_kept_when_its_photo_is_missing_changed_or_unreadable()
    {
        using var dir = new TempDir();
        var photos = new[] { "a.jpg", "b.jpg", "c.jpg" }.Select(n => Fixtures.WriteJpeg(dir.Path, n, 300, 200)).ToArray();
        foreach (var p in photos) Assert.True(new PhotoSaver().Save(p, Crop, FileStamp.Of(p)!.Value).Succeeded);
        File.Delete(photos[0]);                                        // the backup is now the only copy
        Fixtures.WriteJpeg(dir.Path, "b.jpg", 640, 480);                // changed outside PhotoX
        File.WriteAllBytes(photos[2], [1, 2, 3]);                       // damaged

        var result = OrderBackups.RemoveBackups(dir.Path, ["a.jpg", "b.jpg", "c.jpg"]);

        Assert.Empty(result.Finalized);
        Assert.Equal(
            [("a.jpg", KeepReason.PhotoNotVerified), ("b.jpg", KeepReason.PhotoChanged), ("c.jpg", KeepReason.PhotoNotVerified)],
            result.Kept.Select(k => (k.Photo, k.Reason)).Order());
        foreach (var n in new[] { "a.jpg", "b.jpg", "c.jpg" }) Assert.True(File.Exists(Path.Combine(dir.Path, "Original", n)));
        Assert.Equal(3, new OrderStore(dir.Path).Load().Photos.Count);  // still identifiable later
    }

    [Fact]
    public void A_backup_replaced_since_photox_made_it_is_kept()
    {
        using var dir = new TempDir();
        var a = Fixtures.WriteJpeg(dir.Path, "a.jpg", 300, 200);
        Assert.True(new PhotoSaver().Save(a, Crop, FileStamp.Of(a)!.Value).Succeeded);
        var backup = Path.Combine(dir.Path, "Original", "a.jpg");
        File.Delete(backup);
        Fixtures.WriteJpeg(Path.Combine(dir.Path, "Original"), "a.jpg", 640, 480); // someone else's file now
        var theirs = Fixtures.Sha(backup);

        var result = OrderBackups.RemoveBackups(dir.Path, ["a.jpg"]);

        Assert.Equal(KeepReason.NotOwnBackup, Assert.Single(result.Kept).Reason);
        Assert.Equal(theirs, Fixtures.Sha(backup));
        Assert.Equal(RestoreError.BackupNotVerified, OrderBackups.Restore(a).Error); // and it is not restored either
    }

    [Fact]
    public void Sidecar_names_pointing_outside_original_are_never_followed()
    {
        using var dir = new TempDir();
        var a = Fixtures.WriteJpeg(dir.Path, "a.jpg", 300, 200);
        var outside = Fixtures.WriteJpeg(dir.Path, "keep.jpg", 300, 200);
        var sidecar = new OrderSidecar();
        sidecar.Photos["a.jpg"] = new PhotoRecord { Backup = "../keep.jpg", State = SavedState.Edited };
        sidecar.Photos["../keep.jpg"] = new PhotoRecord { Backup = "a.jpg", State = SavedState.Edited };
        new OrderStore(dir.Path).Save(sidecar);
        var before = Snapshot(dir.Path).Where(f => !f.Key.StartsWith("Original/")).ToList();

        OrderBackups.RemoveBackups(dir.Path, ["a.jpg", "../keep.jpg"]);
        Assert.Equal(RestoreError.NoBackup, OrderBackups.Restore(a).Error);

        Assert.Equal(before, Snapshot(dir.Path).Where(f => !f.Key.StartsWith("Original/")).ToList());
        Assert.True(File.Exists(outside));
    }

    [Fact]
    public void Restore_original_brings_back_the_exact_original_and_clears_the_history()
    {
        using var dir = new TempDir();
        var (order, photos, customer) = SavedOrderWithPreExistingOriginals(dir);
        var backup = Path.Combine(order, "Original", "P1 (2).jpg");
        var original = Fixtures.Sha(backup);
        var p2 = Fixtures.Sha(photos[1]);

        var result = OrderBackups.Restore(photos[0]);

        Assert.True(result.Succeeded);
        Assert.Equal(original, Fixtures.Sha(photos[0]));             // byte-identical original
        Assert.False(File.Exists(backup));                            // no longer needed
        Assert.Null(new OrderStore(order).Load().Find("P1.jpg"));
        Assert.Equal(p2, Fixtures.Sha(photos[1]));                    // other photos untouched
        foreach (var (rel, sha) in customer) Assert.Equal(sha, Fixtures.Sha(Path.Combine(order, rel)));
        Assert.Equal(0, (int)(File.GetAttributes(photos[0]) & FileAttributes.Hidden));
        Assert.False(File.Exists(SaveNames.TempFor(photos[0])));
    }

    [Fact]
    public void Restore_without_a_backup_changes_nothing()
    {
        using var dir = new TempDir();
        var (order, photos, _) = SavedOrderWithPreExistingOriginals(dir);
        var before = Snapshot(order);
        Assert.Equal(RestoreError.NoBackup, OrderBackups.Restore(photos[2]).Error); // reviewed: nothing to restore
        Assert.Equal(before, Snapshot(order));
    }

    [Fact]
    public void A_read_only_photo_is_not_restored_and_its_backup_stays()
    {
        Assert.SkipUnless(OperatingSystem.IsWindows() || Environment.UserName != "root", "root ignores read-only files");
        using var dir = new TempDir();
        var a = Fixtures.WriteJpeg(dir.Path, "a.jpg", 300, 200);
        Assert.True(new PhotoSaver().Save(a, Crop, FileStamp.Of(a)!.Value).Succeeded);
        var edited = Fixtures.Sha(a);
        File.SetAttributes(a, FileAttributes.ReadOnly);
        try
        {
            Assert.Equal(RestoreError.ReadOnlyFile, OrderBackups.Restore(a).Error);
            Assert.Equal(edited, Fixtures.Sha(a));
            Assert.True(File.Exists(Path.Combine(dir.Path, "Original", "a.jpg")));
        }
        finally
        {
            File.SetAttributes(a, FileAttributes.Normal);
        }
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
