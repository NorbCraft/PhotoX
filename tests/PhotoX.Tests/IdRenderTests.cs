using ImageMagick;
using PhotoX.Core.Geometry;
using PhotoX.Core.Id;
using PhotoX.Imaging;
using PhotoX.Imaging.Id;
using PhotoX.Storage;

namespace PhotoX.Tests;

/// <summary>TS-14 / TS-15: ID framing render, background lift, sheet pixels, calibration and export names.</summary>
public class IdRenderTests
{
    [Fact]
    public void Framing_maps_the_frame_centre_to_the_chosen_source_point_at_exact_size()
    {
        using var src = Fixtures.CoordinateImage(600, 400);
        var before = src.ToByteArray(MagickFormat.Rgb);
        var framing = new IdFraming(300, 200, 120, 0);
        using var photo = IdRenderer.Render(src, framing, IdSizes.S3x4, new IdAdjustments());
        Assert.Equal((354u, 472u), (photo.Width, photo.Height));
        using var px = photo.GetPixels();
        var c = px.GetPixel(177, 236).ToColor()!;
        Assert.InRange(c.R * 599.0 / 65535, 298, 302);
        Assert.InRange(c.G * 399.0 / 65535, 198, 202);
        Assert.Equal(before, src.ToByteArray(MagickFormat.Rgb)); // the portrait itself is untouched
    }

    [Fact]
    public void Background_lift_brightens_light_neutral_background_but_not_the_face_area()
    {
        using var img = new MagickImage(new MagickColor("#C8C8C8"), 354, 472);
        var oval = IdRenderer.HeadOval(IdSizes.S3x4, 354, 472);
        new ImageMagick.Drawing.Drawables().FillColor(new MagickColor("#C8C8C8")).Draw(img);
        var faceBefore = img.GetPixels().GetPixel((int)oval.CenterX, (int)oval.CenterY).ToColor()!.R;
        BackgroundLift.Apply(img, 80, oval);
        using var px = img.GetPixels();
        Assert.True(px.GetPixel(10, 10).ToColor()!.R > 60000, "corner background lifted towards white");
        Assert.Equal(faceBefore, px.GetPixel((int)oval.CenterX, (int)oval.CenterY).ToColor()!.R);
    }

    [Fact]
    public void Sheet_places_exact_photos_keeps_white_strip_and_draws_guides_below_it()
    {
        using var photo = new MagickImage(new MagickColor("#204060"), (uint)IdSizes.S3x4.WidthPx, (uint)IdSizes.S3x4.HeightPx);
        var layout = SheetLayout.Compute(IdSizes.S3x4, new SheetOptions(8));
        using var page = SheetRenderer.Render(photo, layout, cutGuides: true);
        Assert.Equal((1200u, 1800u), (page.Width, page.Height));
        var k = 300 / 25.4;
        using var px = page.GetPixels();
        foreach (var r in layout.Photos)
        {
            var x = (int)Math.Round(r.X * k);
            var y = (int)Math.Round(r.Y * k);
            Assert.Equal("#204060", px.GetPixel(x + 5, y + 5).ToColor()!.ToHexString()[..7]);
            Assert.Equal("#204060", px.GetPixel(x + (int)photo.Width - 6, y + (int)photo.Height - 6).ToColor()!.ToHexString()[..7]);
            var stripY = y + (int)photo.Height + (int)(3.5 * k);          // middle of the 7 mm strip
            Assert.Equal("#FFFFFF", px.GetPixel(x + 30, stripY).ToColor()!.ToHexString()[..7]);
            var cutY = (int)Math.Round((r.Bottom + 7) * k);                 // guide below the strip
            Assert.Equal("#9A9A9A", px.GetPixel(x + 30, cutY).ToColor()!.ToHexString()[..7]);
        }
    }

    [Fact]
    public void Calibration_sheets_have_the_page_sizes()
    {
        using var a = CalibrationSheet.TenByFifteen();
        using var b = CalibrationSheet.FifteenByTwenty();
        Assert.Equal((1200u, 1800u), (a.Width, a.Height));
        Assert.Equal((1800u, 2400u), (b.Width, b.Height));
    }

    [Fact]
    public void Export_never_overwrites_and_adds_suffixes()
    {
        using var dir = new TempDir();
        var t = new DateTime(2026, 10, 29, 18, 45, 0);
        File.WriteAllText(dir.File("ID_20261029_1845.jpg"), "existing");
        var p1 = IdExporter.Write(dir.Path, [1, 2, 3], ".jpg", t);
        var p2 = IdExporter.Write(dir.Path, [4, 5, 6], ".jpg", t);
        Assert.Equal("ID_20261029_1845_1.jpg", Path.GetFileName(p1));
        Assert.Equal("ID_20261029_1845_2.jpg", Path.GetFileName(p2));
        Assert.Equal("existing", File.ReadAllText(dir.File("ID_20261029_1845.jpg")));
    }

    [Fact]
    public void Encoded_sheet_is_srgb_300ppi_jpeg()
    {
        using var page = new MagickImage(MagickColors.White, 1200, 1800);
        var bytes = IdOutput.Encode(page, ImageKind.Jpeg);
        using var back = new MagickImage(bytes);
        Assert.Equal(300, back.Density.X, 1);
        Assert.Equal(95u, back.Quality);
        Assert.NotNull(back.GetColorProfile());
    }
}
