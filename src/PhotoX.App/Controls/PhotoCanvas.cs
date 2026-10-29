using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using PhotoX.Core.Geometry;

namespace PhotoX.App.Controls;

/// <summary>
/// Shows the preview and the ratio-locked crop frame (FR-003, FR-012). The crop is in preview-pixel coordinates;
/// dragging updates it live and <see cref="CropCommitted"/> fires once when the drag ends (one undo step).
/// </summary>
public sealed class PhotoCanvas : Control
{
    public static readonly StyledProperty<Bitmap?> SourceProperty = AvaloniaProperty.Register<PhotoCanvas, Bitmap?>(nameof(Source));
    public static readonly StyledProperty<Rect> CropRectProperty = AvaloniaProperty.Register<PhotoCanvas, Rect>(nameof(CropRect));
    public static readonly StyledProperty<double> CropRatioProperty = AvaloniaProperty.Register<PhotoCanvas, double>(nameof(CropRatio), 1.5);
    public static readonly StyledProperty<bool> ShowCropProperty = AvaloniaProperty.Register<PhotoCanvas, bool>(nameof(ShowCrop), true);

    private const double Padding = 24;
    private const double HandleReach = 16;
    private static readonly IBrush Shade = new SolidColorBrush(Color.FromArgb(150, 0, 0, 0));
    private static readonly IPen FramePen = new Pen(Brushes.White, 1.5);
    private static readonly IPen ThirdsPen = new Pen(new SolidColorBrush(Color.FromArgb(115, 255, 255, 255)), 1);
    private static readonly IPen HandlePen = new Pen(Brushes.White, 4, lineCap: PenLineCap.Square);

    private CropHandle _drag;
    private Rect _startCrop;
    private Point _startPoint;

    static PhotoCanvas()
    {
        AffectsRender<PhotoCanvas>(SourceProperty, CropRectProperty, ShowCropProperty);
        FocusableProperty.OverrideDefaultValue<PhotoCanvas>(false);
    }

    public event EventHandler<Rect>? CropCommitted;

    public Bitmap? Source
    {
        get => GetValue(SourceProperty);
        set => SetValue(SourceProperty, value);
    }

    public Rect CropRect
    {
        get => GetValue(CropRectProperty);
        set => SetValue(CropRectProperty, value);
    }

    public double CropRatio
    {
        get => GetValue(CropRatioProperty);
        set => SetValue(CropRatioProperty, value);
    }

    public bool ShowCrop
    {
        get => GetValue(ShowCropProperty);
        set => SetValue(ShowCropProperty, value);
    }

    /// <summary>Where the image is drawn inside the control (uniform fit).</summary>
    public Rect ImageDestination()
    {
        if (Source is null) return default;
        var size = Source.Size;
        var availW = Math.Max(1, Bounds.Width - 2 * Padding);
        var availH = Math.Max(1, Bounds.Height - 2 * Padding);
        var scale = Math.Min(availW / size.Width, availH / size.Height);
        var w = size.Width * scale;
        var h = size.Height * scale;
        return new Rect((Bounds.Width - w) / 2, (Bounds.Height - h) / 2, w, h);
    }

    public override void Render(DrawingContext context)
    {
        if (Source is null) return;
        var dest = ImageDestination();
        context.DrawImage(Source, new Rect(Source.Size), dest);
        if (!ShowCrop || CropRect.Width <= 0) return;

        var c = ToScreen(CropRect);
        var shade = new GeometryGroup { FillRule = FillRule.EvenOdd };
        shade.Children.Add(new RectangleGeometry(dest));
        shade.Children.Add(new RectangleGeometry(c));
        context.DrawGeometry(Shade, null, shade);
        for (var k = 1; k <= 2; k++)
        {
            context.DrawLine(ThirdsPen, new Point(c.X + c.Width * k / 3, c.Y), new Point(c.X + c.Width * k / 3, c.Bottom));
            context.DrawLine(ThirdsPen, new Point(c.X, c.Y + c.Height * k / 3), new Point(c.Right, c.Y + c.Height * k / 3));
        }

        context.DrawRectangle(null, FramePen, c);
        var l = Math.Min(24, Math.Min(c.Width, c.Height) / 3);
        foreach (var (x, y, sx, sy) in new[] { (c.X, c.Y, 1, 1), (c.Right, c.Y, -1, 1), (c.X, c.Bottom, 1, -1), (c.Right, c.Bottom, -1, -1) })
        {
            context.DrawLine(HandlePen, new Point(x, y + sy * l), new Point(x, y));
            context.DrawLine(HandlePen, new Point(x, y), new Point(x + sx * l, y));
        }

        var e = Math.Min(14, Math.Min(c.Width, c.Height) / 6);
        context.DrawLine(HandlePen, new Point(c.Center.X - e, c.Y), new Point(c.Center.X + e, c.Y));
        context.DrawLine(HandlePen, new Point(c.Center.X - e, c.Bottom), new Point(c.Center.X + e, c.Bottom));
        context.DrawLine(HandlePen, new Point(c.X, c.Center.Y - e), new Point(c.X, c.Center.Y + e));
        context.DrawLine(HandlePen, new Point(c.Right, c.Center.Y - e), new Point(c.Right, c.Center.Y + e));
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (!ShowCrop || Source is null || !e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;
        var handle = HitTest(e.GetPosition(this));
        if (handle == CropHandle.None) return;
        _drag = handle;
        _startCrop = CropRect;
        _startPoint = ToImage(e.GetPosition(this));
        e.Pointer.Capture(this);
        e.Handled = true;
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (Source is null) return;
        var p = e.GetPosition(this);
        if (_drag == CropHandle.None)
        {
            Cursor = CursorFor(ShowCrop ? HitTest(p) : CropHandle.None);
            return;
        }

        var ip = ToImage(p);
        var bounds = RectD.FromSize(Source.Size.Width, Source.Size.Height);
        var start = new RectD(_startCrop.X, _startCrop.Y, _startCrop.Width, _startCrop.Height);
        var next = _drag == CropHandle.Move
            ? CropMath.Move(start, ip.X - _startPoint.X, ip.Y - _startPoint.Y, bounds)
            : CropMath.Resize(start, _drag, ip.X, ip.Y, CropRatio, bounds, bounds.Width * 0.05);
        SetCurrentValue(CropRectProperty, new Rect(next.X, next.Y, next.Width, next.Height));
        e.Handled = true;
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (_drag == CropHandle.None) return;
        _drag = CropHandle.None;
        e.Pointer.Capture(null);
        if (CropRect != _startCrop) CropCommitted?.Invoke(this, CropRect);
        e.Handled = true;
    }

    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e);
        if (_drag != CropHandle.None)
        {
            _drag = CropHandle.None;
            if (CropRect != _startCrop) CropCommitted?.Invoke(this, CropRect);
        }
    }

    private CropHandle HitTest(Point p)
    {
        if (CropRect.Width <= 0) return CropHandle.None;
        var c = ToScreen(CropRect);
        bool Near(double a, double b) => Math.Abs(a - b) <= HandleReach;
        var nearLeft = Near(p.X, c.X);
        var nearRight = Near(p.X, c.Right);
        var nearTop = Near(p.Y, c.Y);
        var nearBottom = Near(p.Y, c.Bottom);
        var withinX = p.X > c.X - HandleReach && p.X < c.Right + HandleReach;
        var withinY = p.Y > c.Y - HandleReach && p.Y < c.Bottom + HandleReach;
        if (nearLeft && nearTop) return CropHandle.TopLeft;
        if (nearRight && nearTop) return CropHandle.TopRight;
        if (nearLeft && nearBottom) return CropHandle.BottomLeft;
        if (nearRight && nearBottom) return CropHandle.BottomRight;
        if (nearTop && withinX) return CropHandle.Top;
        if (nearBottom && withinX) return CropHandle.Bottom;
        if (nearLeft && withinY) return CropHandle.Left;
        if (nearRight && withinY) return CropHandle.Right;
        return c.Contains(p) ? CropHandle.Move : CropHandle.None;
    }

    private static Cursor CursorFor(CropHandle h) => new(h switch
    {
        CropHandle.TopLeft or CropHandle.BottomRight => StandardCursorType.TopLeftCorner,
        CropHandle.TopRight or CropHandle.BottomLeft => StandardCursorType.TopRightCorner,
        CropHandle.Top or CropHandle.Bottom => StandardCursorType.SizeNorthSouth,
        CropHandle.Left or CropHandle.Right => StandardCursorType.SizeWestEast,
        CropHandle.Move => StandardCursorType.SizeAll,
        _ => StandardCursorType.Arrow,
    });

    private Rect ToScreen(Rect r)
    {
        var d = ImageDestination();
        var k = d.Width / Source!.Size.Width;
        return new Rect(d.X + r.X * k, d.Y + r.Y * k, r.Width * k, r.Height * k);
    }

    private Point ToImage(Point p)
    {
        var d = ImageDestination();
        var k = Source!.Size.Width / d.Width;
        return new Point((p.X - d.X) * k, (p.Y - d.Y) * k);
    }
}
