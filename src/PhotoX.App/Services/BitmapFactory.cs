using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using PhotoX.Imaging;

namespace PhotoX.App.Services;

public static class BitmapFactory
{
    public static WriteableBitmap Create(PixelData data)
    {
        var bmp = new WriteableBitmap(new PixelSize(data.Width, data.Height), new Vector(96, 96), PixelFormat.Bgra8888, AlphaFormat.Unpremul);
        using var fb = bmp.Lock();
        var rowBytes = data.Width * 4;
        for (var y = 0; y < data.Height; y++)
        {
            Marshal.Copy(data.Bgra, y * rowBytes, fb.Address + y * fb.RowBytes, rowBytes);
        }

        return bmp;
    }
}
