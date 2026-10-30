using System.Text.Json;

namespace PhotoX.Storage;

/// <summary>Copies of one photo accepted by Windows through PhotoX.</summary>
public sealed class PrintRecord
{
    public int Copies { get; set; }

    public DateTime LastUtc { get; set; }
}

/// <summary>
/// Print counters per photo (prompt_05 §6), kept in PhotoX's own data folder (<c>print-history.json</c>), never in the
/// customer's folder. A counter is the number of copies whose print job Windows accepted, minus copies of jobs that
/// were cancelled through PhotoX or seen removed from the Windows queue before finishing. It does not prove physical
/// output. Keys are full paths (case-insensitive), so the count survives editing, saving and reopening the order.
/// </summary>
public sealed class PrintHistory
{
    private const int MaxEntries = 20000;
    private readonly string _file;
    private readonly object _gate = new();
    private Dictionary<string, PrintRecord> _photos;

    public PrintHistory(string file)
    {
        _file = file;
        _photos = Load(file);
    }

    public event Action<string>? Changed;

    public int CountFor(string path)
    {
        lock (_gate) return _photos.TryGetValue(Key(path), out var r) ? r.Copies : 0;
    }

    /// <summary>Adds (or, with a negative number, takes back) copies of <paramref name="path"/> and saves the file.</summary>
    public void Add(string path, int copies)
    {
        if (copies == 0) return;
        lock (_gate)
        {
            var key = Key(path);
            if (!_photos.TryGetValue(key, out var r)) _photos[key] = r = new PrintRecord();
            r.Copies = Math.Max(0, r.Copies + copies);
            r.LastUtc = DateTime.UtcNow;
            if (r.Copies == 0) _photos.Remove(key);
            Trim();
            try
            {
                JsonFile.WriteAtomic(_file, _photos);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // The counter stays in memory for this session.
            }
        }

        Changed?.Invoke(path);
    }

    private static string Key(string path) => Path.GetFullPath(path).ToUpperInvariant();

    private void Trim()
    {
        if (_photos.Count <= MaxEntries) return;
        foreach (var old in _photos.OrderBy(p => p.Value.LastUtc).Take(_photos.Count - MaxEntries).Select(p => p.Key).ToList()) _photos.Remove(old);
    }

    private static Dictionary<string, PrintRecord> Load(string file)
    {
        try
        {
            var d = JsonFile.Read<Dictionary<string, PrintRecord>>(file);
            return d is null ? [] : new Dictionary<string, PrintRecord>(d);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            return [];
        }
    }
}
