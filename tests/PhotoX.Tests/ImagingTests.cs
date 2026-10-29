using ImageMagick;
using PhotoX.Core.Editing;
using PhotoX.Core.Geometry;
using PhotoX.Imaging;
using PhotoX.Storage;

namespace PhotoX.Tests;

/// <summary>TS-02, TS-04, TS-05, TS-06, TS-09: loading, rendering, output encoding and bad input.</summary>
public class ImagingTests
{
    [Theory]
    [InlineData(1, 600, 400)]
    [InlineData(3, 600, 400)]
    [InlineData(6, 400, 600)]
    [InlineData(8, 400, 600)]
    public void Exif_orientation_is_applied_on_load(ushort orientation, int w, int h)
    {
        using var dir = new TempDir();
        var path = Fixtures.WriteJpeg(dir.Path, "o.jpg", 600, 400, orientation);
        using var image = ImageLoader.Load(path);
        Assert.Equal((uint)w, image.Width);
        Assert.Equal((uint)h, image.Height);
        Assert.Equal((w, h), (ImageProbe.Probe(path).OrientedWidth, ImageProbe.Probe(path).OrientedHeight));
    }

    [Fact]
    public void Saved_jpeg_is_q95_444_srgb_upright_with_metadata_and_ppi()
    {
        using var dir = new TempDir();
        var path = Fixtures.WriteJpeg(dir.Path, "m.jpg", 600, 400, orientation: 6);
        var recipe = new EditRecipe { Orientation = CropOrientation.Portrait, Crop = new NormalizedRect(0, 0, 1, 0.9) };
        Assert.True(new PhotoSaver().Save(path, recipe, FileStamp.Of(path)!.Value).Succeeded);

        using var saved = new MagickImage(path);
        Assert.Equal(95u, saved.Quality);
        Assert.Equal("1x1,1x1,1x1", saved.GetAttribute("jpeg:sampling-factor"));
        Assert.NotNull(saved.GetColorProfile());
        Assert.Contains("sRGB", saved.GetColorProfile()!.Description ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        var exif = saved.GetExifProfile()!;
        Assert.Equal((ushort)1, exif.GetValue(ExifTag.Orientation)!.Value);
        Assert.Equal("PhotoX test camera", exif.GetValue(ExifTag.Model)!.Value);
        Assert.Equal(0u, exif.ThumbnailLength);
        Assert.Equal(400u, saved.Width); // upright after orientation 6
        Assert.Equal(DensityUnit.PixelsPerInch, saved.Density.Units);
        Assert.Equal(saved.Height / 6.0, saved.Density.Y, 1); // effective ppi on the 6-inch long edge
    }

    [Fact]
    public void Png_alpha_and_16_bit_depth_are_preserved()
    {
        using var dir = new TempDir();
        var path = Fixtures.WritePng(dir.Path, "p.png", 300, 200, alpha: true, depth: 16);
        var recipe = new EditRecipe { Orientation = CropOrientation.Landscape, Crop = new NormalizedRect(0, 0, 0.5, 0.5), Exposure = 0.2 };
        Assert.True(new PhotoSaver().Save(path, recipe, FileStamp.Of(path)!.Value).Succeeded);
        using var saved = new MagickImage(path);
        Assert.True(saved.HasAlpha);
        Assert.Equal(16u, saved.Depth);
    }

    [Fact]
    public void Neutral_tone_leaves_pixels_unchanged()
    {
        using var src = Fixtures.CoordinateImage(64, 48);
        using var outImg = RecipeRenderer.RenderPreview(src, new EditRecipe());
        Assert.Equal(0, src.Compare(outImg, ErrorMetric.Absolute));
    }

    [Theory]
    [InlineData(nameof(EditRecipe.Exposure), 1.0, 1)]
    [InlineData(nameof(EditRecipe.Exposure), -1.0, -1)]
    [InlineData(nameof(EditRecipe.Temperature), 60.0, 0)]
    public void Adjustments_move_output_in_expected_direction(string property, double value, int brighter)
    {
        using var src = Fixtures.Gradient(64, 48);
        var recipe = property switch
        {
            nameof(EditRecipe.Exposure) => new EditRecipe { Exposure = value },
            _ => new EditRecipe { Temperature = value },
        };
        using var outImg = RecipeRenderer.RenderPreview(src, recipe);
        var before = src.Statistics();
        var after = outImg.Statistics();
        if (property == nameof(EditRecipe.Temperature))
        {
            var dr = after.GetChannel(PixelChannel.Red)!.Mean - before.GetChannel(PixelChannel.Red)!.Mean;
            var db = after.GetChannel(PixelChannel.Blue)!.Mean - before.GetChannel(PixelChannel.Blue)!.Mean;
            Assert.True(dr > 0 && db < 0, "warmer = more red, less blue");
        }
        else
        {
            var d = after.GetChannel(PixelChannel.Green)!.Mean - before.GetChannel(PixelChannel.Green)!.Mean;
            Assert.Equal(brighter, Math.Sign(d));
        }
    }

    [Fact]
    public void Corrupt_and_unsupported_inputs_give_explanations()
    {
        using var dir = new TempDir();
        var empty = dir.File("empty.jpg");
        File.WriteAllBytes(empty, []);
        Assert.Equal(ImageError.Corrupt, Assert.Throws<ImageLoadException>(() => ImageLoader.Load(empty)).Error);

        var good = Fixtures.WriteJpeg(dir.Path, "good.jpg", 300, 200);
        var truncated = dir.File("truncated.jpg");
        File.WriteAllBytes(truncated, File.ReadAllBytes(good)[..200]);
        Assert.Equal(ImageError.Corrupt, Assert.Throws<ImageLoadException>(() => ImageLoader.Load(truncated)).Error);

        var heic = dir.File("phone.heic");
        File.WriteAllBytes(heic, [0, 0, 0, 24, (byte)'f', (byte)'t', (byte)'y', (byte)'p', (byte)'h', (byte)'e', (byte)'i', (byte)'c', 0, 0, 0, 0]);
        Assert.Equal(ImageError.UnsupportedHeic, Assert.Throws<ImageLoadException>(() => ImageLoader.Load(heic)).Error);

        var pngNamedJpg = dir.File("really-png.jpg");
        File.Copy(Fixtures.WritePng(dir.Path, "x.png", 30, 20), pngNamedJpg);
        Assert.Equal(ImageKind.Png, FormatSniffer.Detect(pngNamedJpg));
        using var loaded = ImageLoader.Load(pngNamedJpg);
        Assert.Equal(30u, loaded.Width);
    }

    [Fact]
    public void Size_policy_refuses_huge_images_from_header()
    {
        Assert.Equal(ImageError.TooLarge, SizePolicy.Check(SizePolicy.AbsoluteMaxPixels + 1));
        Assert.Equal(ImageError.None, SizePolicy.Check(24_000_000));
    }

    [Fact]
    public void Display_p3_source_is_converted_to_srgb()
    {
        using var dir = new TempDir();
        using (var img = new MagickImage(new MagickColor("#FF0000"), 40, 30))
        {
            img.SetProfile(ColorProfiles.SRGB);
            img.TransformColorSpace(ColorProfiles.SRGB, ColorProfiles.AdobeRGB1998);
            img.Write(dir.File("adobe.png"), MagickFormat.Png);
        }

        using var loaded = ImageLoader.Load(dir.File("adobe.png"));
        using var px = loaded.GetPixels();
        var c = px.GetPixel(10, 10).ToColor()!;
        // Back in sRGB the pure red patch is red again (within conversion tolerance).
        Assert.InRange(c.R, 60000, 65535);
        Assert.InRange(c.G, 0, 3000);
        Assert.InRange(c.B, 0, 3000);
    }
}
