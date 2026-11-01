namespace PhotoX.App.Services;

/// <summary>A third-party component PhotoX uses, as listed in THIRD-PARTY-NOTICES.md.</summary>
public sealed record ThirdPartyComponent(string Name, string Version, string Licence, string Source)
{
    public string Detail => string.IsNullOrEmpty(Version) || Version == "—" ? Licence : $"{Version}  ·  {Licence}";

    public bool HasSource => Source.StartsWith("https://", StringComparison.Ordinal);
}

/// <summary>
/// The third-party licences shown from Settings → About: the table of THIRD-PARTY-NOTICES.md, embedded in the app so
/// the list matches the repository file and needs no network or Markdown viewer.
/// </summary>
public static class ThirdPartyNotices
{
    public static IReadOnlyList<ThirdPartyComponent> Load()
    {
        using var stream = typeof(ThirdPartyNotices).Assembly.GetManifestResourceStream("PhotoX.ThirdPartyNotices.md");
        if (stream is null) return [];
        using var reader = new StreamReader(stream);
        return Parse(reader.ReadToEnd());
    }

    /// <summary>The rows of the first Markdown table: component, version, licence, source (backticks removed).</summary>
    public static IReadOnlyList<ThirdPartyComponent> Parse(string markdown)
    {
        var rows = new List<ThirdPartyComponent>();
        var header = true;
        foreach (var raw in markdown.Split('\n'))
        {
            var line = raw.Trim();
            if (!line.StartsWith('|'))
            {
                if (rows.Count > 0) break;
                continue;
            }

            var cells = line.Trim('|').Split('|').Select(c => c.Trim().Replace("`", string.Empty, StringComparison.Ordinal)).ToArray();
            if (header || cells.All(c => c.Length > 0 && c.All(ch => ch is '-' or ':')))
            {
                header = false;
                continue;
            }

            if (cells.Length >= 4) rows.Add(new ThirdPartyComponent(cells[0], cells[1], cells[2], cells[3]));
        }

        return rows;
    }
}
