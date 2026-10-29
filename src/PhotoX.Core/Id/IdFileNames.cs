namespace PhotoX.Core.Id;

/// <summary>ID output names (FR-075): ID_yyyyMMdd_HHmm.ext, then _1, _2 … if taken. Never an existing name.</summary>
public static class IdFileNames
{
    public static string Base(DateTime localTime) => $"ID_{localTime:yyyyMMdd_HHmm}";

    public static IEnumerable<string> Candidates(DateTime localTime, string extension)
    {
        var b = Base(localTime);
        yield return b + extension;
        for (var n = 1; ; n++) yield return $"{b}_{n}{extension}";
    }
}
