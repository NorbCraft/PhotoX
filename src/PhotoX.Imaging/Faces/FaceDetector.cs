using ImageMagick;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using PhotoX.Core.Geometry;

namespace PhotoX.Imaging.Faces;

/// <summary>A detected face: box, five landmarks (two eyes, nose, two mouth corners) and score, in image pixels.</summary>
public sealed record DetectedFace(RectD Box, IReadOnlyList<(double X, double Y)> Landmarks, double Score)
{
    /// <summary>The eye that appears on the left of the image.</summary>
    public (double X, double Y) LeftEye => Landmarks[0].X <= Landmarks[1].X ? Landmarks[0] : Landmarks[1];

    public (double X, double Y) RightEye => Landmarks[0].X <= Landmarks[1].X ? Landmarks[1] : Landmarks[0];

    public (double X, double Y) MouthCenter => ((Landmarks[3].X + Landmarks[4].X) / 2, (Landmarks[3].Y + Landmarks[4].Y) / 2);

    /// <summary>
    /// Estimated crown-to-chin height. No permissive model gives crown and chin, so this uses the eye-to-mouth
    /// distance with typical proportions (≈ 3.4×). The operator always checks and corrects the guides.
    /// </summary>
    public double HeadHeightEstimate
    {
        get
        {
            var ex = (LeftEye.X + RightEye.X) / 2;
            var ey = (LeftEye.Y + RightEye.Y) / 2;
            var d = Math.Sqrt(Math.Pow(MouthCenter.X - ex, 2) + Math.Pow(MouthCenter.Y - ey, 2));
            return d * HeadToEyeMouthRatio;
        }
    }

    public const double HeadToEyeMouthRatio = 3.4;
}

/// <summary>
/// Offline face detection with OpenCV Zoo's YuNet (MIT) on ONNX Runtime (MIT), used only by Auto Align (FR-063,
/// DEC-038). The model has a fixed 640×640 input; images are scaled to fit and padded at the right and bottom.
/// </summary>
public sealed class FaceDetector : IDisposable
{
    public const string ModelFile = "face_detection_yunet_2023mar.onnx";
    private static readonly int[] Strides = [8, 16, 32];
    private readonly InferenceSession _session;
    private readonly string _input;
    private readonly int _w;
    private readonly int _h;

    public FaceDetector(string modelPath)
    {
        var options = new SessionOptions { GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_ALL, IntraOpNumThreads = Math.Clamp(Environment.ProcessorCount / 2, 1, 4) };
        _session = new InferenceSession(modelPath, options);
        var input = _session.InputMetadata.First();
        _input = input.Key;
        _h = input.Value.Dimensions[2];
        _w = input.Value.Dimensions[3];
    }

    public static string DefaultModelPath => Path.Combine(AppContext.BaseDirectory, "models", ModelFile);

    public IReadOnlyList<DetectedFace> Detect(MagickImage image, double scoreThreshold = 0.6, double nmsThreshold = 0.3)
    {
        var k = Math.Min((double)_w / image.Width, (double)_h / image.Height);
        var sw = Math.Max(1, (int)Math.Round(image.Width * k));
        var sh = Math.Max(1, (int)Math.Round(image.Height * k));
        using var small = (MagickImage)image.Clone();
        small.Resize(new MagickGeometry((uint)sw, (uint)sh) { IgnoreAspectRatio = true });
        small.Alpha(AlphaOption.Off);
        byte[] bgr;
        using (var pixels = small.GetPixelsUnsafe())
        {
            bgr = pixels.ToByteArray(PixelMapping.BGR) ?? [];
        }

        var tensor = new DenseTensor<float>([1, 3, _h, _w]);
        for (var y = 0; y < sh; y++)
        {
            for (var x = 0; x < sw; x++)
            {
                var i = (y * sw + x) * 3;
                tensor[0, 0, y, x] = bgr[i];
                tensor[0, 1, y, x] = bgr[i + 1];
                tensor[0, 2, y, x] = bgr[i + 2];
            }
        }

        using var results = _session.Run([NamedOnnxValue.CreateFromTensor(_input, tensor)]);
        var outputs = results.ToDictionary(r => r.Name, r => r.AsTensor<float>().ToArray());
        var raw = Decode(outputs, _w, _h, scoreThreshold);
        return Nms(raw, nmsThreshold)
            .Select(f => new DetectedFace(
                new RectD(f.Box.X / k, f.Box.Y / k, f.Box.Width / k, f.Box.Height / k),
                f.Landmarks.Select(p => (p.X / k, p.Y / k)).ToList(),
                f.Score))
            .OrderBy(f => f.Box.X)
            .ToList();
    }

    /// <summary>YuNet output decoding (as in OpenCV's FaceDetectorYN): score = √(cls·obj), box and landmarks per anchor.</summary>
    internal static List<DetectedFace> Decode(IReadOnlyDictionary<string, float[]> o, int width, int height, double threshold)
    {
        var faces = new List<DetectedFace>();
        foreach (var s in Strides)
        {
            int cols = width / s, rows = height / s;
            var cls = o[$"cls_{s}"];
            var obj = o[$"obj_{s}"];
            var bbox = o[$"bbox_{s}"];
            var kps = o[$"kps_{s}"];
            for (var r = 0; r < rows; r++)
            {
                for (var c = 0; c < cols; c++)
                {
                    var i = r * cols + c;
                    var score = Math.Sqrt(Math.Clamp(cls[i], 0, 1) * Math.Clamp(obj[i], 0, 1));
                    if (score < threshold) continue;
                    var cx = (c + bbox[i * 4]) * s;
                    var cy = (r + bbox[i * 4 + 1]) * s;
                    var w = Math.Exp(bbox[i * 4 + 2]) * s;
                    var h = Math.Exp(bbox[i * 4 + 3]) * s;
                    var lm = new List<(double, double)>(5);
                    for (var n = 0; n < 5; n++) lm.Add(((kps[i * 10 + 2 * n] + c) * s, (kps[i * 10 + 2 * n + 1] + r) * s));
                    faces.Add(new DetectedFace(new RectD(cx - w / 2, cy - h / 2, w, h), lm, score));
                }
            }
        }

        return faces;
    }

    internal static List<DetectedFace> Nms(List<DetectedFace> faces, double iouThreshold)
    {
        var keep = new List<DetectedFace>();
        foreach (var f in faces.OrderByDescending(f => f.Score))
        {
            if (keep.All(k => IoU(k.Box, f.Box) < iouThreshold)) keep.Add(f);
        }

        return keep;
    }

    private static double IoU(RectD a, RectD b)
    {
        var x0 = Math.Max(a.X, b.X);
        var y0 = Math.Max(a.Y, b.Y);
        var x1 = Math.Min(a.Right, b.Right);
        var y1 = Math.Min(a.Bottom, b.Bottom);
        var inter = Math.Max(0, x1 - x0) * Math.Max(0, y1 - y0);
        return inter / (a.Width * a.Height + b.Width * b.Height - inter);
    }

    public void Dispose() => _session.Dispose();
}
