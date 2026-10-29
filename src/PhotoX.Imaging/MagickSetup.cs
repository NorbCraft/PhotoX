using ImageMagick;

namespace PhotoX.Imaging;

/// <summary>Process-wide Magick.NET guards against decompression bombs (FR-036).</summary>
public static class MagickSetup
{
    private static int _initialized;

    public static void EnsureInitialized()
    {
        if (Interlocked.Exchange(ref _initialized, 1) == 1) return;
        ResourceLimits.Width = 60_000;
        ResourceLimits.Height = 60_000;
        ResourceLimits.Area = (ulong)SizePolicy.AbsoluteMaxPixels * 2;
        ResourceLimits.MaxProfileSize = 64 * 1024 * 1024;
    }
}
