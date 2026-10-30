using Avalonia.Input;
using Avalonia.Platform.Storage;

namespace PhotoX.App.Services;

/// <summary>
/// Drag and drop of photos onto collage frames (prompt_06 §6): from PhotoX's own thumbnails (in-process, the path),
/// or image files dragged from the system's file manager.
/// </summary>
public static class PhotoDrag
{
    public static readonly DataFormat<string> PathFormat = DataFormat.CreateInProcessFormat<string>("PhotoX.PhotoPath");

    public static IDataTransfer Create(string path)
    {
        var data = new DataTransfer();
        data.Add(DataTransferItem.Create(PathFormat, path));
        return data;
    }

    /// <summary>The dragged photo's path, or null when the data is not a photo PhotoX can use.</summary>
    public static string? PathFrom(IDataTransfer data)
    {
        if (data.TryGetValue(PathFormat) is { Length: > 0 } path) return path;
        return data.TryGetFile()?.TryGetLocalPath() is { } file && Imaging.FormatSniffer.HasPhotoExtension(file) ? file : null;
    }

    public static bool CanAccept(IDataTransfer data) => data.Contains(PathFormat) || data.Contains(DataFormat.File);
}
