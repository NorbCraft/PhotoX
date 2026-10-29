using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using PhotoX.Core.Editing;

namespace PhotoX.App.Controls;

/// <summary>
/// Compact luminance histogram with one levels bar and three draggable triangles for the black point, midtones and
/// white point (FR-043, FR-044, prompt_03 §3). The midtone triangle sits where input maps to mid-grey:
/// black + (white − black) · 0.5^gamma, so it keeps its relative place when the end points move.
/// </summary>
public sealed class LevelsHistogram : Control
{
    public static readonly StyledProperty<Histogram?> HistogramProperty = AvaloniaProperty.Register<LevelsHistogram, Histogram?>(nameof(Histogram));
    public static readonly StyledProperty<double> BlackProperty = AvaloniaProperty.Register<LevelsHistogram, double>(nameof(Black), defaultBindingMode: Avalonia.Data.BindingMode.TwoWay);
    public static readonly StyledProperty<double> MidProperty = AvaloniaProperty.Register<LevelsHistogram, double>(nameof(Mid), 1, defaultBindingMode: Avalonia.Data.BindingMode.TwoWay);
    public static readonly StyledProperty<double> WhiteProperty = AvaloniaProperty.Register<LevelsHistogram, double>(nameof(White), 1, defaultBindingMode: Avalonia.Data.BindingMode.TwoWay);

    private const double Inset = 8;
    private const double BarHeight = 8;
    private const double HandleSize = 11;
    private const double MinGap = 0.05;
    private static readonly IBrush Background = new SolidColorBrush(Color.Parse("#161616"));
    private static readonly IPen Frame = new Pen(new SolidColorBrush(Color.Parse("#333333")), 1);
    private static readonly IBrush LumaFill = new SolidColorBrush(Color.FromArgb(150, 205, 205, 205));
    private static readonly IBrush Clipped = new SolidColorBrush(Color.FromArgb(120, 0, 0, 0));
    private static readonly IPen MarkPen = new Pen(new SolidColorBrush(Color.FromArgb(110, 255, 255, 255)), 1, new DashStyle([2, 3], 0));
    private static readonly IPen HandlePen = new Pen(new SolidColorBrush(Color.Parse("#A8A8A8")), 1.2);
    private static readonly IBrush BarBrush = new LinearGradientBrush
    {
        StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
        EndPoint = new RelativePoint(1, 0, RelativeUnit.Relative),
        GradientStops = { new GradientStop(Colors.Black, 0), new GradientStop(Colors.White, 1) },
    };

    private int _drag = -1;

    static LevelsHistogram() => AffectsRender<LevelsHistogram>(HistogramProperty, BlackProperty, MidProperty, WhiteProperty);

    public Histogram? Histogram { get => GetValue(HistogramProperty); set => SetValue(HistogramProperty, value); }

    /// <summary>Black point, 0..1.</summary>
    public double Black { get => GetValue(BlackProperty); set => SetValue(BlackProperty, value); }

    /// <summary>Midtone gamma (1 = neutral, larger = brighter).</summary>
    public double Mid { get => GetValue(MidProperty); set => SetValue(MidProperty, value); }

    /// <summary>White point, 0..1.</summary>
    public double White { get => GetValue(WhiteProperty); set => SetValue(WhiteProperty, value); }

    private Rect Plot => new(Inset, 4, Math.Max(1, Bounds.Width - 2 * Inset), Math.Max(1, Bounds.Height - BarHeight - HandleSize - 12));

    private double BarTop => Plot.Bottom + 5;

    private double X(double value) => Plot.X + Plot.Width * value;

    private double MidPosition => Black + (White - Black) * Math.Pow(0.5, Mid);

    public override void Render(DrawingContext context)
    {
        var plot = Plot;
        context.DrawRectangle(Background, Frame, new Rect(Bounds.Size), 6, 6);
        if (Histogram is { Count: > 0 } h)
        {
            // Square-root scale keeps small populations visible; the extreme bins are ignored for the scale.
            double max = 1;
            for (var i = 1; i < 255; i++) max = Math.Max(max, h.Luma[i]);
            var scale = Math.Sqrt(max);
            var luma = new StreamGeometry();
            using (var g = luma.Open())
            {
                g.BeginFigure(new Point(plot.X, plot.Bottom), true);
                for (var i = 0; i < 256; i++) g.LineTo(new Point(X(i / 255.0), plot.Bottom - plot.Height * Math.Min(1, Math.Sqrt(h.Luma[i]) / scale)));
                g.LineTo(new Point(plot.Right, plot.Bottom));
                g.EndFigure(true);
            }

            context.DrawGeometry(LumaFill, null, luma);
        }

        // Inputs below the black point or above the white point become pure black or white.
        context.FillRectangle(Clipped, new Rect(plot.X, plot.Y, X(Black) - plot.X, plot.Height));
        context.FillRectangle(Clipped, new Rect(X(White), plot.Y, plot.Right - X(White), plot.Height));
        foreach (var v in new[] { Black, MidPosition, White })
        {
            context.DrawLine(MarkPen, new Point(X(v), plot.Y), new Point(X(v), plot.Bottom));
        }

        context.DrawRectangle(BarBrush, null, new Rect(plot.X, BarTop, plot.Width, BarHeight), 2, 2);
        DrawHandle(context, Black, Colors.Black);
        DrawHandle(context, MidPosition, Color.Parse("#808080"));
        DrawHandle(context, White, Colors.White);
    }

    private void DrawHandle(DrawingContext context, double value, Color fill)
    {
        var x = X(value);
        var top = BarTop + BarHeight + 1;
        var triangle = new StreamGeometry();
        using (var g = triangle.Open())
        {
            g.BeginFigure(new Point(x, top), true);
            g.LineTo(new Point(x + HandleSize / 2 + 1, top + HandleSize));
            g.LineTo(new Point(x - HandleSize / 2 - 1, top + HandleSize));
            g.EndFigure(true);
        }

        context.DrawGeometry(new SolidColorBrush(fill), HandlePen, triangle);
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;
        var x = e.GetPosition(this).X;
        // The nearest triangle; on a tie (handles on top of each other) prefer the one that can move towards the pointer.
        var candidates = new[] { (0, X(Black)), (1, X(MidPosition)), (2, X(White)) };
        _drag = candidates.OrderBy(c => Math.Abs(c.Item2 - x)).ThenBy(c => c.Item1 == 1 ? 0 : 1).First().Item1;
        e.Pointer.Capture(this);
        Move(x);
        e.Handled = true;
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (_drag < 0) return;
        Move(e.GetPosition(this).X);
        e.Handled = true;
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (_drag < 0) return;
        _drag = -1;
        e.Pointer.Capture(null);
        e.Handled = true;
    }

    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e);
        _drag = -1;
    }

    private void Move(double x)
    {
        var v = Math.Clamp((x - Plot.X) / Plot.Width, 0, 1);
        switch (_drag)
        {
            case 0:
                SetCurrentValue(BlackProperty, Math.Round(Math.Clamp(v, 0, White - MinGap), 3));
                break;
            case 2:
                SetCurrentValue(WhiteProperty, Math.Round(Math.Clamp(v, Black + MinGap, 1), 3));
                break;
            case 1:
                var t = Math.Clamp((v - Black) / Math.Max(1e-6, White - Black), 0.0625, 0.84);
                SetCurrentValue(MidProperty, Math.Round(Math.Log(t) / Math.Log(0.5), 2));
                break;
        }
    }
}
