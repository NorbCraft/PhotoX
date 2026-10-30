using PhotoX.Core.Formats;
using PhotoX.Core.Geometry;

namespace PhotoX.Core.Editing;

/// <summary>
/// Pixel geometry of a render: size after rotation, the crop rectangle and the output canvas
/// (borders for whole-photo mode and the 13×18-on-15×20 canvas, FR-049, FR-073).
/// </summary>
public sealed record RenderPlan(
    int RotatedWidth,
    int RotatedHeight,
    PixelRect Crop,
    int CanvasWidth,
    int CanvasHeight,
    int PhotoX,
    int PhotoY,
    double? EffectivePpi)
{
    /// <summary>True when the output would be pixel-identical to the input (no geometry change at all).</summary>
    public bool IsGeometryIdentity(int sourceWidth, int sourceHeight) =>
        RotatedWidth == sourceWidth && RotatedHeight == sourceHeight &&
        Crop == new PixelRect(0, 0, sourceWidth, sourceHeight) &&
        CanvasWidth == Crop.Width && CanvasHeight == Crop.Height;

    /// <summary>
    /// Output canvas for a crop of <paramref name="cropWidth"/>×<paramref name="cropHeight"/> pixels: borders for
    /// whole-photo mode, and the print sheet for 13×18 and custom sizes, at a consistent physical scale (plan §8.1).
    /// </summary>
    public static (int Width, int Height, double? Ppi) CanvasFor(int cropWidth, int cropHeight, EditRecipe recipe)
    {
        int canvasW = cropWidth, canvasH = cropHeight;
        if (recipe.Composition == Composition.WholeWithBorder)
        {
            // Smallest canvas of the print ratio that holds the whole photo without scaling.
            var ratio = recipe.CropRatio;
            if ((double)cropWidth / cropHeight > ratio)
            {
                canvasH = Math.Max(cropHeight, (int)Math.Round(cropWidth / ratio));
            }
            else
            {
                canvasW = Math.Max(cropWidth, (int)Math.Round(cropHeight * ratio));
            }
        }

        var format = recipe.Format;
        if (format is null)
        {
            return (canvasW, canvasH, null);
        }

        if (!format.HasCanvas)
        {
            return (canvasW, canvasH, Math.Max(canvasW, canvasH) / (Math.Max(format.WidthMm, format.HeightMm) / 25.4));
        }

        var portrait = recipe.Orientation == CropOrientation.Portrait;
        var (fw, fh, cw, ch) = portrait
            ? (format.WidthMm, format.HeightMm, format.CanvasWidthMm!.Value, format.CanvasHeightMm!.Value)
            : (format.HeightMm, format.WidthMm, format.CanvasHeightMm!.Value, format.CanvasWidthMm!.Value);
        var w = (int)Math.Round(canvasW * cw / fw);
        var h = (int)Math.Round(canvasH * ch / fh);
        return (w, h, Math.Max(w, h) / (Math.Max(cw, ch) / 25.4));
    }

    public static RenderPlan Create(int sourceWidth, int sourceHeight, EditRecipe recipe)
    {
        var w = recipe.QuarterTurns % 2 == 1 ? sourceHeight : sourceWidth;
        var h = recipe.QuarterTurns % 2 == 1 ? sourceWidth : sourceHeight;
        if (Math.Abs(recipe.Straighten) > 1e-6)
        {
            var (rw, rh) = CropMath.RotatedSize(w, h, recipe.Straighten);
            w = (int)Math.Round(rw);
            h = (int)Math.Round(rh);
        }

        var crop = recipe.Composition == Composition.Fill
            ? CropMath.ToPixelRect(recipe.Crop, w, h)
            : new PixelRect(0, 0, w, h);
        var (canvasW, canvasH, ppi) = CanvasFor(crop.Width, crop.Height, recipe);
        return new RenderPlan(w, h, crop, canvasW, canvasH, (canvasW - crop.Width) / 2, (canvasH - crop.Height) / 2, ppi);
    }
}
