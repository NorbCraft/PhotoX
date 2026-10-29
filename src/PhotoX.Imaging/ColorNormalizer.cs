using ImageMagick;

namespace PhotoX.Imaging;

/// <summary>Converts images to sRGB using embedded ICC profiles (FR-034, DEC-010).</summary>
public static class ColorNormalizer
{
    public static void ToSrgb(MagickImage image)
    {
        var profile = image.GetColorProfile();
        if (image.ColorSpace == ColorSpace.CMYK)
        {
            // CMYK JPEG: through its own profile, or a generic CMYK profile when untagged.
            if (profile is null)
            {
                image.TransformColorSpace(ColorProfiles.USWebCoatedSWOP, ColorProfiles.SRGB);
            }
            else
            {
                image.TransformColorSpace(ColorProfiles.SRGB);
            }

            image.ColorSpace = ColorSpace.sRGB;
            return;
        }

        if (profile is not null && profile.ColorSpace == ColorSpace.sRGB && !IsSrgbProfile(profile))
        {
            image.TransformColorSpace(ColorProfiles.SRGB);
        }
        else if (profile is not null && profile.ColorSpace != ColorSpace.sRGB && profile.ColorSpace != ColorSpace.Gray)
        {
            image.TransformColorSpace(ColorProfiles.SRGB);
        }
    }

    /// <summary>True when an RGB profile is (a variant of) sRGB, so no conversion is needed.</summary>
    public static bool IsSrgbProfile(IColorProfile profile)
    {
        var d = profile.Description ?? string.Empty;
        return d.Contains("sRGB", StringComparison.OrdinalIgnoreCase) || d.Contains("IEC61966-2", StringComparison.OrdinalIgnoreCase);
    }
}
