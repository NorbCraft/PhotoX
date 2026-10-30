using Avalonia.Media.Imaging;
using Avalonia.Threading;
using PhotoX.Imaging;
using PhotoX.Storage;
using Serilog;

namespace PhotoX.App.Services;

/// <summary>Gallery thumbnails (FR-053) for every workspace: from the disk cache or generated, decoded off the UI thread.</summary>
public static class ThumbnailSource
{
    /// <summary>Call from a worker thread. Returns the decoded bitmap and the pixel size, or a localized error.</summary>
    public static (Bitmap? Bitmap, string Info, string? Error) Load(ThumbnailCache cache, PhotoEntry entry)
    {
        var path = entry.Path;
        try
        {
            var info = ImageProbe.Probe(path);
            var dims = info.OrientedWidth > 0 ? $"{info.OrientedWidth} × {info.OrientedHeight}" : string.Empty;
            var bytes = cache.TryGet(entry);
            if (bytes is null)
            {
                bytes = Thumbnailer.Create(path);
                cache.Put(entry, bytes);
            }

            using var ms = new MemoryStream(bytes);
            return (new Bitmap(ms), dims, null);
        }
        catch (ImageLoadException ex)
        {
            Log.Warning("Thumbnail failed for {Path}: {Message}", path, ex.Message);
            return (null, string.Empty, ErrorTexts.ForImage(ex.Error));
        }
    }

    /// <summary>
    /// Loads thumbnails for <paramref name="items"/> with a few parallel workers (visible items first, in list order)
    /// and hands each result to <paramref name="apply"/> on the UI thread, unless cancelled.
    /// </summary>
    public static async Task LoadAllAsync<T>(
        ThumbnailCache cache,
        IReadOnlyList<T> items,
        Func<T, PhotoEntry?> entryOf,
        Action<T, Bitmap?, string, string?> apply,
        CancellationToken ct)
    {
        using var gate = new SemaphoreSlim(Math.Clamp(Environment.ProcessorCount - 1, 1, 4));
        var tasks = items.Select(async item =>
        {
            await gate.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                if (ct.IsCancellationRequested || entryOf(item) is not { } entry) return;
                var (bitmap, info, error) = await Task.Run(() => Load(cache, entry), ct).ConfigureAwait(false);
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    if (!ct.IsCancellationRequested) apply(item, bitmap, info, error);
                });
            }
            catch (OperationCanceledException)
            {
            }
            finally
            {
                gate.Release();
            }
        });
        try
        {
            await Task.WhenAll(tasks).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
        }
    }
}
