using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using PhotoX.Core.Geometry;

namespace PhotoX.App.Controls;

/// <summary>A photo as drawn in a slot: its preview bitmap and the part of it that shows.</summary>
public sealed record CollageSceneItem(Bitmap Preview, NormalizedRect Crop);

/// <summary>Everything the collage canvas draws: the sheet and slots in millimetres, the photos and the selected slot.</summary>
public sealed record CollageScene(double PageWidthMm, double PageHeightMm, IReadOnlyList<RectD> Slots, IReadOnlyList<CollageSceneItem?> Photos, int Selected);

/// <summary>
/// Collage preview and arrangement (prompt_04 §5): the whole sheet in true proportions, each photo drawn into its slot
/// by the GPU (moving or zooming a photo costs only a redraw). Click selects a slot; dragging moves the photo inside
/// its slot; the wheel zooms; double-click resets; dragging the round handle onto another slot swaps the two photos.
/// </summary>
public sealed class CollageCanvas : Control
{
    public static readonly StyledProperty<CollageScene?> SceneProperty = AvaloniaProperty.Register<CollageCanvas, CollageScene?>(nameof(Scene));

    private const double PagePadding = 24;
    private const double HandleRadius = 15;
    private static readonly IBrush Paper = Brushes.White;
    private static readonly IBrush EmptyFill = new SolidColorBrush(Color.Parse("#E4E7EC"));
    private static readonly IBrush EmptyMark = new SolidColorBrush(Color.Parse("#9AA3B2"));
    private static readonly IPen SelectedPen = new Pen(new SolidColorBrush(Color.Parse("#3B82F6")), 3);
    private static readonly IPen TargetPen = new Pen(new SolidColorBrush(Color.Parse("#3B82F6")), 3, new DashStyle([4, 3], 0));
    private static readonly IBrush HandleFill = new SolidColorBrush(Color.FromArgb(230, 59, 130, 246));
    private static readonly IPen HandlePen = new Pen(Brushes.White, 1.8, lineCap: PenLineCap.Round);
    private static readonly BoxShadows PageShadow = BoxShadows.Parse("0 6 18 0 #90000000");

    private int _dragSlot = -1;
    private bool _moving;
    private int _target = -1;
    private Point _last;
    private Point _pointer;

    static CollageCanvas() => AffectsRender<CollageCanvas>(SceneProperty);

    public CollageCanvas()
    {
        ClipToBounds = true;
        DoubleTapped += (_, e) =>
        {
            if (SlotAt(e.GetPosition(this)) is >= 0 and var i) SlotReset?.Invoke(i);
        };
    }

    public event Action<int>? SlotSelected;

    public event Action<int, double, double>? Panned;

    public event Action<int, double>? Zoomed;

    public event Action<int>? SlotReset;

    public event Action<int, int>? Swapped;

    public CollageScene? Scene { get => GetValue(SceneProperty); set => SetValue(SceneProperty, value); }

    /// <summary>The sheet fitted into the control.</summary>
    private Rect Page
    {
        get
        {
            if (Scene is not { } s) return default;
            var k = Math.Max(0.01, Math.Min((Bounds.Width - 2 * PagePadding) / s.PageWidthMm, (Bounds.Height - 2 * PagePadding) / s.PageHeightMm));
            var w = s.PageWidthMm * k;
            var h = s.PageHeightMm * k;
            return new Rect((Bounds.Width - w) / 2, (Bounds.Height - h) / 2, w, h);
        }
    }

    private Rect SlotRect(int i)
    {
        var s = Scene!;
        var page = Page;
        var k = page.Width / s.PageWidthMm;
        var r = s.Slots[i];
        return new Rect(page.X + r.X * k, page.Y + r.Y * k, r.Width * k, r.Height * k);
    }

    private Point HandleCenter(int i) => SlotRect(i).TopLeft + new Point(HandleRadius + 8, HandleRadius + 8);

    private int SlotAt(Point p)
    {
        if (Scene is not { } s) return -1;
        for (var i = 0; i < s.Slots.Count; i++)
        {
            if (SlotRect(i).Contains(p)) return i;
        }

        return -1;
    }

    public override void Render(DrawingContext context)
    {
        if (Scene is not { } s) return;
        var page = Page;
        context.DrawRectangle(Paper, null, new RoundedRect(page), PageShadow);
        for (var i = 0; i < s.Slots.Count; i++)
        {
            var r = SlotRect(i);
            if (i < s.Photos.Count && s.Photos[i] is { } photo)
            {
                var size = photo.Preview.Size;
                var c = photo.Crop;
                var source = new Rect(c.X * size.Width, c.Y * size.Height, c.Width * size.Width, c.Height * size.Height);
                using (context.PushClip(r))
                {
                    context.DrawImage(photo.Preview, source, r);
                }
            }
            else
            {
                context.FillRectangle(EmptyFill, r);
                var m = Math.Min(r.Width, r.Height) * 0.12;
                var pen = new Pen(EmptyMark, Math.Max(2, m / 5), lineCap: PenLineCap.Round);
                context.DrawLine(pen, r.Center - new Point(m, 0), r.Center + new Point(m, 0));
                context.DrawLine(pen, r.Center - new Point(0, m), r.Center + new Point(0, m));
            }
        }

        if (s.Selected >= 0 && s.Selected < s.Slots.Count)
        {
            var sel = SlotRect(s.Selected);
            context.DrawRectangle(null, SelectedPen, sel.Deflate(1.5));
            if (s.Selected < s.Photos.Count && s.Photos[s.Selected] is not null) DrawHandle(context, HandleCenter(s.Selected));
        }

        if (_moving && _target >= 0) context.DrawRectangle(null, TargetPen, SlotRect(_target).Deflate(1.5));
        if (_moving) DrawHandle(context, _pointer);
    }

    /// <summary>The move handle: a round button with four arrows.</summary>
    private static void DrawHandle(DrawingContext context, Point c)
    {
        context.DrawEllipse(HandleFill, null, c, HandleRadius, HandleRadius);
        const double a = 8, h = 3;
        foreach (var (dx, dy) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
        {
            var tip = c + new Point(dx * a, dy * a);
            context.DrawLine(HandlePen, c, tip);
            context.DrawLine(HandlePen, tip, tip + new Point(-dx * h + dy * h, -dy * h + dx * h));
            context.DrawLine(HandlePen, tip, tip + new Point(-dx * h - dy * h, -dy * h - dx * h));
        }
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (Scene is not { } s || !e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;
        var p = e.GetPosition(this);
        // The handle of the selected slot starts a move to another slot; anywhere else selects and pans.
        var onHandle = s.Selected >= 0 && s.Selected < s.Photos.Count && s.Photos[s.Selected] is not null
            && Distance(p, HandleCenter(s.Selected)) <= HandleRadius + 2;
        _dragSlot = onHandle ? s.Selected : SlotAt(p);
        if (_dragSlot < 0) return;
        _moving = onHandle;
        _target = -1;
        _last = _pointer = p;
        if (!onHandle) SlotSelected?.Invoke(_dragSlot);
        e.Pointer.Capture(this);
        Cursor = new Cursor(onHandle ? StandardCursorType.DragMove : StandardCursorType.SizeAll);
        e.Handled = true;
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (_dragSlot < 0) return;
        var p = e.GetPosition(this);
        if (_moving)
        {
            _pointer = p;
            var t = SlotAt(p);
            _target = t != _dragSlot ? t : -1;
            InvalidateVisual();
        }
        else
        {
            var r = SlotRect(_dragSlot);
            Panned?.Invoke(_dragSlot, (p.X - _last.X) / r.Width, (p.Y - _last.Y) / r.Height);
        }

        _last = p;
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (_dragSlot < 0) return;
        if (_moving && _target >= 0) Swapped?.Invoke(_dragSlot, _target);
        EndDrag(e.Pointer);
    }

    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e);
        _dragSlot = -1;
        _moving = false;
        _target = -1;
        Cursor = Cursor.Default;
        InvalidateVisual();
    }

    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        base.OnPointerWheelChanged(e);
        if (SlotAt(e.GetPosition(this)) is not (>= 0 and var i)) return;
        SlotSelected?.Invoke(i);
        Zoomed?.Invoke(i, e.Delta.Y > 0 ? 1.08 : 1 / 1.08);
        e.Handled = true;
    }

    private void EndDrag(IPointer pointer)
    {
        _dragSlot = -1;
        _moving = false;
        _target = -1;
        pointer.Capture(null);
        Cursor = Cursor.Default;
        InvalidateVisual();
    }

    private static double Distance(Point a, Point b) => Math.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y));
}
