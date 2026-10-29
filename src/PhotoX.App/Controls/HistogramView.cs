using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using PhotoX.Core.Editing;

namespace PhotoX.App.Controls;

/// <summary>Live luminance + RGB histogram of the edited preview inside the crop (FR-043).</summary>
public sealed class HistogramView : Control
{
    public static readonly StyledProperty<Histogram?> HistogramProperty = AvaloniaProperty.Register<HistogramView, Histogram?>(nameof(Histogram));

    private static readonly IBrush Background = new SolidColorBrush(Color.Parse("#161616"));
    private static readonly IPen Frame = new Pen(new SolidColorBrush(Color.Parse("#333333")), 1);
    private static readonly IBrush LumaFill = new SolidColorBrush(Color.FromArgb(90, 210, 210, 210));
    private static readonly IPen Red = new Pen(new SolidColorBrush(Color.FromArgb(220, 255, 90, 82)), 1.4);
    private static readonly IPen Green = new Pen(new SolidColorBrush(Color.FromArgb(220, 52, 199, 89)), 1.4);
    private static readonly IPen Blue = new Pen(new SolidColorBrush(Color.FromArgb(220, 76, 141, 246)), 1.4);

    static HistogramView() => AffectsRender<HistogramView>(HistogramProperty);

    public Histogram? Histogram
    {
        get => GetValue(HistogramProperty);
        set => SetValue(HistogramProperty, value);
    }

    public override void Render(DrawingContext context)
    {
        var b = new Rect(Bounds.Size);
        context.DrawRectangle(Background, Frame, b, 6, 6);
        if (Histogram is not { Count: > 0 } h) return;
        var inner = b.Deflate(4);

        // Square-root scale so small populations remain visible; ignore the extreme bins for the scale.
        double max = 1;
        for (var i = 1; i < 255; i++)
        {
            max = Math.Max(max, Math.Max(Math.Max(h.Luma[i], h.Red[i]), Math.Max(h.Green[i], h.Blue[i])));
        }

        var scale = Math.Sqrt(max);
        Point P(int i, long v) => new(inner.X + inner.Width * i / 255.0, inner.Bottom - inner.Height * Math.Min(1, Math.Sqrt(v) / scale));

        var luma = new StreamGeometry();
        using (var g = luma.Open())
        {
            g.BeginFigure(new Point(inner.X, inner.Bottom), true);
            for (var i = 0; i < 256; i++) g.LineTo(P(i, h.Luma[i]));
            g.LineTo(new Point(inner.Right, inner.Bottom));
            g.EndFigure(true);
        }

        context.DrawGeometry(LumaFill, null, luma);
        foreach (var (bins, pen) in new[] { (h.Red, Red), (h.Green, Green), (h.Blue, Blue) })
        {
            var line = new StreamGeometry();
            using (var g = line.Open())
            {
                g.BeginFigure(P(0, bins[0]), false);
                for (var i = 1; i < 256; i++) g.LineTo(P(i, bins[i]));
                g.EndFigure(false);
            }

            context.DrawGeometry(null, pen, line);
        }
    }
}
