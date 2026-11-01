using PhotoX.Core.Editing;
using PhotoX.Core.Geometry;
using PhotoX.Core.Orders;
using PhotoX.Storage;

namespace PhotoX.Tests;

/// <summary>
/// prompt_08 §5: deleting photos moves them into <c>Original/</c> and records them; restoring brings them back; Discard
/// Originals for the whole folder removes only PhotoX's own, verified files. Generated test images in temp folders
/// only (TS-23).
/// </summary>
public class PhotoTrashTests
{
    private static EditRecipe Crop => new() { Orientation = CropOrientation.Landscape, Crop = new NormalizedRect(0.1, 0.1, 0.5, 0.5) };

    private static string Order(TempDir dir, params string[] names)
    {
        var order = dir.File("order");
        Directory.CreateDirectory(order);
        foreach (var n in names) Fixtures.WriteJpeg(order, n, 300, 200);
        return order;
    }

    private static Dictionary<string, string> Snapshot(string folder) =>
        Directory.EnumerateFiles(folder, "*", SearchOption.AllDirectories)
            .Where(f => !f.EndsWith(OrderSidecar.FileName, StringComparison.Ordinal))
            .ToDictionary(f => Path.GetRelativePath(folder, f).Replace('\\', '/'), Fixtures.Sha);

    private static void Edit(string photo) => Assert.Equal(SaveOutcome.Saved, new PhotoSaver().Save(photo, Crop, FileStamp.Of(photo)!.Value).Outcome);

    [Fact]
    public void Deleting_a_photo_moves_it_into_original_and_keeps_it_recoverable()
    {
        using var dir = new TempDir();
        var order = Order(dir, "P1.jpg", "P2.jpg");
        var sha = Fixtures.Sha(Path.Combine(order, "P1.jpg"));

        var result = PhotoTrash.Delete(order, ["P1.jpg"]);

        Assert.Empty(result.Failed);
        var deleted = Assert.Single(result.Deleted);
        Assert.Equal("P1 (deleted).jpg", deleted.StoredName);
        Assert.Equal(sha, Fixtures.Sha(Path.Combine(order, "Original", "P1 (deleted).jpg")));       // moved, not changed
        Assert.False(File.Exists(Path.Combine(order, "P1.jpg")));
        Assert.Equal(["P2.jpg"], FolderScanner.Scan(order).Select(e => e.Name));                  // gone from the gallery
        Assert.Equal("P1.jpg", Assert.Single(PhotoTrash.List(order)).Name);                        // shown under Deleted
        Assert.Equal("P1.jpg", new OrderStore(order).Load().Deleted["P1 (deleted).jpg"].Name);
    }

    [Fact]
    public void Deleting_several_photos_moves_each_one()
    {
        using var dir = new TempDir();
        var order = Order(dir, "P1.jpg", "P2.jpg", "P3.jpg", "P4.jpg");
        var before = Snapshot(order);

        var result = PhotoTrash.Delete(order, ["P1.jpg", "P3.jpg", "P4.jpg", "missing.jpg", "../P2.jpg"]);

        Assert.Equal(["P1.jpg", "P3.jpg", "P4.jpg"], result.Deleted.Select(d => d.Name));
        Assert.Equal(["missing.jpg", "../P2.jpg"], result.Failed.Select(f => f.Name));
        Assert.All(result.Failed, f => Assert.Equal(TrashError.NotFound, f.Error));
        Assert.Equal(["P2.jpg"], FolderScanner.Scan(order).Select(e => e.Name));
        Assert.Equal(["P1.jpg", "P3.jpg", "P4.jpg"], PhotoTrash.List(order).Select(d => d.Name));
        // The same bytes, only in new places.
        Assert.Equal(before.Values.Order(), Snapshot(order).Values.Order());
    }

    [Fact]
    public void Restoring_brings_deleted_photos_back_with_their_edit_state()
    {
        using var dir = new TempDir();
        var order = Order(dir, "P1.jpg", "P2.jpg", "P3.jpg");
        var p1 = Path.Combine(order, "P1.jpg");
        Edit(p1);
        var edited = Fixtures.Sha(p1);
        var sha3 = Fixtures.Sha(Path.Combine(order, "P3.jpg"));
        var deleted = PhotoTrash.Delete(order, ["P1.jpg", "P3.jpg"]).Deleted;

        var result = PhotoTrash.Restore(order, deleted.Select(d => d.StoredName));

        Assert.Empty(result.Failed);
        Assert.Equal(["P1.jpg", "P3.jpg"], result.Restored.Select(r => r.Name));
        Assert.Equal(edited, Fixtures.Sha(p1));
        Assert.Equal(sha3, Fixtures.Sha(Path.Combine(order, "P3.jpg")));
        Assert.Equal(["P1.jpg", "P2.jpg", "P3.jpg"], FolderScanner.Scan(order).Select(e => e.Name));
        Assert.Empty(PhotoTrash.List(order));
        var sidecar = new OrderStore(order).Load();
        Assert.Empty(sidecar.Deleted);
        var record = sidecar.Find("P1.jpg")!;                                                       // still edited, still restorable
        Assert.Equal(SavedState.Edited, record.State);
        Assert.Equal(FileStamp.Of(p1)!.Value.Length, record.OutputLength);
        Assert.Equal(FileStamp.Of(p1)!.Value.LastWriteUtc, record.OutputWriteTimeUtc);
    }

    [Fact]
    public void A_deleted_edited_photo_keeps_its_original_and_can_still_be_restored_to_it()
    {
        using var dir = new TempDir();
        var order = Order(dir, "P1.jpg");
        var p1 = Path.Combine(order, "P1.jpg");
        var original = Fixtures.Sha(p1);
        Edit(p1);                                                                                     // Original/P1.jpg is the kept original
        Assert.Equal(original, Fixtures.Sha(Path.Combine(order, "Original", "P1.jpg")));

        var stored = Assert.Single(PhotoTrash.Delete(order, ["P1.jpg"]).Deleted).StoredName;

        Assert.Equal("P1 (deleted).jpg", stored);                                                    // never the kept original's name
        Assert.Equal(original, Fixtures.Sha(Path.Combine(order, "Original", "P1.jpg")));             // the kept original is untouched
        PhotoTrash.Restore(order, [stored]);
        Assert.True(OrderBackups.Restore(p1).Succeeded);                                             // Restore Original still works
        Assert.Equal(original, Fixtures.Sha(p1));
        Assert.False(Directory.Exists(Path.Combine(order, "Original")));
    }

    [Fact]
    public void Deleting_never_overwrites_or_reuses_a_name_in_original()
    {
        using var dir = new TempDir();
        var order = Order(dir, "P1.jpg", "P2.jpg");
        var originalDir = Path.Combine(order, "Original");
        Directory.CreateDirectory(originalDir);
        Fixtures.WriteJpeg(originalDir, "P2 (deleted).jpg", 50, 50);                                   // a customer's file with the name PhotoX would use
        Fixtures.WriteJpeg(originalDir, "P2.jpg", 60, 60);
        var customer = Snapshot(originalDir);

        var first = Assert.Single(PhotoTrash.Delete(order, ["P2.jpg"]).Deleted);
        Fixtures.WriteJpeg(order, "P2.jpg", 120, 80);                                                // a new photo of the same name, deleted too
        var second = Assert.Single(PhotoTrash.Delete(order, ["P2.jpg"]).Deleted);

        Assert.Equal("P2 (deleted 2).jpg", first.StoredName);
        Assert.Equal("P2 (deleted 3).jpg", second.StoredName);
        foreach (var (rel, sha) in customer) Assert.Equal(sha, Fixtures.Sha(Path.Combine(originalDir, rel)));
        Assert.Equal(2, PhotoTrash.List(order).Count);
    }

    [Fact]
    public void Restoring_into_a_taken_name_uses_a_free_one_and_overwrites_nothing()
    {
        using var dir = new TempDir();
        var order = Order(dir, "P1.jpg");
        var deletedSha = Fixtures.Sha(Path.Combine(order, "P1.jpg"));
        var stored = Assert.Single(PhotoTrash.Delete(order, ["P1.jpg"]).Deleted).StoredName;
        var newer = Fixtures.Sha(Fixtures.WriteJpeg(order, "P1.jpg", 200, 300));                      // the customer copied a new P1.jpg meanwhile
        Fixtures.WriteJpeg(order, "P1 (2).jpg", 80, 80);

        var restored = Assert.Single(PhotoTrash.Restore(order, [stored]).Restored);

        Assert.Equal("P1 (3).jpg", restored.Name);
        Assert.True(restored.Renamed("P1.jpg"));
        Assert.Equal(newer, Fixtures.Sha(Path.Combine(order, "P1.jpg")));
        Assert.Equal(deletedSha, Fixtures.Sha(Path.Combine(order, "P1 (3).jpg")));
        Assert.Equal(["P1 (2).jpg", "P1 (3).jpg", "P1.jpg"], FolderScanner.Scan(order).Select(e => e.Name).Order(StringComparer.Ordinal));
    }

    [Fact]
    public void A_deleted_photo_changed_outside_photox_is_neither_restored_nor_removed()
    {
        using var dir = new TempDir();
        var order = Order(dir, "P1.jpg", "P2.jpg");
        var stored = Assert.Single(PhotoTrash.Delete(order, ["P1.jpg"]).Deleted).StoredName;
        var path = Path.Combine(order, "Original", stored);
        File.AppendAllText(path, "changed by someone");
        var sha = Fixtures.Sha(path);

        Assert.Empty(PhotoTrash.List(order));
        Assert.Equal(TrashError.NotVerified, Assert.Single(PhotoTrash.Restore(order, [stored]).Failed).Error);
        var discard = OrderBackups.DiscardFolder(order);

        Assert.Equal(KeepReason.NotOwnBackup, Assert.Single(discard.Kept).Reason);
        Assert.Equal(sha, Fixtures.Sha(path));                                                       // kept as it is
        Assert.True(new OrderStore(order).Load().Deleted.ContainsKey(stored));                        // and still recorded
    }

    [Fact]
    public void Discard_originals_for_the_folder_keeps_edited_photos_and_unrelated_files()
    {
        using var dir = new TempDir();
        var order = Order(dir, "P1.jpg", "P2.jpg", "P3.jpg", "P4.jpg", "P5.jpg", "P6.jpg");
        var originalDir = Path.Combine(order, "Original");
        Directory.CreateDirectory(Path.Combine(originalDir, "Older"));
        File.WriteAllText(Path.Combine(originalDir, "notes.txt"), "customer notes");
        Fixtures.WriteJpeg(Path.Combine(originalDir, "Older"), "P1.jpg", 90, 60);
        Directory.CreateDirectory(Path.Combine(order, "Prints"));
        Fixtures.WriteJpeg(Path.Combine(order, "Prints"), "P1.jpg", 100, 100);
        foreach (var n in new[] { "P1.jpg", "P2.jpg", "P4.jpg" }) Edit(Path.Combine(order, n));
        Assert.Equal(SaveOutcome.Reviewed, new PhotoSaver().Save(Path.Combine(order, "P3.jpg"), new EditRecipe(), FileStamp.Of(Path.Combine(order, "P3.jpg"))!.Value).Outcome);
        PhotoTrash.Delete(order, ["P4.jpg", "P5.jpg"]);                                              // P4 edited (its original is kept), P5 untouched
        var unrelated = new[] { "Original/notes.txt", "Original/Older/P1.jpg", "Prints/P1.jpg", "P6.jpg" }
            .ToDictionary(r => r, r => Fixtures.Sha(Path.Combine(order, r)));
        var edited = new[] { "P1.jpg", "P2.jpg", "P3.jpg" }.ToDictionary(r => r, r => Fixtures.Sha(Path.Combine(order, r)));

        var result = OrderBackups.DiscardFolder(order);

        Assert.True(result.Complete);
        Assert.Equal(["P1.jpg", "P2.jpg", "P3.jpg"], result.Finalized.Order());
        Assert.Equal(["P4.jpg", "P5.jpg"], result.RemovedDeleted.Order());
        foreach (var (rel, sha) in edited.Concat(unrelated)) Assert.Equal(sha, Fixtures.Sha(Path.Combine(order, rel)));
        Assert.Equal(
            ["Original/Older/P1.jpg", "Original/notes.txt", "P1.jpg", "P2.jpg", "P3.jpg", "P6.jpg", "Prints/P1.jpg"],
            Snapshot(order).Keys.Order(StringComparer.Ordinal));
        Assert.False(File.Exists(new OrderStore(order).SidecarPath));
        Assert.False(result.OriginalFolderRemoved);                                                  // the customer's files keep it
    }

    [Fact]
    public void Discard_originals_removes_the_original_folder_only_when_photox_emptied_it()
    {
        using var dir = new TempDir();
        var order = Order(dir, "P1.jpg", "P2.jpg");
        Edit(Path.Combine(order, "P1.jpg"));
        PhotoTrash.Delete(order, ["P2.jpg"]);

        var result = OrderBackups.DiscardFolder(order);

        Assert.True(result.Complete);
        Assert.True(result.OriginalFolderRemoved);
        Assert.Equal(["P1.jpg"], Directory.GetFiles(order).Select(Path.GetFileName));
    }

    [Fact]
    public void Reopening_a_folder_lists_its_deleted_photos_and_saving_others_keeps_them()
    {
        using var dir = new TempDir();
        var order = Order(dir, "P1.jpg", "P2.jpg", "P3.jpg");
        PhotoTrash.Delete(order, ["P2.jpg"]);
        var p1 = Path.Combine(order, "P1.jpg");

        Edit(p1);                                                                                     // saving and discarding other photos keep the record
        OrderBackups.RemoveBackups(order, ["P1.jpg"]);
        Edit(Path.Combine(order, "P3.jpg"));
        Assert.True(OrderBackups.Restore(Path.Combine(order, "P3.jpg")).Succeeded);

        var listed = Assert.Single(PhotoTrash.List(order));                                           // as after a restart: read from disk
        Assert.Equal("P2.jpg", listed.Name);
        Assert.Equal(["P1.jpg", "P3.jpg"], FolderScanner.Scan(order).Select(e => e.Name));
        Assert.Single(PhotoTrash.Restore(order, [listed.StoredName]).Restored);
        Assert.Equal(["P1.jpg", "P2.jpg", "P3.jpg"], FolderScanner.Scan(order).Select(e => e.Name));
    }

    [Fact]
    public void A_sidecar_of_an_earlier_version_loads_without_deleted_photos()
    {
        using var dir = new TempDir();
        var order = Order(dir, "P1.jpg");
        Directory.CreateDirectory(Path.Combine(order, "Original"));
        File.WriteAllText(new OrderStore(order).SidecarPath, """{ "version": 1, "app": "PhotoX", "photos": {} }""");

        Assert.Empty(new OrderStore(order).Load().Deleted);
        Assert.Single(PhotoTrash.Delete(order, ["P1.jpg"]).Deleted);
        Assert.Equal(OrderSidecar.CurrentVersion, new OrderStore(order).Load().Version);
    }

    [Fact]
    public void A_new_kept_original_never_takes_a_name_recorded_for_a_deleted_photo()
    {
        using var dir = new TempDir();
        var order = Order(dir, "P1.jpg");
        var stored = Assert.Single(PhotoTrash.Delete(order, ["P1.jpg"]).Deleted).StoredName;
        File.Move(Path.Combine(order, "Original", stored), dir.File("away.jpg"));                     // the file went missing, the record stays
        Fixtures.WriteJpeg(order, "P1 (deleted).jpg", 300, 200);

        Edit(Path.Combine(order, "P1 (deleted).jpg"));

        Assert.Equal("P1 (deleted) (2).jpg", new OrderStore(order).Load().Find("P1 (deleted).jpg")!.Backup);
    }
}
