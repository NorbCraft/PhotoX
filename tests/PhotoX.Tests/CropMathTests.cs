using PhotoX.Core.Editing;
using PhotoX.Core.Formats;
using PhotoX.Core.Geometry;

namespace PhotoX.Tests;

public class CropMathTests
{
    [Theory]
    [InlineData(4000, 3000, 4000, 2666.6667)] // landscape 4:3 → 2:3 landscape crop uses full width
    [InlineData(6000, 4000, 6000, 4000)]       // already 3:2 → whole image (FR-003)
    [InlineData(4000, 1000, 1500, 1000)]       // panorama → full height
    public void Default_crop_is_largest_centered_landscape(double w, double h, double cw, double ch)
    {
        var ratio = CropMath.OrientedRatio(PrintFormats.P10x15.PortraitRatio, CropMath.OrientationFor(w, h));
        var r = CropMath.LargestCentered(RectD.FromSize(w, h), ratio);
        Assert.Equal(cw, r.Width, 3);
        Assert.Equal(ch, r.Height, 3);
        Assert.Equal(w / 2, r.CenterX, 6);
        Assert.Equal(h / 2, r.CenterY, 6);
    }

    [Fact]
    public void Portrait_photo_gets_portrait_crop()
    {
        Assert.Equal(CropOrientation.Portrait, CropMath.OrientationFor(3000, 4000));
        var ratio = CropMath.OrientedRatio(PrintFormats.P10x15.PortraitRatio, CropOrientation.Portrait);
        var r = CropMath.LargestCentered(RectD.FromSize(3000, 4000), ratio);
        Assert.Equal(2.0 / 3.0, r.Width / r.Height, 9);
        Assert.Equal(4000, r.Height, 6);
    }

    [Fact]
    public void Pixel_rect_rounds_edges_and_stays_inside()
    {
        var p = CropMath.ToPixelRect(new NormalizedRect(0.1, 0.2, 0.5, 0.6), 1001, 999);
        Assert.Equal(new PixelRect(100, 200, 501, 599), p);
        var full = CropMath.ToPixelRect(NormalizedRect.Full, 640, 480);
        Assert.Equal(new PixelRect(0, 0, 640, 480), full);
    }

    [Fact]
    public void Resize_keeps_ratio_and_bounds_for_every_handle()
    {
        var bounds = RectD.FromSize(4000, 3000);
        var rnd = new Random(42);
        foreach (var handle in Enum.GetValues<CropHandle>().Where(h => h is not CropHandle.None and not CropHandle.Move))
        {
            for (var i = 0; i < 300; i++)
            {
                var ratio = rnd.Next(2) == 0 ? 1.5 : 2.0 / 3.0;
                var start = CropMath.Refit(new RectD(rnd.NextDouble() * 2000, rnd.NextDouble() * 1500, 300 + rnd.NextDouble() * 1500, 300), ratio, bounds);
                var r = CropMath.Resize(start, handle, rnd.NextDouble() * 6000 - 1000, rnd.NextDouble() * 5000 - 1000, ratio, bounds, 50);
                Assert.Equal(ratio, r.Width / r.Height, 6);
                Assert.True(bounds.Contains(r, 1e-6), $"{handle} {r}");
            }
        }
    }

    [Fact]
    public void Move_is_clamped_to_bounds()
    {
        var r = CropMath.Move(new RectD(10, 10, 100, 50), -500, 900, RectD.FromSize(400, 300));
        Assert.Equal(new RectD(0, 250, 100, 50), r);
    }

    [Fact]
    public void Valid_area_after_rotation_is_inside_rotated_image()
    {
        var (bw, bh) = CropMath.RotatedSize(4000, 3000, 5);
        var a = CropMath.ValidAreaAfterRotation(4000, 3000, 5);
        Assert.True(a.Width < 4000 && a.Height < 3000);
        Assert.Equal(4000.0 / 3000.0, a.Width / a.Height, 6);
        Assert.Equal(bw / 2, a.CenterX, 6);
        Assert.Equal(bh / 2, a.CenterY, 6);
        // All four corners of the valid area lie inside the rotated source rectangle.
        var t = 5 * Math.PI / 180;
        foreach (var (x, y) in new[] { (a.X, a.Y), (a.Right, a.Y), (a.X, a.Bottom), (a.Right, a.Bottom) })
        {
            var dx = x - bw / 2;
            var dy = y - bh / 2;
            var ux = dx * Math.Cos(t) + dy * Math.Sin(t);
            var uy = -dx * Math.Sin(t) + dy * Math.Cos(t);
            Assert.True(Math.Abs(ux) <= 2000 + 1e-6 && Math.Abs(uy) <= 1500 + 1e-6);
        }
    }

    [Fact]
    public void Plan_13x18_centres_photo_on_15x20_canvas()
    {
        var recipe = new EditRecipe { FormatId = "13x18", Orientation = CropOrientation.Portrait, Crop = new NormalizedRect(0, 0, 1, 1) };
        var plan = RenderPlan.Create(1300, 1800, recipe);
        Assert.Equal(1524, plan.CanvasWidth);
        Assert.Equal(2032, plan.CanvasHeight);
        Assert.Equal(0.75, (double)plan.CanvasWidth / plan.CanvasHeight, 3);
        Assert.Equal(112, plan.PhotoX);
        Assert.Equal(116, plan.PhotoY);
        Assert.Equal(254, plan.EffectivePpi!.Value, 0); // 2032 px over 8 in
    }

    [Fact]
    public void Plan_9x13_centres_photo_on_10x15_canvas()
    {
        // prompt_07 §1.1: 90 × 130 mm on the 101.6 × 152.4 mm sheet at the photo's own scale.
        var recipe = new EditRecipe { FormatId = "9x13", Orientation = CropOrientation.Portrait, Crop = new NormalizedRect(0, 0, 1, 1) };
        Assert.Equal(90.0 / 130.0, recipe.CropRatio, 6);
        var plan = RenderPlan.Create(1800, 2600, recipe);
        Assert.Equal(2032, plan.CanvasWidth);                                   // 1800 × 101.6 / 90
        Assert.Equal(3048, plan.CanvasHeight);                                  // 2600 × 152.4 / 130
        Assert.Equal(2.0 / 3.0, (double)plan.CanvasWidth / plan.CanvasHeight, 3);
        Assert.Equal(116, plan.PhotoX);
        Assert.Equal(224, plan.PhotoY);
        Assert.Equal(508, plan.EffectivePpi!.Value, 0);                         // 2600 px over 130 mm
        var landscape = RenderPlan.Create(2600, 1800, recipe with { Orientation = CropOrientation.Landscape });
        Assert.Equal((3048, 2032), (landscape.CanvasWidth, landscape.CanvasHeight));
    }

    [Fact]
    public void Plan_whole_photo_adds_borders_without_scaling()
    {
        var recipe = new EditRecipe { Composition = Composition.WholeWithBorder, Orientation = CropOrientation.Landscape };
        var plan = RenderPlan.Create(4000, 3000, recipe);
        Assert.Equal(new PixelRect(0, 0, 4000, 3000), plan.Crop);
        Assert.Equal(4500, plan.CanvasWidth);
        Assert.Equal(3000, plan.CanvasHeight);
        Assert.Equal(250, plan.PhotoX);
    }

    [Fact]
    public void Identity_plan_detected_only_without_changes()
    {
        var recipe = new EditRecipe { Orientation = CropOrientation.Landscape };
        Assert.True(RenderPlan.Create(600, 400, recipe).IsGeometryIdentity(600, 400));
        Assert.False(RenderPlan.Create(600, 400, recipe with { QuarterTurns = 1 }).IsGeometryIdentity(600, 400));
        Assert.False(RenderPlan.Create(600, 400, recipe with { Crop = new NormalizedRect(0, 0, 0.5, 0.5) }).IsGeometryIdentity(600, 400));
    }
}
