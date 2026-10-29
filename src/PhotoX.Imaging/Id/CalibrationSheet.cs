using ImageMagick;
using ImageMagick.Drawing;
using PhotoX.Core.Id;

namespace PhotoX.Imaging.Id;

/// <summary>
/// Printable test sheets (FR-071): mm rulers on all edges plus reference rectangles. Print through the Citizen
/// software and measure to verify ID sizes, the 7 mm strip, the centred 13×18 on 15×20, and any driver scaling.
/// </summary>
public static class CalibrationSheet
{
    public static MagickImage TenByFifteen()
    {
        var page = Blank(101.6, 152.4);
        var d = Pen();
        Rulers(d, 101.6, 152.4);
        Rect(d, 12, 20, 30, 40);
        Rect(d, 12, 20, 30, 47, dashed: true);  // 3×4 + 7 mm strip
        Rect(d, 55, 20, 35, 45);
        Rect(d, 25, 80, 50.8, 50.8);             // 2×2 in
        d.Draw(page);
        Label(page, "PhotoX 10x15 calibration: 30x40 (+7 = 47), 35x45, 50.8x50.8 mm; page 101.6 x 152.4 mm", 6, 145);
        return page;
    }

    public static MagickImage FifteenByTwenty()
    {
        var page = Blank(152.4, 203.2);
        var d = Pen();
        Rulers(d, 152.4, 203.2);
        Rect(d, (152.4 - 130) / 2, (203.2 - 180) / 2, 130, 180);
        d.Draw(page);
        Label(page, "PhotoX 15x20 calibration: centred 13x18 frame = 130 x 180 mm", 14, 199);
        return page;
    }

    private static double K => IdSize.Ppi / 25.4;

    private static MagickImage Blank(double wMm, double hMm)
    {
        var page = new MagickImage(MagickColors.White, (uint)Units.MmToPx(wMm, IdSize.Ppi), (uint)Units.MmToPx(hMm, IdSize.Ppi));
        page.Density = new Density(IdSize.Ppi, IdSize.Ppi, DensityUnit.PixelsPerInch);
        return page;
    }

    private static Drawables Pen()
    {
        var d = new Drawables();
        d.StrokeColor(MagickColors.Black).StrokeWidth(1.5).FillColor(MagickColors.Transparent);
        return d;
    }

    private static void Rect(Drawables d, double x, double y, double w, double h, bool dashed = false)
    {
        if (dashed) d.StrokeDashArray(8, 6);
        d.Rectangle(x * K, y * K, (x + w) * K, (y + h) * K);
        if (dashed) d.StrokeDashArray(Array.Empty<double>());
    }

    private static void Rulers(Drawables d, double wMm, double hMm)
    {
        for (var mm = 0; mm <= (int)wMm; mm++)
        {
            var len = mm % 10 == 0 ? 6 : mm % 5 == 0 ? 4 : 2;
            d.Line(mm * K, 0, mm * K, len * K);
            d.Line(mm * K, hMm * K, mm * K, (hMm - len) * K);
        }

        for (var mm = 0; mm <= (int)hMm; mm++)
        {
            var len = mm % 10 == 0 ? 6 : mm % 5 == 0 ? 4 : 2;
            d.Line(0, mm * K, len * K, mm * K);
            d.Line(wMm * K, mm * K, (wMm - len) * K, mm * K);
        }
    }

    private static void Label(MagickImage page, string text, double xMm, double yMm)
    {
        try
        {
            new Drawables().FillColor(MagickColors.Black).FontPointSize(22).Text(xMm * K, yMm * K, text).Draw(page);
        }
        catch (MagickException)
        {
            // Text needs a system font; the rulers and rectangles are what matter for measuring.
        }
    }
}
