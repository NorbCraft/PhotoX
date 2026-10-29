using PhotoX.App.ViewModels;

namespace PhotoX.App.Services;

/// <summary>Drives and mount points for the explorer, with removable detection (FR-001, FR-054).</summary>
public static class DriveHelper
{
    public static IEnumerable<LocationItem> Locations(string homeName, string removableName)
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (!OperatingSystem.IsWindows())
        {
            yield return new LocationItem(homeName, home, false);
        }

        foreach (var d in SafeDrives())
        {
            var root = d.RootDirectory.FullName;
            if (OperatingSystem.IsWindows())
            {
                var label = string.IsNullOrWhiteSpace(SafeLabel(d)) ? (d.DriveType == DriveType.Removable ? removableName : root) : SafeLabel(d);
                yield return new LocationItem($"{label} ({root.TrimEnd('\\')})", root, d.DriveType == DriveType.Removable);
            }
            else if (IsUserMount(root))
            {
                yield return new LocationItem(Path.GetFileName(root.TrimEnd('/')), root, true);
            }
        }
    }

    public static bool IsRemovable(string folder)
    {
        var full = Path.GetFullPath(folder);
        var drive = SafeDrives()
            .Where(d => full.StartsWith(d.RootDirectory.FullName, OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal))
            .OrderByDescending(d => d.RootDirectory.FullName.Length)
            .FirstOrDefault();
        if (drive is null) return false;
        return drive.DriveType == DriveType.Removable || (!OperatingSystem.IsWindows() && IsUserMount(drive.RootDirectory.FullName));
    }

    private static bool IsUserMount(string root) =>
        root.StartsWith("/media/", StringComparison.Ordinal) || root.StartsWith("/run/media/", StringComparison.Ordinal);

    private static IEnumerable<DriveInfo> SafeDrives()
    {
        DriveInfo[] drives;
        try
        {
            drives = DriveInfo.GetDrives();
        }
        catch (IOException)
        {
            yield break;
        }

        foreach (var d in drives)
        {
            bool ok;
            try
            {
                ok = d.IsReady && d.DriveType is DriveType.Fixed or DriveType.Removable or DriveType.Network;
            }
            catch (IOException)
            {
                ok = false;
            }

            if (ok) yield return d;
        }
    }

    private static string SafeLabel(DriveInfo d)
    {
        try
        {
            return d.VolumeLabel;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return string.Empty;
        }
    }
}
