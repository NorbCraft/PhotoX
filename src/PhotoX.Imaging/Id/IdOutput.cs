using ImageMagick;

namespace PhotoX.Imaging.Id;

/// <summary>Encodes ID sheets and calibration pages: sRGB, 300 ppi, JPEG q95 4:4:4 or PNG (FR-068).</summary>
public static class IdOutput
{
    public static byte[] Encode(MagickImage page, ImageKind kind, int jpegQuality = 95)
    {
        page.Density = new Density(300, 300, DensityUnit.PixelsPerInch);
        return ImageEncoder.Encode(page, kind, new OutputOptions(jpegQuality, 300));
    }
}
