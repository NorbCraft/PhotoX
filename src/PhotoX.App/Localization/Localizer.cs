using System.ComponentModel;
using System.Globalization;
using System.Text.Json;

namespace PhotoX.App.Localization;

/// <summary>
/// Runtime-switchable UI strings (FR-009). Strings live in Strings_en.json / Strings_ro.json; views bind through
/// <see cref="TrExtension"/> to the indexer, which updates live when the language changes.
/// </summary>
public sealed class Localizer : INotifyPropertyChanged
{
    public static readonly string[] Languages = ["en", "ro"];

    private readonly Dictionary<string, Dictionary<string, string>> _tables = new();
    private readonly Dictionary<string, LocalizedString> _observables = new();
    private Dictionary<string, string> _current;

    private Localizer()
    {
        foreach (var lang in Languages)
        {
            _tables[lang] = LoadTable(lang);
        }

        _current = _tables["en"];
    }

    public static Localizer Instance { get; } = new();

    public event PropertyChangedEventHandler? PropertyChanged;

    public event EventHandler? LanguageChanged;

    public string Language { get; private set; } = "en";

    public CultureInfo Culture { get; private set; } = CultureInfo.GetCultureInfo("en-GB");

    public string this[string key] =>
        _current.TryGetValue(key, out var s) ? s : _tables["en"].TryGetValue(key, out var e) ? e : $"[{key}]";

    public string Format(string key, params object?[] args) => string.Format(Culture, this[key], args);

    /// <summary>
    /// Count-dependent text: uses key_one / key_few / key_many (Romanian: 1 fotografie, 2–19 fotografii,
    /// 20+ de fotografii) and formats the count into it.
    /// </summary>
    public string Plural(string key, int count, params object?[] more)
    {
        var form = count == 1 ? "one"
            : Language == "ro" && count != 0 && (count % 100 == 0 || count % 100 >= 20) ? "many"
            : "few";
        var args = new object?[more.Length + 1];
        args[0] = count;
        more.CopyTo(args, 1);
        return string.Format(Culture, this[$"{key}_{form}"], args);
    }

    /// <summary>Observable wrapper for one key; XAML binds to its <see cref="LocalizedString.Value"/>.</summary>
    public LocalizedString Observe(string key)
    {
        lock (_observables)
        {
            if (!_observables.TryGetValue(key, out var o))
            {
                o = new LocalizedString(this, key);
                _observables[key] = o;
            }

            return o;
        }
    }

    public void SetLanguage(string language)
    {
        if (!_tables.ContainsKey(language)) language = "en";
        Language = language;
        _current = _tables[language];
        Culture = CultureInfo.GetCultureInfo(language == "ro" ? "ro-RO" : "en-GB");
        CultureInfo.CurrentCulture = Culture;
        CultureInfo.CurrentUICulture = Culture;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Item[]"));
        List<LocalizedString> observed;
        lock (_observables)
        {
            observed = [.. _observables.Values];
        }

        foreach (var o in observed) o.Raise();
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Language)));
        LanguageChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>All keys of a language table (used by tests for key parity).</summary>
    public IReadOnlyCollection<string> Keys(string language) => _tables[language].Keys;

    private static Dictionary<string, string> LoadTable(string lang)
    {
        var asm = typeof(Localizer).Assembly;
        using var stream = asm.GetManifestResourceStream($"PhotoX.Strings.Strings_{lang}.json")
            ?? throw new InvalidOperationException($"Missing strings for '{lang}'.");
        return JsonSerializer.Deserialize<Dictionary<string, string>>(stream) ?? new();
    }
}

/// <summary>A single localized string that notifies when the language changes.</summary>
public sealed class LocalizedString(Localizer localizer, string key) : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    public string Value => localizer[key];

    internal void Raise() => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Value)));
}
