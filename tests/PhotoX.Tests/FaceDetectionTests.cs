using ImageMagick;
using PhotoX.Core.Id;
using PhotoX.Imaging;
using PhotoX.Imaging.Faces;
using PhotoX.Imaging.Id;

namespace PhotoX.Tests;

/// <summary>TS-13: YuNet decoding, detection on the owner's sample (copy), several faces, no face.</summary>
public class FaceDetectionTests(ITestOutputHelper output)
{
    [Fact]
    public void Decoder_turns_one_strong_anchor_into_one_face()
    {
        var o = new Dictionary<string, float[]>();
        foreach (var s in new[] { 8, 16, 32 })
        {
            var n = (640 / s) * (640 / s);
            o[$"cls_{s}"] = new float[n];
            o[$"obj_{s}"] = new float[n];
            o[$"bbox_{s}"] = new float[n * 4];
            o[$"kps_{s}"] = new float[n * 10];
        }

        // Anchor at row 5, col 7 of stride 32: centre (7.5, 5.5)·32, size e^0·32 … with landmarks.
        var i = 5 * 20 + 7;
        o["cls_32"][i] = 0.9f;
        o["obj_32"][i] = 0.9f;
        o["bbox_32"][i * 4] = 0.5f;
        o["bbox_32"][i * 4 + 1] = 0.5f;
        o["bbox_32"][i * 4 + 2] = (float)Math.Log(3);
        o["bbox_32"][i * 4 + 3] = (float)Math.Log(4);
        for (var k = 0; k < 10; k++) o["kps_32"][i * 10 + k] = 0.25f;
        var faces = FaceDetector.Nms(FaceDetector.Decode(o, 640, 640, 0.6), 0.3);
        var f = Assert.Single(faces);
        Assert.Equal(0.9, f.Score, 3);
        Assert.Equal(7.5 * 32, f.Box.CenterX, 3);
        Assert.Equal(5.5 * 32, f.Box.CenterY, 3);
        Assert.Equal(96, f.Box.Width, 3);
        Assert.Equal((7.25 * 32, 5.25 * 32), f.Landmarks[0]);
    }

    [Fact]
    public void No_face_in_a_plain_image()
    {
        using var detector = new FaceDetector(FaceDetector.DefaultModelPath);
        using var img = Fixtures.Gradient(800, 600);
        Assert.Empty(detector.Detect(img));
    }

    [Fact]
    public void Sample_portrait_one_face_and_auto_align_puts_eyes_on_the_eye_line()
    {
        var sample = SampleTests.SamplePath();
        Assert.SkipWhen(sample is null, "Owner sample not available");
        using var dir = new TempDir();
        var copy = dir.File("s.jpg");
        File.Copy(sample!, copy);
        using var image = ImageLoader.Load(copy, 1800);
        using var detector = new FaceDetector(FaceDetector.DefaultModelPath);
        var faces = detector.Detect(image);
        var face = Assert.Single(faces);
        output.WriteLine($"score {face.Score:0.00} box {face.Box} eyes {face.LeftEye} {face.RightEye} head≈{face.HeadHeightEstimate:0}");
        Assert.True(face.Score > 0.8);
        Assert.True(face.LeftEye.X < face.RightEye.X);
        Assert.True(face.MouthCenter.Y > face.LeftEye.Y);

        var size = IdSizes.S35x45;
        var framing = IdFraming.FromFace(face.LeftEye.X, face.LeftEye.Y, face.RightEye.X, face.RightEye.Y, face.HeadHeightEstimate, size);
        using var photo = IdRenderer.Render(image, framing, size, new IdAdjustments());
        var f2 = Assert.Single(detector.Detect(photo));
        var eyeY = (f2.LeftEye.Y + f2.RightEye.Y) / 2 / photo.Height;
        output.WriteLine($"eye line at {eyeY:0.000} of height (target {size.EyeFraction:0.000}); roll {framing.RotationDeg:0.0}°");
        Assert.InRange(eyeY, size.EyeFraction - 0.02, size.EyeFraction + 0.02);
        Assert.InRange((f2.LeftEye.X + f2.RightEye.X) / 2 / photo.Width, 0.47, 0.53);
    }

    [Fact]
    public void Two_people_give_two_faces_left_to_right()
    {
        var sample = SampleTests.SamplePath();
        Assert.SkipWhen(sample is null, "Owner sample not available");
        using var dir = new TempDir();
        var copy = dir.File("s.jpg");
        File.Copy(sample!, copy);
        using var one = ImageLoader.Load(copy, 900);
        using var two = new MagickImage(MagickColors.White, one.Width * 2, one.Height);
        two.Composite(one, 0, 0);
        two.Composite(one, (int)one.Width, 0);
        using var detector = new FaceDetector(FaceDetector.DefaultModelPath);
        var faces = detector.Detect(two);
        Assert.Equal(2, faces.Count);
        Assert.True(faces[0].Box.X < faces[1].Box.X);
    }
}
