namespace PhotoX.Core.Id;

/// <summary>
/// Position of the portrait under the fixed ID frame (FR-062): the source point at the frame centre, the frame width
/// in source pixels (zoom) and the rotation in degrees. Resolution-independent.
/// </summary>
public sealed record IdFraming(double CenterX, double CenterY, double FrameWidthSource, double RotationDeg)
{
    /// <summary>Output pixels per source pixel for an output of <paramref name="outputWidthPx"/>.</summary>
    public double ScaleFor(int outputWidthPx) => outputWidthPx / FrameWidthSource;

    /// <summary>Initial framing: frame covers 80 % of the image height (or the full width), centred.</summary>
    public static IdFraming Initial(double imageWidth, double imageHeight, IdSize size)
    {
        var frameH = imageHeight * 0.8;
        var frameW = frameH * size.Ratio;
        if (frameW > imageWidth)
        {
            frameW = imageWidth;
        }

        return new IdFraming(imageWidth / 2, imageHeight / 2, frameW, 0);
    }

    /// <summary>
    /// Framing that puts a face with the given eye centres and estimated crown/chin on the size's guides (used by Auto Align).
    /// </summary>
    public static IdFraming FromFace(double leftEyeX, double leftEyeY, double rightEyeX, double rightEyeY, double headHeight, IdSize size)
    {
        var roll = Math.Atan2(rightEyeY - leftEyeY, rightEyeX - leftEyeX) * 180 / Math.PI;
        var frameH = headHeight / size.HeadFraction;
        var frameW = frameH * size.Ratio;
        var eyeX = (leftEyeX + rightEyeX) / 2;
        var eyeY = (leftEyeY + rightEyeY) / 2;
        // In the frame, the eyes sit at EyeFraction of the height; the frame centre is (0.5 − EyeFraction)·H below.
        var dy = (0.5 - size.EyeFraction) * frameH;
        var a = roll * Math.PI / 180;
        var cx = eyeX - Math.Sin(a) * dy;
        var cy = eyeY + Math.Cos(a) * dy;
        return new IdFraming(cx, cy, frameW, roll);
    }
}
