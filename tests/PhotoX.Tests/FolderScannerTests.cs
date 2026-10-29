using PhotoX.Core.Naming;
using PhotoX.Imaging;
using PhotoX.Storage;

namespace PhotoX.Tests;

/// <summary>TS-10: listing, natural sort and exclusions.</summary>
public class FolderScannerTests
{
    [Fact]
    public void Scan_lists_photos_naturally_and_excludes_backups_temps_and_id_outputs()
    {
        using var dir = new TempDir();
        foreach (var n in new[] { "IMG_10.jpg", "IMG_2.jpg", "img_1.JPG", "Fotografie ș ț.jpg" })
        {
            Fixtures.WriteJpeg(dir.Path, n, 30, 20);
        }

        Fixtures.WriteJpeg(dir.Path, "ID_20261029_1845.jpg", 30, 20);
        File.WriteAllText(dir.File(".IMG_2.jpg.photox-tmp"), "x");
        File.WriteAllText(dir.File("notes.txt"), "x");
        File.WriteAllBytes(dir.File("phone.HEIC"), [0, 0, 0, 24, (byte)'f', (byte)'t', (byte)'y', (byte)'p', (byte)'h', (byte)'e', (byte)'i', (byte)'c']);
        Directory.CreateDirectory(dir.File("Original"));
        Fixtures.WriteJpeg(dir.File("Original"), "IMG_2.jpg", 30, 20);

        var names = FolderScanner.Scan(dir.Path).Select(p => p.Name).ToList();
        Assert.Equal(["Fotografie ș ț.jpg", "img_1.JPG", "IMG_2.jpg", "IMG_10.jpg", "phone.HEIC"], names);
        Assert.Equal(ImageKind.Heic, FolderScanner.Scan(dir.Path).Single(p => p.Name == "phone.HEIC").Kind);
        Assert.Contains("ID_20261029_1845.jpg", FolderScanner.Scan(dir.Path, includeIdOutputs: true).Select(p => p.Name));
        Assert.DoesNotContain(FolderScanner.SubFolders(dir.Path), d => d.EndsWith("Original"));
    }

    [Fact]
    public void Natural_comparer_orders_numbers_by_value()
    {
        var list = new List<string> { "a10", "a2", "A1", "a02b", "b" };
        list.Sort(NaturalComparer.Instance);
        Assert.Equal(["A1", "a2", "a02b", "a10", "b"], list);
    }
}
