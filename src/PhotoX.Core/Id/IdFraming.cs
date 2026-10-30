namespace PhotoX.Core.Id;

/// <summary>
/// Position of the portrait under the fixed ID frame (FR-062): the source point at the frame centre, the frame width
/// in source pixels (zoom), the fine rotation in degrees and the quarter turns (90° clockwise each, prompt_06 §5) for
/// photos taken in the wrong orientation. Resolution-independent; the portrait file itself is never turned.
/// </summary>
public sealed record IdFraming(double CenterX, double CenterY, double FrameWidthSource, double RotationDeg, int QuarterTurns = 0)
{
    /// <summary>
    /// The angle of the frame's horizontal in the stored photo: the fine rotation less the quarter turns. The frame is
    /// made upright by rotating the photo by −Angle.
    /// </summary>
    public double Angle => RotationDeg - 90 * QuarterTurns;

    /// <summary>Output pixels per source pixel for an output of <paramref name="outputWidthPx"/>.</summary>
    public double ScaleFor(int outputWidthPx) => outputWidthPx / FrameWidthSource;

    /// <summary>
    /// Initial framing: the frame covers 80 % of the height of the photo as shown (after <paramref name="quarterTurns"/>),
    /// or its full width, centred.
    /// </summary>
    public static IdFraming Initial(double imageWidth, double imageHeight, IdSize size, int quarterTurns = 0)
    {
        var turns = Normalize(quarterTurns);
        var (shownW, shownH) = turns % 2 == 1 ? (imageHeight, imageWidth) : (imageWidth, imageHeight);
        var frameH = shownH * 0.8;
        var frameW = frameH * size.Ratio;
        if (frameW > shownW)
        {
            frameW = shownW;
        }

        return new IdFraming(imageWidth / 2, imageHeight / 2, frameW, 0, turns);
    }

    /// <summary>Quarter turns in 0..3.</summary>
    public static int Normalize(int quarterTurns) => ((quarterTurns % 4) + 4) % 4;

    /// <summary>
    /// Maps a point of the photo as shown after <paramref name="quarterTurns"/> clockwise turns back to the stored
    /// photo of <paramref name="width"/> × <paramref name="height"/>.
    /// </summary>
    public static (double X, double Y) Unturn(double x, double y, double width, double height, int quarterTurns) => Normalize(quarterTurns) switch
    {
        1 => (y, height - x),
        2 => (width - x, height - y),
        3 => (width - y, x),
        _ => (x, y),
    };

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
