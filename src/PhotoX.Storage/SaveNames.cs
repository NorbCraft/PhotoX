namespace PhotoX.Storage;

public static class SaveNames
{
    public const string TempSuffix = ".photox-tmp";

    public static string TempFor(string path) =>
        Path.Combine(Path.GetDirectoryName(path)!, "." + Path.GetFileName(path) + TempSuffix);
}
