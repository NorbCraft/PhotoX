using PhotoX.Imaging;

namespace PhotoX.Storage;

/// <summary>A file in an order folder as seen by the gallery.</summary>
public sealed record PhotoEntry(string Path, string Name, long Length, DateTime LastWriteUtc, ImageKind Kind)
{
    public bool IsSupported => Kind is ImageKind.Jpeg or ImageKind.Png;

    public FileStamp Stamp => new(Length, LastWriteUtc);
}

/// <summary>Size and write time of a file when it was loaded, to detect external changes (FR-023).</summary>
public readonly record struct FileStamp(long Length, DateTime LastWriteUtc)
{
    public static FileStamp? Of(string path)
    {
        var fi = new FileInfo(path);
        return fi.Exists ? new FileStamp(fi.Length, fi.LastWriteTimeUtc) : null;
    }
}
