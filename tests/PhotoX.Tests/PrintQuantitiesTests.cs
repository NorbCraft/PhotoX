using PhotoX.Core.Formats;
using PhotoX.Printing;
using PhotoX.Storage;

namespace PhotoX.Tests;

/// <summary>prompt_09 §§1, 5, 6: requested copies per photo, kept outside the customer's folder, and the queue (TS-24).</summary>
public class PrintQuantitiesTests
{
    private static readonly PrinterPaper Paper4x6 = new(1, "(6x4)", 152.4, 101.6);

    [Fact]
    public void Every_photo_starts_at_one_copy_and_keeps_what_is_set_between_sessions()
    {
        using var dir = new TempDir();
        var file = dir.File("print-quantities.json");
        var q = new PrintQuantities(file);
        var (a, b, c) = (dir.File("order/A.jpg"), dir.File("order/B.jpg"), dir.File("order/C.jpg"));
        Assert.Equal(1, q.For(a));
        Assert.False(File.Exists(file));                                          // nothing stored for the default

        q.Set([a], 3);
        q.Set([b, c], 0);
        q.Set([c], 2);

        var reopened = new PrintQuantities(file);
        Assert.Equal([3, 0, 2], new[] { a, b, c }.Select(reopened.For));
        Assert.Equal(3, reopened.For(a.ToUpperInvariant()));                       // paths compare as on Windows
        q.Set([a, b, c], 1);                                                       // back to the default: no longer stored
        Assert.Equal([1, 1, 1], new[] { a, b, c }.Select(new PrintQuantities(file).For));
        Assert.DoesNotContain("A.JPG", File.ReadAllText(file), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Quantities_stay_between_0_and_99_and_only_real_changes_are_reported()
    {
        using var dir = new TempDir();
        var q = new PrintQuantities(dir.File("q.json"));
        var photo = dir.File("P1.jpg");
        var changes = new List<IReadOnlyList<string>>();
        q.Changed += changes.Add;

        q.Set([photo], -4);
        Assert.Equal(0, q.For(photo));
        q.Set([photo], 500);
        Assert.Equal(99, q.For(photo));
        q.Set([photo], 99);                                                         // unchanged: no event, no write
        Assert.Equal(2, changes.Count);
        Assert.All(changes, c => Assert.Equal([photo], c));
    }

    [Fact]
    public void A_damaged_or_unreadable_file_means_the_defaults()
    {
        using var dir = new TempDir();
        var file = dir.File("q.json");
        File.WriteAllText(file, "{ not json");
        Assert.Equal(1, new PrintQuantities(file).For(dir.File("P1.jpg")));
        var (p1, p2) = (dir.File("P1.jpg"), dir.File("P2.jpg"));
        File.WriteAllText(file, System.Text.Json.JsonSerializer.Serialize(new Dictionary<string, object>
        {
            [Path.GetFullPath(p1).ToUpperInvariant()] = new { copies = -3 },
            [Path.GetFullPath(p2).ToUpperInvariant()] = new { copies = 4 },
        }));
        var q = new PrintQuantities(file);
        Assert.Equal((1, 4), (q.For(p1), q.For(p2)));                                // out of range: ignored
    }

    [Fact]
    public void Each_photo_prints_its_own_copies_and_photos_at_zero_are_not_queued()
    {
        // The example of prompt_09 §5: 1, 3, 0, 2, 1 copies → 4 photos, 7 copies, IMG_003 left out.
        using var dir = new TempDir();
        var photos = Enumerable.Range(1, 5).Select(i => Fixtures.WriteJpeg(dir.Path, $"IMG_00{i}.jpg", 600, 400)).ToArray();
        var hashes = photos.Select(Fixtures.Sha).ToArray();
        int[] copies = [1, 3, 0, 2, 1];
        var backend = new FakePrintBackend();
        var history = new PrintHistory(dir.File("print-history.json"));
        using var service = new PrintService(backend, history, startWorker: false);

        var jobs = service.Enqueue(photos.Select((p, i) => new PrintRequest(new PhotoPrintItem(Path.GetFileName(p), p, null, false, copies[i]), "Citizen CY-02", Paper4x6, PrintFormats.P10x15)));
        service.ProcessPending();

        Assert.Equal(4, jobs.Count);
        Assert.Equal(7, backend.Submitted.Sum(s => s.Copies));
        Assert.Equal([1, 3, 2, 1], backend.Submitted.Select(s => s.Copies));
        Assert.DoesNotContain(backend.Submitted, s => s.DocumentName.Contains("IMG_003", StringComparison.Ordinal));
        Assert.Equal([1, 3, 0, 2, 1], photos.Select(history.CountFor));             // counted once Windows accepted them

        service.Cancel(jobs[1]);                                                    // a cancelled job is taken back
        Assert.Equal(0, history.CountFor(photos[1]));
        Assert.Equal(hashes, photos.Select(Fixtures.Sha));                          // nothing written to the photos
        Assert.False(Directory.Exists(Path.Combine(dir.Path, "Original")));
    }
}
