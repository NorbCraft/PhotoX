using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using PhotoX.Core.Geometry;

namespace PhotoX.App.Controls;

/// <summary>
/// Shows the preview and the ratio-locked crop frame (FR-003, FR-012). Coordinates are preview pixels of the
/// straightened photo (its bounding box, <see cref="ContentSize"/>); the source bitmap is drawn rotated by
/// <see cref="Angle"/>, so straightening needs no re-rendering. Dragging updates the crop live and
/// <see cref="CropCommitted"/> fires once when the drag ends (one undo step). In the result view the output canvas
/// (<see cref="ResultFrame"/>, white) is fitted to the control and only the crop is drawn (prompt_03 §4).
/// </summary>
public sealed class PhotoCanvas : Control
{
    public static readonly StyledProperty<Bitmap?> SourceProperty = AvaloniaProperty.Register<PhotoCanvas, Bitmap?>(nameof(Source));
    public static readonly StyledProperty<Size> ContentSizeProperty = AvaloniaProperty.Register<PhotoCanvas, Size>(nameof(ContentSize));
    public static readonly StyledProperty<double> AngleProperty = AvaloniaProperty.Register<PhotoCanvas, double>(nameof(Angle));
    public static readonly StyledProperty<Rect> CropRectProperty = AvaloniaProperty.Register<PhotoCanvas, Rect>(nameof(CropRect));
    public static readonly StyledProperty<double> CropRatioProperty = AvaloniaProperty.Register<PhotoCanvas, double>(nameof(CropRatio), 1.5);
    public static readonly StyledProperty<bool> ShowCropProperty = AvaloniaProperty.Register<PhotoCanvas, bool>(nameof(ShowCrop), true);
    public static readonly StyledProperty<Rect> CropBoundsProperty = AvaloniaProperty.Register<PhotoCanvas, Rect>(nameof(CropBounds));
    public static readonly StyledProperty<bool> ShowResultProperty = AvaloniaProperty.Register<PhotoCanvas, bool>(nameof(ShowResult));
    public static readonly StyledProperty<Rect> ResultFrameProperty = AvaloniaProperty.Register<PhotoCanvas, Rect>(nameof(ResultFrame));
    public static readonly StyledProperty<Rect> ResultCropProperty = AvaloniaProperty.Register<PhotoCanvas, Rect>(nameof(ResultCrop));

    private const double Padding = 24;
    private const double HandleReach = 16;
    private static readonly IBrush Shade = new SolidColorBrush(Color.FromArgb(150, 0, 0, 0));
    private static readonly IPen FramePen = new Pen(Brushes.White, 1.5);
    private static readonly IPen ThirdsPen = new Pen(new SolidColorBrush(Color.FromArgb(115, 255, 255, 255)), 1);
    private static readonly IPen HandlePen = new Pen(Brushes.White, 4, lineCap: PenLineCap.Square);
    private static readonly IPen ResultEdgePen = new Pen(new SolidColorBrush(Color.FromArgb(60, 255, 255, 255)), 1);

    private CropHandle _drag;
    private Rect _startCrop;
    private Point _startPoint;

    static PhotoCanvas()
    {
        AffectsRender<PhotoCanvas>(SourceProperty, ContentSizeProperty, AngleProperty, CropRectProperty, ShowCropProperty,
            ShowResultProperty, ResultFrameProperty, ResultCropProperty);
        FocusableProperty.OverrideDefaultValue<PhotoCanvas>(false);
    }

    public PhotoCanvas()
    {
        DoubleTapped += OnDoubleTapped;
    }

    public event EventHandler<Rect>? CropCommitted;

    /// <summary>Double-click inside the crop frame (apply the crop) or on the result view (edit the crop again).</summary>
    public event EventHandler? CropToggleRequested;

    public Bitmap? Source { get => GetValue(SourceProperty); set => SetValue(SourceProperty, value); }

    /// <summary>Size of the straightened photo's bounding box in preview pixels (defaults to the bitmap size).</summary>
    public Size ContentSize { get => GetValue(ContentSizeProperty); set => SetValue(ContentSizeProperty, value); }

    /// <summary>Straightening angle in degrees (positive = clockwise), as in the saved photo.</summary>
    public double Angle { get => GetValue(AngleProperty); set => SetValue(AngleProperty, value); }

    public Rect CropRect { get => GetValue(CropRectProperty); set => SetValue(CropRectProperty, value); }

    public double CropRatio { get => GetValue(CropRatioProperty); set => SetValue(CropRatioProperty, value); }

    /// <summary>Area the crop must stay inside (valid area after straightening), in preview pixels; empty = whole image.</summary>
    public Rect CropBounds { get => GetValue(CropBoundsProperty); set => SetValue(CropBoundsProperty, value); }

    public bool ShowCrop { get => GetValue(ShowCropProperty); set => SetValue(ShowCropProperty, value); }

    public bool ShowResult { get => GetValue(ShowResultProperty); set => SetValue(ShowResultProperty, value); }

    /// <summary>The output canvas (crop plus white borders) in preview pixels, shown in the result view.</summary>
    public Rect ResultFrame { get => GetValue(ResultFrameProperty); set => SetValue(ResultFrameProperty, value); }

    /// <summary>The part of the photo that appears in the output, in preview pixels.</summary>
    public Rect ResultCrop { get => GetValue(ResultCropProperty); set => SetValue(ResultCropProperty, value); }

    private Size Content => ContentSize.Width > 0 ? ContentSize : Source?.Size ?? default;

    private bool IsResultView => ShowResult && ResultFrame.Width > 0 && ResultCrop.Width > 0;

    /// <summary>The visible area in content coordinates: the whole photo, or the output canvas in the result view.</summary>
    private Rect View => IsResultView ? ResultFrame : new Rect(Content);

    /// <summary>Where the visible area is drawn inside the control (uniform fit).</summary>
    public Rect ImageDestination()
    {
        var view = View;
        if (Source is null || view.Width <= 0) return default;
        var availW = Math.Max(1, Bounds.Width - 2 * Padding);
        var availH = Math.Max(1, Bounds.Height - 2 * Padding);
        var scale = Math.Min(availW / view.Width, availH / view.Height);
        var w = view.Width * scale;
        var h = view.Height * scale;
        return new Rect((Bounds.Width - w) / 2, (Bounds.Height - h) / 2, w, h);
    }

    public override void Render(DrawingContext context)
    {
        if (Source is null) return;
        var dest = ImageDestination();
        var k = dest.Width / View.Width;
        if (IsResultView)
        {
            context.FillRectangle(Brushes.White, dest);
            using (context.PushClip(ToScreen(ResultCrop)))
            {
                DrawPhoto(context, dest, k);
            }

            context.DrawRectangle(null, ResultEdgePen, dest);
            return;
        }

        DrawPhoto(context, dest, k);
        if (!ShowCrop || CropRect.Width <= 0) return;

        var c = ToScreen(CropRect);
        var shade = new GeometryGroup { FillRule = FillRule.EvenOdd };
        shade.Children.Add(new RectangleGeometry(dest));
        shade.Children.Add(new RectangleGeometry(c));
        context.DrawGeometry(Shade, null, shade);
        for (var i = 1; i <= 2; i++)
        {
            context.DrawLine(ThirdsPen, new Point(c.X + c.Width * i / 3, c.Y), new Point(c.X + c.Width * i / 3, c.Bottom));
            context.DrawLine(ThirdsPen, new Point(c.X, c.Y + c.Height * i / 3), new Point(c.Right, c.Y + c.Height * i / 3));
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

    /// <summary>Draws the bitmap rotated about its centre into the centre of the content box, then maps content to screen.</summary>
    private void DrawPhoto(DrawingContext context, Rect dest, double k)
    {
        var src = Source!.Size;
        var content = Content;
        var view = View;
        var transform = Matrix.CreateTranslation(-src.Width / 2, -src.Height / 2)
            * Matrix.CreateRotation(Angle * Math.PI / 180)
            * Matrix.CreateTranslation(content.Width / 2 - view.X, content.Height / 2 - view.Y)
            * Matrix.CreateScale(k, k)
            * Matrix.CreateTranslation(dest.X, dest.Y);
        using (context.PushTransform(transform))
        {
            context.DrawImage(Source, new Rect(src));
        }
    }

    private void OnDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (Source is null) return;
        var p = e.GetPosition(this);
        if (IsResultView ? ImageDestination().Contains(p) : ShowCrop && ToScreen(CropRect).Contains(p))
        {
            CropToggleRequested?.Invoke(this, EventArgs.Empty);
            e.Handled = true;
        }
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (!ShowCrop || IsResultView || Source is null || !e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;
        var handle = HitTest(e.GetPosition(this));
        if (handle == CropHandle.None) return;
        _drag = handle;
        _startCrop = CropRect;
        _startPoint = ToContent(e.GetPosition(this));
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
            Cursor = CursorFor(ShowCrop && !IsResultView ? HitTest(p) : CropHandle.None);
            return;
        }

        var ip = ToContent(p);
        var bounds = CropBounds.Width > 0
            ? new RectD(CropBounds.X, CropBounds.Y, CropBounds.Width, CropBounds.Height)
            : RectD.FromSize(Content.Width, Content.Height);
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
        var view = View;
        var k = d.Width / view.Width;
        return new Rect(d.X + (r.X - view.X) * k, d.Y + (r.Y - view.Y) * k, r.Width * k, r.Height * k);
    }

    private Point ToContent(Point p)
    {
        var d = ImageDestination();
        var view = View;
        var k = view.Width / d.Width;
        return new Point((p.X - d.X) * k + view.X, (p.Y - d.Y) * k + view.Y);
    }
}
