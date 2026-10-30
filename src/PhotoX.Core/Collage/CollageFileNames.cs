namespace PhotoX.Core.Collage;

/// <summary>Collage output names: Collage_yyyyMMdd_HHmm.jpg, then _1, _2 … (never an existing name, like ID sheets).</summary>
public static class CollageFileNames
{
    public static string Base(DateTime localTime) => $"Collage_{localTime:yyyyMMdd_HHmm}";
}
