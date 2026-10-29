using ImageMagick;

namespace PhotoX.Tests;

/// <summary>Synthetic test images generated at test time (no real photos are committed).</summary>
public static class Fixtures
{
    /// <summary>
    /// 16-bit image whose red channel encodes x and green encodes y (blue constant), so a crop's position can be
    /// read back from its pixels.
    /// </summary>
    public static MagickImage CoordinateImage(int width, int height)
    {
        var image = new MagickImage(MagickColors.Black, (uint)width, (uint)height);
        var data = new ushort[width * height * 3];
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var i = (y * width + x) * 3;
                data[i] = (ushort)(x * 65535 / Math.Max(1, width - 1));
                data[i + 1] = (ushort)(y * 65535 / Math.Max(1, height - 1));
                data[i + 2] = 20000;
            }
        }

        using (var pixels = image.GetPixels())
        {
            pixels.SetArea(0, 0, (uint)width, (uint)height, data);
        }

        image.ColorSpace = ColorSpace.sRGB;
        return image;
    }

    public static string WriteJpeg(string dir, string name, int width, int height, ushort orientation = 1)
    {
        using var image = Gradient(width, height);
        var exif = new ExifProfile();
        exif.SetValue(ExifTag.Orientation, orientation);
        exif.SetValue(ExifTag.Model, "PhotoX test camera");
        exif.SetValue(ExifTag.DateTimeOriginal, "2026:10:12 14:35:22");
        image.SetProfile(exif);
        image.Orientation = (OrientationType)orientation; // ImageMagick writes EXIF orientation from this attribute
        image.Quality = 92;
        var path = Path.Combine(dir, name);
        image.Write(path, MagickFormat.Jpeg);
        return path;
    }

    public static string WritePng(string dir, string name, int width, int height, bool alpha = false, int depth = 8)
    {
        using var image = CoordinateImage(width, height);
        if (alpha)
        {
            image.Alpha(AlphaOption.Set);
            image.Evaluate(Channels.Alpha, EvaluateOperator.Multiply, 0.5);
        }

        image.Depth = (uint)depth;
        var path = Path.Combine(dir, name);
        image.Write(path, MagickFormat.Png);
        return path;
    }

    public static MagickImage Gradient(int width, int height)
    {
        var image = CoordinateImage(width, height);
        image.Depth = 8;
        return image;
    }

    public static string Sha(string path) => Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(path)));
}

/// <summary>A temporary working folder deleted after the test.</summary>
public sealed class TempDir : IDisposable
{
    public TempDir()
    {
        Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "photox-tests", Guid.NewGuid().ToString("N")[..10]);
        Directory.CreateDirectory(Path);
    }

    public string Path { get; }

    public string File(string name) => System.IO.Path.Combine(Path, name);

    public void Dispose()
    {
        try
        {
            foreach (var f in Directory.EnumerateFiles(Path, "*", SearchOption.AllDirectories))
            {
                System.IO.File.SetAttributes(f, FileAttributes.Normal);
            }

            Directory.Delete(Path, true);
        }
        catch (IOException)
        {
        }
    }
}
