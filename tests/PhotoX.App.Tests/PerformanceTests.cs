using System.Diagnostics;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using ImageMagick;
using PhotoX.Tests;

namespace PhotoX.App.Tests;

/// <summary>
/// TS-11 / TS-12 measurements (opt-in: set PHOTOX_PERF=1). Prints timings and memory for a large synthetic order;
/// numbers are recorded in IMPLEMENTATION_LOG together with the machine they were measured on.
/// </summary>
public class PerformanceTests(ITestOutputHelper output)
{
    [AvaloniaFact]
    public async Task Large_order_timings()
    {
        Assert.SkipUnless(Environment.GetEnvironmentVariable("PHOTOX_PERF") == "1", "Set PHOTOX_PERF=1 to run measurements");
        var count = int.TryParse(Environment.GetEnvironmentVariable("PHOTOX_PERF_COUNT"), out var c) ? c : 300;
        using var dir = new TempDir();
        var order = Path.Combine(dir.Path, "order");
        Directory.CreateDirectory(order);
        var sw = Stopwatch.StartNew();
        using (var template = new MagickImage("plasma:", new MagickReadSettings { Width = 4000, Height = 3000 }))
        {
            template.Quality = 90;
            var bytes = template.ToByteArray(MagickFormat.Jpeg);
            for (var i = 0; i < count; i++)
            {
                // Distinct files (different bytes) with identical content cost: append a comment marker.
                File.WriteAllBytes(Path.Combine(order, $"IMG_{i:0000}.jpg"), bytes);
                File.SetLastWriteTimeUtc(Path.Combine(order, $"IMG_{i:0000}.jpg"), DateTime.UtcNow.AddSeconds(-i));
            }

            output.WriteLine($"Fixture: {count} × 4000×3000 JPEG ({bytes.Length / 1024} KB each), {sw.ElapsedMilliseconds} ms");
        }

        var (window, shell) = UiHelpers.CreateWindow(Path.Combine(dir.Path, "cfg"), 1920, 1080);
        sw.Restart();
        await shell.Browse.OpenFolderAsync(order);
        output.WriteLine($"Open folder (scan + items): {sw.ElapsedMilliseconds} ms");
        await UiHelpers.WaitUntil(() => shell.Browse.AllPhotos.Take(20).All(p => p.Thumbnail is not null), 120000);
        output.WriteLine($"First 20 thumbnails (cold): {sw.ElapsedMilliseconds} ms");
        await UiHelpers.WaitUntil(() => shell.Browse.AllPhotos.All(p => p.Thumbnail is not null), 600000);
        output.WriteLine($"All {count} thumbnails (cold): {sw.ElapsedMilliseconds} ms");

        sw.Restart();
        await shell.Browse.OpenFolderAsync(order);
        await UiHelpers.WaitUntil(() => shell.Browse.AllPhotos.All(p => p.Thumbnail is not null), 600000);
        output.WriteLine($"All {count} thumbnails (warm cache): {sw.ElapsedMilliseconds} ms");

        sw.Restart();
        shell.Browse.StartEditingCommand.Execute(null);
        await UiHelpers.WaitUntil(() => shell.Editor is { Preview: not null, IsLoading: false }, 60000);
        output.WriteLine($"Open first photo in editor: {sw.ElapsedMilliseconds} ms");

        var cycles = Math.Min(20, count - 1);
        var times = new List<long>();
        for (var i = 0; i < cycles; i++)
        {
            shell.Editor.Tone.ExposurePlusCommand.Execute(null);
            var before = shell.Editor.Current!.Name;
            sw.Restart();
            await shell.Editor.SaveAndNextAsync();
            await UiHelpers.WaitUntil(() => shell.Editor.Current!.Name != before && !shell.Editor.IsBusy && shell.Editor.Preview is not null, 60000);
            times.Add(sw.ElapsedMilliseconds);
        }

        times.Sort();
        output.WriteLine($"Save & Next (12 MP, save + open next): median {times[times.Count / 2]} ms, max {times[^1]} ms over {cycles}");
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        var proc = Process.GetCurrentProcess();
        proc.Refresh();
        output.WriteLine($"Working set after run: {proc.WorkingSet64 / 1048576} MB, managed heap {GC.GetTotalMemory(false) / 1048576} MB, peak {proc.PeakWorkingSet64 / 1048576} MB");
        output.WriteLine($"Machine: {Environment.ProcessorCount} logical CPUs, {GC.GetGCMemoryInfo().TotalAvailableMemoryBytes / 1073741824.0:0.0} GB available, {Environment.OSVersion}");
        window.Close();
    }

    /// <summary>
    /// Interactive latency (prompt_03 §9): time from a control change to the new preview being ready, as the median of
    /// repeated changes on a 12 MP photo. Rendering the frame on screen is not included (headless).
    /// </summary>
    [AvaloniaFact]
    public async Task Interactive_latency()
    {
        Assert.SkipUnless(Environment.GetEnvironmentVariable("PHOTOX_PERF") == "1", "Set PHOTOX_PERF=1 to run measurements");
        using var dir = new TempDir();
        var order = Path.Combine(dir.Path, "order");
        Directory.CreateDirectory(order);
        using (var template = new MagickImage("plasma:", new MagickReadSettings { Width = 4000, Height = 3000 }))
        {
            template.Quality = 90;
            for (var i = 0; i < 6; i++) template.Write(Path.Combine(order, $"IMG_{i}.jpg"), MagickFormat.Jpeg);
        }

        var (window, shell) = UiHelpers.CreateWindow(Path.Combine(dir.Path, "cfg"), 1920, 1080);
        await shell.Browse.OpenFolderAsync(order);
        var sw = Stopwatch.StartNew();
        shell.Browse.StartEditingCommand.Execute(null);
        await Poll(() => shell.Editor is { Preview: not null, IsLoading: false });
        output.WriteLine($"Open photo in editor: {sw.ElapsedMilliseconds} ms");
        var editor = shell.Editor;

        async Task<string> Measure(Action<int> change, Func<bool>? ready = null)
        {
            var times = new List<double>();
            for (var i = 0; i < 15; i++)
            {
                var before = editor.Preview;
                var t = Stopwatch.StartNew();
                change(i);
                await Poll(ready ?? (() => editor.Preview != before && !editor.IsBusy));
                times.Add(t.Elapsed.TotalMilliseconds);
            }

            times.Sort();
            return $"median {times[times.Count / 2]:0} ms, max {times[^1]:0} ms";
        }

        output.WriteLine($"Exposure slider step: {await Measure(i => editor.Tone.Exposure = (i % 2 == 0 ? 0.5 : -0.5) + i * 0.01)}");
        output.WriteLine($"Contrast slider step: {await Measure(i => editor.Tone.Contrast = (i % 2 == 0 ? 20 : -20) + i)}");
        output.WriteLine($"Saturation slider step: {await Measure(i => editor.Tone.Saturation = (i % 2 == 0 ? 20 : -20) + i)}");
        // Straightening is drawn by the canvas since 0.5.0: measure the change plus one rendered frame.
        var canvasForAngle = window.GetVisualDescendants().OfType<PhotoX.App.Controls.PhotoCanvas>().First();
        var angles = new List<double>();
        for (var i = 0; i < 15; i++)
        {
            var t = Stopwatch.StartNew();
            editor.Straighten = (i % 2 == 0 ? 3 : -3) + i * 0.1;
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();
            Avalonia.Headless.AvaloniaHeadlessPlatform.ForceRenderTimerTick();
            angles.Add(t.Elapsed.TotalMilliseconds);
        }

        angles.Sort();
        output.WriteLine($"Straighten slider step (change + headless frame): median {angles[angles.Count / 2]:0} ms, max {angles[^1]:0} ms, angle drawn {canvasForAngle.Angle:0.0}°");
        editor.Straighten = 0;
        var hist = new List<double>();
        for (var i = 0; i < 15; i++)
        {
            var t = Stopwatch.StartNew();
            editor.CommitCrop(new Avalonia.Rect(10 + i, 10, 600, 400));
            hist.Add(t.Elapsed.TotalMilliseconds);
        }

        hist.Sort();
        output.WriteLine($"Crop commit + histogram (UI thread): median {hist[hist.Count / 2]:0.0} ms");

        // One crop-drag frame: move the frame and render the window (software Skia, 1920×1080).
        var canvas = window.GetVisualDescendants().OfType<PhotoX.App.Controls.PhotoCanvas>().First();
        var frames = new List<double>();
        for (var i = 0; i < 30; i++)
        {
            var t = Stopwatch.StartNew();
            canvas.CropRect = new Avalonia.Rect(20 + i * 3, 20, 600, 400);
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();
            Avalonia.Headless.AvaloniaHeadlessPlatform.ForceRenderTimerTick();
            frames.Add(t.Elapsed.TotalMilliseconds);
        }

        frames.Sort();
        output.WriteLine($"Crop drag frame (layout + headless render): median {frames[frames.Count / 2]:0.0} ms, max {frames[^1]:0.0} ms");

        var moves = new List<double>();
        for (var i = 0; i < 4; i++)
        {
            var name = editor.Current!.Name;
            var t = Stopwatch.StartNew();
            await editor.NextAsync();
            await Poll(() => editor.Current!.Name != name && !editor.IsBusy && editor.Preview is not null);
            moves.Add(t.Elapsed.TotalMilliseconds);
            await Task.Delay(1500); // let the next photo preload, as during normal work
        }

        moves.Sort();
        output.WriteLine($"Next photo (preloaded): median {moves[moves.Count / 2]:0} ms");

        await shell.Id.LoadSourceAsync(Path.Combine(order, "IMG_0.jpg"));
        await Poll(() => shell.Id.Preview is not null && !shell.Id.IsBusy);
        var id = shell.Id;
        async Task<string> MeasureId(Action<int> change)
        {
            var times = new List<double>();
            for (var i = 0; i < 15; i++)
            {
                var before = id.Preview;
                var t = Stopwatch.StartNew();
                change(i);
                await Poll(() => id.Preview != before);
                times.Add(t.Elapsed.TotalMilliseconds);
            }

            times.Sort();
            return $"median {times[times.Count / 2]:0} ms, max {times[^1]:0} ms";
        }

        output.WriteLine($"ID rotation step: {await MeasureId(i => id.Rotation = (i % 2 == 0 ? 2 : -2) + i * 0.1)}");
        output.WriteLine($"ID exposure step: {await MeasureId(i => id.Tone.Exposure = (i % 2 == 0 ? 0.5 : -0.5) + i * 0.01)}");

        // Collages (prompt_04 §9): moving a photo in its slot is a redraw of the preview bitmaps.
        shell.ShowCollagesCommand.Execute(null);
        var collage = shell.Collage;
        foreach (var path in Directory.GetFiles(order).Order().Take(4)) await collage.PlaceAsync(path);
        var pans = new List<double>();
        for (var i = 0; i < 15; i++)
        {
            var t = Stopwatch.StartNew();
            collage.Zoom(0, i == 0 ? 2 : 1);
            collage.Pan(0, i % 2 == 0 ? 0.05 : -0.04, 0.02);
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();
            Avalonia.Headless.AvaloniaHeadlessPlatform.ForceRenderTimerTick();
            pans.Add(t.Elapsed.TotalMilliseconds);
        }

        pans.Sort();
        output.WriteLine($"Collage pan step (change + headless frame): median {pans[pans.Count / 2]:0.0} ms, max {pans[^1]:0.0} ms");
        output.WriteLine($"Machine: {Environment.ProcessorCount} logical CPUs, {Environment.OSVersion}");
        window.Close();
    }

    /// <summary>Spins the UI dispatcher until the condition holds, so measured latency is not padded by polling sleeps.</summary>
    private static async Task Poll(Func<bool> condition, int timeoutMs = 60000)
    {
        var start = Environment.TickCount64;
        while (!condition())
        {
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();
            if (Environment.TickCount64 - start > timeoutMs) throw new TimeoutException("Condition not reached");
            await Task.Yield();
        }
    }
}
