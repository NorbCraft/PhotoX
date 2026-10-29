using System.Security.Cryptography;
using System.Text;

namespace PhotoX.Storage;

/// <summary>
/// Persistent thumbnail cache (FR-053, DEC-036): one sub-folder per order folder, so Finish Order can remove an
/// order's entries; size cap with least-recently-used pruning, age limit and Clear.
/// </summary>
public sealed class ThumbnailCache(string root, long maxBytes = 1L << 30, int maxAgeDays = 30)
{
    private const string Version = "v1";

    public string Root { get; } = root;

    public byte[]? TryGet(PhotoEntry entry)
    {
        var path = PathFor(entry);
        try
        {
            if (!File.Exists(path)) return null;
            var bytes = File.ReadAllBytes(path);
            File.SetLastWriteTimeUtc(path, DateTime.UtcNow); // LRU touch
            return bytes;
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
    }

    public void Put(PhotoEntry entry, byte[] bytes)
    {
        try
        {
            var path = PathFor(entry);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            var tmp = path + ".tmp";
            File.WriteAllBytes(tmp, bytes);
            File.Move(tmp, path, overwrite: true);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    /// <summary>Removes entries older than the age limit, then the least recently used ones above the size cap.</summary>
    public void Prune()
    {
        if (!Directory.Exists(Root)) return;
        var files = new DirectoryInfo(Root).EnumerateFiles("*.jpg", SearchOption.AllDirectories).ToList();
        var cutoff = DateTime.UtcNow.AddDays(-maxAgeDays);
        foreach (var f in files.Where(f => f.LastWriteTimeUtc < cutoff).ToList())
        {
            TryDelete(f);
            files.Remove(f);
        }

        var total = files.Sum(f => f.Length);
        foreach (var f in files.OrderBy(f => f.LastWriteTimeUtc))
        {
            if (total <= maxBytes) break;
            total -= f.Length;
            TryDelete(f);
        }

        foreach (var d in new DirectoryInfo(Root).EnumerateDirectories())
        {
            if (!d.EnumerateFileSystemInfos().Any()) d.Delete();
        }
    }

    public long SizeBytes() =>
        Directory.Exists(Root) ? new DirectoryInfo(Root).EnumerateFiles("*", SearchOption.AllDirectories).Sum(f => f.Length) : 0;

    public void Clear()
    {
        if (Directory.Exists(Root)) Directory.Delete(Root, recursive: true);
    }

    /// <summary>Removes the cached thumbnails of one order folder (Finish Order).</summary>
    public void RemoveFolder(string folder)
    {
        var dir = Path.Combine(Root, Hash(Normalize(folder)));
        if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true);
    }

    private string PathFor(PhotoEntry e)
    {
        var folder = Normalize(Path.GetDirectoryName(e.Path)!);
        var key = Hash($"{Normalize(e.Path)}|{e.Length}|{e.LastWriteUtc.Ticks}|{Version}");
        return Path.Combine(Root, Hash(folder), key + ".jpg");
    }

    private static string Normalize(string path)
    {
        var full = Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar);
        return OperatingSystem.IsWindows() ? full.ToUpperInvariant() : full;
    }

    private static string Hash(string s) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(s)))[..32].ToLowerInvariant();

    private static void TryDelete(FileInfo f)
    {
        try
        {
            f.Delete();
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
