using System.Text.RegularExpressions;
using PhotoX.Core.Naming;
using PhotoX.Core.Orders;
using PhotoX.Imaging;

namespace PhotoX.Storage;

/// <summary>Lists the photos of an order folder (FR-002, FR-031).</summary>
public static partial class FolderScanner
{
    /// <summary>Generated ID sheets (FR-075) are excluded from ordinary-photo processing by default.</summary>
    [GeneratedRegex(@"^ID_\d{8}_\d{4}(_\d+)?\.(jpg|jpeg|png)$", RegexOptions.IgnoreCase)]
    private static partial Regex IdOutputName();

    public static bool IsIdOutput(string fileName) => IdOutputName().IsMatch(fileName);

    public static IReadOnlyList<PhotoEntry> Scan(string folder, bool includeIdOutputs = false)
    {
        var result = new List<PhotoEntry>();
        var dir = new DirectoryInfo(folder);
        if (!dir.Exists) return result;

        foreach (var fi in dir.EnumerateFiles("*", new EnumerationOptions { IgnoreInaccessible = true, AttributesToSkip = FileAttributes.System }))
        {
            if (fi.Name.StartsWith('.') || (fi.Attributes & FileAttributes.Hidden) != 0) continue;
            if (fi.Name.EndsWith(SaveNames.TempSuffix, StringComparison.OrdinalIgnoreCase)) continue;
            if (!FormatSniffer.HasPhotoExtension(fi.Name)) continue;
            if (!includeIdOutputs && IsIdOutput(fi.Name)) continue;
            result.Add(new PhotoEntry(fi.FullName, fi.Name, fi.Length, fi.LastWriteTimeUtc, FormatSniffer.Detect(fi.FullName)));
        }

        result.Sort((a, b) => NaturalComparer.Instance.Compare(a.Name, b.Name));
        return result;
    }

    /// <summary>Subfolders shown in the explorer: hidden/system folders and <c>Original</c> are skipped (FR-001).</summary>
    public static IReadOnlyList<string> SubFolders(string folder)
    {
        try
        {
            return VisibleSubFolders(folder)
                .Select(d => d.FullName)
                .OrderBy(n => Path.GetFileName(n), NaturalComparer.Instance)
                .ToList();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        {
            return [];
        }
    }

    /// <summary>True if the explorer would show at least one subfolder (stops at the first one).</summary>
    public static bool HasSubFolders(string folder)
    {
        try
        {
            return VisibleSubFolders(folder).Any();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        {
            return false;
        }
    }

    private static IEnumerable<DirectoryInfo> VisibleSubFolders(string folder) =>
        new DirectoryInfo(folder)
            .EnumerateDirectories("*", new EnumerationOptions { IgnoreInaccessible = true, AttributesToSkip = FileAttributes.Hidden | FileAttributes.System })
            .Where(d => !d.Name.StartsWith('.') && !string.Equals(d.Name, OrderSidecar.OriginalFolderName, StringComparison.OrdinalIgnoreCase));
}
