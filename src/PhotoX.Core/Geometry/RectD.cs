namespace PhotoX.Core.Geometry;

/// <summary>Axis-aligned rectangle in pixel (or any linear) units.</summary>
public readonly record struct RectD(double X, double Y, double Width, double Height)
{
    public double Right => X + Width;
    public double Bottom => Y + Height;
    public double CenterX => X + Width / 2;
    public double CenterY => Y + Height / 2;

    public static RectD FromSize(double width, double height) => new(0, 0, width, height);

    public bool Contains(RectD other, double tolerance = 1e-6) =>
        other.X >= X - tolerance && other.Y >= Y - tolerance &&
        other.Right <= Right + tolerance && other.Bottom <= Bottom + tolerance;

    public RectD Offset(double dx, double dy) => this with { X = X + dx, Y = Y + dy };
}

/// <summary>Integer pixel rectangle.</summary>
public readonly record struct PixelRect(int X, int Y, int Width, int Height)
{
    public int Right => X + Width;
    public int Bottom => Y + Height;
}

/// <summary>Rectangle in coordinates relative to an image (0..1 on both axes).</summary>
public readonly record struct NormalizedRect(double X, double Y, double Width, double Height)
{
    public static NormalizedRect Full { get; } = new(0, 0, 1, 1);

    public static NormalizedRect FromPixels(RectD r, double imageWidth, double imageHeight) =>
        new(r.X / imageWidth, r.Y / imageHeight, r.Width / imageWidth, r.Height / imageHeight);

    public RectD ToPixels(double imageWidth, double imageHeight) =>
        new(X * imageWidth, Y * imageHeight, Width * imageWidth, Height * imageHeight);
}
