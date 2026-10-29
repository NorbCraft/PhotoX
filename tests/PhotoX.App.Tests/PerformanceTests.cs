using System.Diagnostics;
using Avalonia.Headless.XUnit;
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
        shell.Browse.OpenFolder(order);
        output.WriteLine($"Open folder (scan + items): {sw.ElapsedMilliseconds} ms");
        await UiHelpers.WaitUntil(() => shell.Browse.AllPhotos.Take(20).All(p => p.Thumbnail is not null), 120000);
        output.WriteLine($"First 20 thumbnails (cold): {sw.ElapsedMilliseconds} ms");
        await UiHelpers.WaitUntil(() => shell.Browse.AllPhotos.All(p => p.Thumbnail is not null), 600000);
        output.WriteLine($"All {count} thumbnails (cold): {sw.ElapsedMilliseconds} ms");

        sw.Restart();
        shell.Browse.OpenFolder(order);
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
            shell.Editor.ExposurePlusCommand.Execute(null);
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
}
