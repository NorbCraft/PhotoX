namespace PhotoX.Core.Geometry;

public enum CropOrientation
{
    Portrait,
    Landscape,
}

/// <summary>Crop handles: four corners and four edges.</summary>
public enum CropHandle
{
    None,
    Move,
    TopLeft,
    Top,
    TopRight,
    Right,
    BottomRight,
    Bottom,
    BottomLeft,
    Left,
}

/// <summary>Pure crop geometry (FR-003, FR-012). Ratios are width / height.</summary>
public static class CropMath
{
    /// <summary>Orientation that matches the photo; square photos are treated as landscape.</summary>
    public static CropOrientation OrientationFor(double width, double height) =>
        height > width ? CropOrientation.Portrait : CropOrientation.Landscape;

    /// <summary>Ratio (width/height) of a format given its portrait ratio and the wanted orientation.</summary>
    public static double OrientedRatio(double portraitRatio, CropOrientation orientation)
    {
        var r = portraitRatio <= 1 ? portraitRatio : 1 / portraitRatio;
        return orientation == CropOrientation.Portrait ? r : 1 / r;
    }

    /// <summary>Largest rectangle of <paramref name="ratio"/> centred in <paramref name="bounds"/>.</summary>
    public static RectD LargestCentered(RectD bounds, double ratio)
    {
        double w, h;
        if (bounds.Width / bounds.Height > ratio)
        {
            h = bounds.Height;
            w = h * ratio;
        }
        else
        {
            w = bounds.Width;
            h = w / ratio;
        }

        return new RectD(bounds.CenterX - w / 2, bounds.CenterY - h / 2, w, h);
    }

    /// <summary>
    /// Converts a normalized crop into whole pixels by rounding each edge, so the saved crop is the
    /// selected one to within half a pixel per edge (FR-004).
    /// </summary>
    public static PixelRect ToPixelRect(NormalizedRect crop, int imageWidth, int imageHeight)
    {
        var x0 = Clamp((int)Math.Round(crop.X * imageWidth, MidpointRounding.AwayFromZero), 0, imageWidth - 1);
        var y0 = Clamp((int)Math.Round(crop.Y * imageHeight, MidpointRounding.AwayFromZero), 0, imageHeight - 1);
        var x1 = Clamp((int)Math.Round((crop.X + crop.Width) * imageWidth, MidpointRounding.AwayFromZero), x0 + 1, imageWidth);
        var y1 = Clamp((int)Math.Round((crop.Y + crop.Height) * imageHeight, MidpointRounding.AwayFromZero), y0 + 1, imageHeight);
        return new PixelRect(x0, y0, x1 - x0, y1 - y0);
    }

    /// <summary>Moves a crop by (dx, dy), keeping it inside <paramref name="bounds"/>.</summary>
    public static RectD Move(RectD crop, double dx, double dy, RectD bounds)
    {
        var x = Math.Clamp(crop.X + dx, bounds.X, Math.Max(bounds.X, bounds.Right - crop.Width));
        var y = Math.Clamp(crop.Y + dy, bounds.Y, Math.Max(bounds.Y, bounds.Bottom - crop.Height));
        return crop with { X = x, Y = y };
    }

    /// <summary>
    /// Resizes a crop by dragging a handle to (px, py) while keeping <paramref name="ratio"/> and staying
    /// inside <paramref name="bounds"/>. The opposite corner (or edge) stays fixed.
    /// </summary>
    public static RectD Resize(RectD start, CropHandle handle, double px, double py, double ratio, RectD bounds, double minWidth)
    {
        minWidth = Math.Min(minWidth, Math.Min(bounds.Width, bounds.Height * ratio));
        switch (handle)
        {
            case CropHandle.TopLeft:
            case CropHandle.TopRight:
            case CropHandle.BottomLeft:
            case CropHandle.BottomRight:
            {
                var right = handle is CropHandle.TopRight or CropHandle.BottomRight;
                var down = handle is CropHandle.BottomLeft or CropHandle.BottomRight;
                var ax = right ? start.X : start.Right;
                var ay = down ? start.Y : start.Bottom;
                var w = Math.Max(right ? px - ax : ax - px, 0);
                var h = Math.Max(down ? py - ay : ay - py, 0);
                w = Math.Max(w, h * ratio);
                var maxW = right ? bounds.Right - ax : ax - bounds.X;
                var maxH = down ? bounds.Bottom - ay : ay - bounds.Y;
                w = Math.Clamp(w, minWidth, Math.Max(minWidth, Math.Min(maxW, maxH * ratio)));
                h = w / ratio;
                return new RectD(right ? ax : ax - w, down ? ay : ay - h, w, h);
            }

            case CropHandle.Left:
            case CropHandle.Right:
            {
                var right = handle == CropHandle.Right;
                var ax = right ? start.X : start.Right;
                var cy = start.CenterY;
                var w = Math.Max(right ? px - ax : ax - px, 0);
                var maxW = right ? bounds.Right - ax : ax - bounds.X;
                var maxH = 2 * Math.Min(cy - bounds.Y, bounds.Bottom - cy);
                w = Math.Clamp(w, minWidth, Math.Max(minWidth, Math.Min(maxW, maxH * ratio)));
                var h = w / ratio;
                return new RectD(right ? ax : ax - w, cy - h / 2, w, h);
            }

            case CropHandle.Top:
            case CropHandle.Bottom:
            {
                var down = handle == CropHandle.Bottom;
                var ay = down ? start.Y : start.Bottom;
                var cx = start.CenterX;
                var h = Math.Max(down ? py - ay : ay - py, 0);
                var maxH = down ? bounds.Bottom - ay : ay - bounds.Y;
                var maxW = 2 * Math.Min(cx - bounds.X, bounds.Right - cx);
                var w = Math.Clamp(h * ratio, minWidth, Math.Max(minWidth, Math.Min(maxW, maxH * ratio)));
                h = w / ratio;
                return new RectD(cx - w / 2, down ? ay : ay - h, w, h);
            }

            default:
                return start;
        }
    }

    /// <summary>Fits a crop of the given ratio inside bounds, keeping its centre where possible (used when the ratio changes).</summary>
    public static RectD Refit(RectD crop, double ratio, RectD bounds)
    {
        var largest = LargestCentered(bounds, ratio);
        var area = crop.Width * crop.Height;
        var w = Math.Min(largest.Width, Math.Sqrt(area * ratio));
        var h = w / ratio;
        var r = new RectD(crop.CenterX - w / 2, crop.CenterY - h / 2, w, h);
        return Move(r, 0, 0, bounds);
    }

    /// <summary>
    /// Largest axis-aligned rectangle with the image's aspect ratio that fits inside a W×H image rotated by
    /// <paramref name="degrees"/>, expressed in the coordinates of the rotated bounding box (FR-015).
    /// </summary>
    public static RectD ValidAreaAfterRotation(double width, double height, double degrees)
    {
        var a = Math.Abs(degrees) * Math.PI / 180;
        var sin = Math.Sin(a);
        var cos = Math.Cos(a);
        var bw = width * cos + height * sin;
        var bh = width * sin + height * cos;
        if (a < 1e-9)
        {
            return new RectD(0, 0, width, height);
        }

        // Scale factor of an inscribed rectangle with the same aspect as the image.
        var k = Math.Min(width / (width * cos + height * sin), height / (width * sin + height * cos));
        var iw = width * k;
        var ih = height * k;
        return new RectD((bw - iw) / 2, (bh - ih) / 2, iw, ih);
    }

    /// <summary>Size of the bounding box of a W×H image rotated by <paramref name="degrees"/>.</summary>
    public static (double Width, double Height) RotatedSize(double width, double height, double degrees)
    {
        var a = Math.Abs(degrees) * Math.PI / 180;
        return (width * Math.Cos(a) + height * Math.Sin(a), width * Math.Sin(a) + height * Math.Cos(a));
    }

    private static int Clamp(int v, int min, int max) => v < min ? min : v > max ? max : v;
}
