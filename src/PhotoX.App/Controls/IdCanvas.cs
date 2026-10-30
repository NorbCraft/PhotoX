using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using PhotoX.Core.Id;

namespace PhotoX.App.Controls;

/// <summary>
/// ID framing view (FR-062): the preview viewport with the fixed frame (of the size's ratio) in its centre, dimmed
/// surroundings and, for sizes with guides, the face guides (oval, crown, eye, chin and centre lines). Dragging moves the photo; the wheel zooms; Shift+wheel rotates.
/// </summary>
public sealed class IdCanvas : Control
{
    public static readonly StyledProperty<Bitmap?> SourceProperty = AvaloniaProperty.Register<IdCanvas, Bitmap?>(nameof(Source));
    public static readonly StyledProperty<IdSize?> SizeProperty = AvaloniaProperty.Register<IdCanvas, IdSize?>(nameof(Size));
    public static readonly StyledProperty<double> FrameWidthProperty = AvaloniaProperty.Register<IdCanvas, double>(nameof(FrameWidth));
    public static readonly StyledProperty<double> FrameHeightProperty = AvaloniaProperty.Register<IdCanvas, double>(nameof(FrameHeight));

    private static readonly IBrush Shade = new SolidColorBrush(Color.FromArgb(150, 0, 0, 0));
    private static readonly IPen FramePen = new Pen(Brushes.White, 2);
    private static readonly IPen GuidePen = new Pen(new SolidColorBrush(Color.Parse("#5BE37D")), 1.6);
    private static readonly IPen GuideDash = new Pen(new SolidColorBrush(Color.Parse("#5BE37D")), 1.2, new DashStyle([6, 4], 0));
    private static readonly IPen CenterPen = new Pen(new SolidColorBrush(Color.FromArgb(160, 91, 227, 125)), 1, new DashStyle([3, 5], 0));

    private Point? _last;

    static IdCanvas() => AffectsRender<IdCanvas>(SourceProperty, SizeProperty, FrameWidthProperty, FrameHeightProperty);

    public event Action<double, double>? Moved;

    public event Action<double>? Zoomed;

    public event Action<double>? Rotated;

    public Bitmap? Source { get => GetValue(SourceProperty); set => SetValue(SourceProperty, value); }

    public IdSize? Size { get => GetValue(SizeProperty); set => SetValue(SizeProperty, value); }

    public double FrameWidth { get => GetValue(FrameWidthProperty); set => SetValue(FrameWidthProperty, value); }

    public double FrameHeight { get => GetValue(FrameHeightProperty); set => SetValue(FrameHeightProperty, value); }

    private double Scale => Source is null ? 1 : Math.Min(Bounds.Width / Source.Size.Width, Bounds.Height / Source.Size.Height);

    public override void Render(DrawingContext context)
    {
        if (Source is null || Size is null) return;
        var k = Scale;
        var w = Source.Size.Width * k;
        var h = Source.Size.Height * k;
        var dest = new Rect((Bounds.Width - w) / 2, (Bounds.Height - h) / 2, w, h);
        context.DrawImage(Source, new Rect(Source.Size), dest);
        var fw = FrameWidth * k;
        var fh = FrameHeight * k;
        var f = new Rect(dest.Center.X - fw / 2, dest.Center.Y - fh / 2, fw, fh);
        var shade = new GeometryGroup { FillRule = FillRule.EvenOdd };
        shade.Children.Add(new RectangleGeometry(dest));
        shade.Children.Add(new RectangleGeometry(f));
        context.DrawGeometry(Shade, null, shade);
        context.DrawRectangle(null, FramePen, f);
        if (!Size.Guides) return; // framed by hand (9×12, prompt_04 §8)

        double Y(double fraction) => f.Y + fh * fraction;
        context.DrawLine(GuidePen, new Point(f.X, Y(Size.CrownFraction)), new Point(f.Right, Y(Size.CrownFraction)));
        context.DrawLine(GuideDash, new Point(f.X, Y(Size.EyeFraction)), new Point(f.Right, Y(Size.EyeFraction)));
        context.DrawLine(GuidePen, new Point(f.X, Y(Size.ChinFraction)), new Point(f.Right, Y(Size.ChinFraction)));
        context.DrawLine(CenterPen, new Point(f.Center.X, f.Y), new Point(f.Center.X, f.Bottom));
        var head = fh * Size.HeadFraction;
        context.DrawEllipse(null, GuidePen, new Point(f.Center.X, Y(Size.CrownFraction) + head / 2), head * 0.37, head / 2);
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        _last = e.GetPosition(this);
        e.Pointer.Capture(this);
        Cursor = new Cursor(StandardCursorType.SizeAll);
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (_last is not { } last) return;
        var p = e.GetPosition(this);
        var k = Scale;
        Moved?.Invoke((p.X - last.X) / k, (p.Y - last.Y) / k);
        _last = p;
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        _last = null;
        e.Pointer.Capture(null);
        Cursor = Cursor.Default;
    }

    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        base.OnPointerWheelChanged(e);
        if (e.KeyModifiers.HasFlag(KeyModifiers.Shift)) Rotated?.Invoke(Math.Sign(e.Delta.Y + e.Delta.X) * 0.5);
        else Zoomed?.Invoke(e.Delta.Y > 0 ? 1.05 : 1 / 1.05);
        e.Handled = true;
    }
}
