using System.Text.Json;

namespace PhotoX.Storage;

/// <summary>A requested copy count that differs from the default.</summary>
public sealed class QuantityRecord
{
    public int Copies { get; set; }

    public DateTime ChangedUtc { get; set; }
}

/// <summary>
/// Requested copies per photo (prompt_09 §1): how many copies the next Print selected or Print all makes, 1 unless
/// the operator chose otherwise, 0 to leave the photo out. Kept beside the print counters in PhotoX's user data folder
/// (<c>print-quantities.json</c>), never in the customer's folder or the photo (DEC-083). Only values other than 1 are
/// stored; keys are full paths (case-insensitive), like <see cref="PrintHistory"/>. Printing does not change them.
/// </summary>
public sealed class PrintQuantities
{
    public const int Default = 1;
    public const int Max = 99;
    private const int MaxEntries = 50000;
    private readonly string _file;
    private readonly object _gate = new();
    private readonly Dictionary<string, QuantityRecord> _photos;

    public PrintQuantities(string file)
    {
        _file = file;
        _photos = Load(file);
    }

    /// <summary>Raised with the paths whose quantity changed.</summary>
    public event Action<IReadOnlyList<string>>? Changed;

    public int For(string path)
    {
        lock (_gate) return _photos.TryGetValue(Key(path), out var r) ? r.Copies : Default;
    }

    /// <summary>Sets the quantity of every path (clamped to 0–<see cref="Max"/>) and saves the file once.</summary>
    public void Set(IReadOnlyCollection<string> paths, int copies)
    {
        copies = Math.Clamp(copies, 0, Max);
        var changed = new List<string>();
        lock (_gate)
        {
            foreach (var path in paths)
            {
                var key = Key(path);
                var old = _photos.TryGetValue(key, out var r) ? r.Copies : Default;
                if (old == copies) continue;
                if (copies == Default) _photos.Remove(key);
                else _photos[key] = new QuantityRecord { Copies = copies, ChangedUtc = DateTime.UtcNow };
                changed.Add(path);
            }

            if (changed.Count == 0) return;
            Trim();
            try
            {
                JsonFile.WriteAtomic(_file, _photos);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // The quantities stay in memory for this session.
            }
        }

        Changed?.Invoke(changed);
    }

    private static string Key(string path) => Path.GetFullPath(path).ToUpperInvariant();

    private void Trim()
    {
        if (_photos.Count <= MaxEntries) return;
        foreach (var old in _photos.OrderBy(p => p.Value.ChangedUtc).Take(_photos.Count - MaxEntries).Select(p => p.Key).ToList()) _photos.Remove(old);
    }

    private static Dictionary<string, QuantityRecord> Load(string file)
    {
        try
        {
            var d = JsonFile.Read<Dictionary<string, QuantityRecord>>(file);
            return d is null ? [] : d.Where(p => p.Value is { Copies: >= 0 and <= Max and not Default }).ToDictionary(p => p.Key.ToUpperInvariant(), p => p.Value);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            return [];
        }
    }
}
