using ImageMagick;
using PhotoX.Core.Formats;
using PhotoX.Core.Printing;
using PhotoX.Imaging;
using PhotoX.Imaging.Printing;
using PhotoX.Storage;

namespace PhotoX.Printing;

/// <summary>A print order: what, on which printer and paper, at which print size, with the driver settings to use.</summary>
public sealed record PrintRequest(PrintItem Item, string Printer, PrinterPaper Paper, PrintFormat Size, byte[]? Settings = null, string? OutputFile = null);

public enum PrintJobState
{
    /// <summary>In PhotoX's queue, not yet prepared.</summary>
    Waiting,

    /// <summary>PhotoX renders the page.</summary>
    Preparing,

    /// <summary>The page is being handed to Windows.</summary>
    Sending,

    /// <summary>Accepted by Windows and in its queue (spooling or printing).</summary>
    InQueue,

    /// <summary>In the Windows queue, but Windows reports a problem (paper out, offline, error, paused).</summary>
    Attention,

    /// <summary>Left the Windows queue normally. This is not a confirmation that paper came out of the printer.</summary>
    Finished,

    /// <summary>Removed from the Windows queue before it finished (cancelled outside PhotoX).</summary>
    Removed,

    Canceled,

    /// <summary>PhotoX could not prepare the page or Windows refused the job; nothing was printed.</summary>
    Failed,
}

/// <summary>One job in the print queue (prompt_05 §4). Changed only by <see cref="PrintService"/>.</summary>
public sealed class PrintJob
{
    internal PrintJob(int id, PrintRequest request)
    {
        Id = id;
        Request = request;
        CreatedUtc = DateTime.UtcNow;
    }

    public int Id { get; }

    public PrintRequest Request { get; }

    public string Name => Request.Item.Name;

    public string Printer => Request.Printer;

    public PrintFormat Size => Request.Size;

    public int Copies => Request.Item.Copies;

    public DateTime CreatedUtc { get; }

    public PrintJobState State { get; internal set; }

    /// <summary>The printing system's message (failure reason, problem reported by Windows).</summary>
    public string? Detail { get; internal set; }

    public int? WindowsJobId { get; internal set; }

    public bool IsActive => State is PrintJobState.Waiting or PrintJobState.Preparing or PrintJobState.Sending or PrintJobState.InQueue or PrintJobState.Attention;

    public bool CanCancel => State is PrintJobState.Waiting or PrintJobState.Preparing or PrintJobState.InQueue or PrintJobState.Attention;

    internal bool CancelRequested { get; set; }

    internal bool Counted { get; set; }

    internal bool SawDeleting { get; set; }
}

/// <summary>
/// The print queue (prompt_05 §§2, 4, 6): prepares one job at a time off the UI thread, hands it to the printing system,
/// then follows it in the Windows queue. Print counters change only when Windows has accepted a job, and are taken
/// back when the job is cancelled through PhotoX or seen removed before finishing. Jobs are never resubmitted
/// automatically, so an uncertain job cannot print twice.
/// </summary>
public sealed class PrintService : IDisposable
{
    private readonly object _gate = new();
    private readonly List<PrintJob> _jobs = [];
    private readonly SemaphoreSlim _signal = new(0);
    private readonly CancellationTokenSource _stop = new();
    private readonly TimeSpan _poll;
    private readonly Task? _worker;
    private int _nextId;

    /// <param name="startWorker">False in unit tests, which call <see cref="ProcessPending"/> and <see cref="Poll"/> themselves.</param>
    public PrintService(IPrintBackend backend, PrintHistory history, TimeSpan? pollInterval = null, bool startWorker = true)
    {
        Backend = backend;
        History = history;
        _poll = pollInterval ?? TimeSpan.FromSeconds(2);
        if (startWorker) _worker = Task.Run(RunAsync);
    }

    /// <summary>Raised (on a worker thread) whenever a job is added or changes state.</summary>
    public event Action<PrintJob>? JobChanged;

    public IPrintBackend Backend { get; }

    public PrintHistory History { get; }

    public IReadOnlyList<PrintJob> Jobs
    {
        get
        {
            lock (_gate) return _jobs.ToList();
        }
    }

    public IReadOnlyList<PrintJob> Enqueue(IEnumerable<PrintRequest> requests)
    {
        var added = new List<PrintJob>();
        lock (_gate)
        {
            foreach (var r in requests.Where(r => r.Item.Copies > 0))
            {
                var job = new PrintJob(++_nextId, r);
                _jobs.Add(job);
                added.Add(job);
            }
        }

        foreach (var job in added) JobChanged?.Invoke(job);
        _signal.Release();
        return added;
    }

    /// <summary>Cancels a job: before it reaches Windows it is dropped; in the Windows queue it is deleted there.</summary>
    public void Cancel(PrintJob job)
    {
        bool deleteInWindows;
        lock (_gate)
        {
            switch (job.State)
            {
                case PrintJobState.Waiting:
                    job.State = PrintJobState.Canceled;
                    deleteInWindows = false;
                    break;
                case PrintJobState.Preparing or PrintJobState.Sending:
                    job.CancelRequested = true;
                    return;
                case PrintJobState.InQueue or PrintJobState.Attention:
                    deleteInWindows = true;
                    break;
                default:
                    return;
            }
        }

        if (deleteInWindows) DeleteInWindows(job);
        JobChanged?.Invoke(job);
    }

    /// <summary>Removes finished, cancelled and failed jobs from the list.</summary>
    public void ClearFinished()
    {
        lock (_gate) _jobs.RemoveAll(j => !j.IsActive);
    }

    /// <summary>Prepares and submits every waiting job, in order.</summary>
    internal void ProcessPending()
    {
        while (true)
        {
            PrintJob? job;
            lock (_gate)
            {
                job = _jobs.FirstOrDefault(j => j.State == PrintJobState.Waiting);
                if (job is not null) job.State = PrintJobState.Preparing;
            }

            if (job is null) return;
            JobChanged?.Invoke(job);
            Process(job);
        }
    }

    /// <summary>Follows submitted jobs in the Windows queue.</summary>
    internal void Poll()
    {
        foreach (var job in Jobs.Where(j => j.State is PrintJobState.InQueue or PrintJobState.Attention && j.WindowsJobId is not null))
        {
            var s = Backend.Query(job.Printer, job.WindowsJobId!.Value);
            var before = (job.State, job.Detail);
            lock (_gate)
            {
                switch (s.Status)
                {
                    case SpoolStatus.Queued or SpoolStatus.Printing or SpoolStatus.Printed:
                        (job.State, job.Detail) = (PrintJobState.InQueue, s.Status == SpoolStatus.Queued ? null : s.Status.ToString());
                        break;
                    case SpoolStatus.Attention:
                        (job.State, job.Detail) = (PrintJobState.Attention, s.Detail);
                        break;
                    case SpoolStatus.Deleting:
                        job.SawDeleting = true;
                        break;
                    case SpoolStatus.Gone when job.SawDeleting:
                        job.State = PrintJobState.Removed;
                        Uncount(job);
                        break;
                    case SpoolStatus.Gone:
                        (job.State, job.Detail) = (PrintJobState.Finished, null);
                        break;
                }
            }

            if (before != (job.State, job.Detail)) JobChanged?.Invoke(job);
        }
    }

    private void Process(PrintJob job)
    {
        var r = job.Request;
        try
        {
            var g = Backend.GetGeometry(r.Printer, r.Paper, r.Settings);
            var (pageW, pageH, x, y, pageWmm, pageHmm) = PageFor(g, r);
            var (content, cropToFill) = PrintContent.Load(r.Item, preview: false);
            PageBitmap bitmap;
            using (content)
            {
                var placement = PrintLayout.Place(content.Width, content.Height, pageWmm, pageHmm, cropToFill);
                using var page = PrintRenderer.RenderPage(content, placement, pageW, pageH, pageWmm, pageHmm);
                bitmap = PrintRenderer.ToBitmap(page);
            }

            lock (_gate)
            {
                job.State = job.CancelRequested ? PrintJobState.Canceled : PrintJobState.Sending;
            }

            JobChanged?.Invoke(job);
            if (job.State == PrintJobState.Canceled) return;

            var id = Backend.Submit(new PrintSubmission(r.Printer, r.Paper, r.Settings, $"PhotoX – {r.Item.Name}", bitmap, x, y, r.Item.Copies, r.OutputFile));
            bool cancelNow;
            lock (_gate)
            {
                job.WindowsJobId = id;
                job.State = PrintJobState.InQueue;
                if (r.Item.CounterPath is { } path)
                {
                    History.Add(path, r.Item.Copies); // only now: Windows has accepted the job
                    job.Counted = true;
                }

                cancelNow = job.CancelRequested;
            }

            if (cancelNow) DeleteInWindows(job);
        }
        catch (Exception ex) when (ex is PrintException or ImageLoadException or MagickException or IOException or UnauthorizedAccessException)
        {
            lock (_gate) (job.State, job.Detail) = (PrintJobState.Failed, ex.Message);
        }

        JobChanged?.Invoke(job);
    }

    /// <summary>
    /// The page in device pixels and where it goes. On the paper of the print size the page covers the driver's whole
    /// page (its borderless area); on another paper (a PDF printer's A4) the print size is centred at true size.
    /// </summary>
    internal static (int W, int H, int X, int Y, double Wmm, double Hmm) PageFor(PageGeometry g, PrintRequest r)
    {
        if (PrintLayout.PaperMatches(r.Paper.WidthMm, r.Paper.HeightMm, r.Size.WidthMm, r.Size.HeightMm))
        {
            return (g.PhysicalWidthPx, g.PhysicalHeightPx, -g.OffsetXPx, -g.OffsetYPx, g.PhysicalWidthPx * 25.4 / g.DpiX, g.PhysicalHeightPx * 25.4 / g.DpiY);
        }

        var portrait = g.PhysicalWidthPx <= g.PhysicalHeightPx;
        var (wMm, hMm) = portrait ? (r.Size.WidthMm, r.Size.HeightMm) : (r.Size.HeightMm, r.Size.WidthMm);
        var w = (int)Math.Round(wMm * g.DpiX / 25.4);
        var h = (int)Math.Round(hMm * g.DpiY / 25.4);
        if (w > g.PhysicalWidthPx + 1 || h > g.PhysicalHeightPx + 1) throw new PrintException($"The paper \"{r.Paper.Name}\" is smaller than {r.Size.Name} cm.");
        return (w, h, (g.PhysicalWidthPx - w) / 2 - g.OffsetXPx, (g.PhysicalHeightPx - h) / 2 - g.OffsetYPx, wMm, hMm);
    }

    private void DeleteInWindows(PrintJob job)
    {
        if (job.WindowsJobId is not { } id) return;
        var ok = Backend.Cancel(job.Printer, id);
        lock (_gate)
        {
            if (ok)
            {
                job.State = PrintJobState.Canceled;
                job.Detail = null;
                Uncount(job);
            }
            else
            {
                job.Detail = "cancel-failed";
            }
        }
    }

    private void Uncount(PrintJob job)
    {
        if (!job.Counted || job.Request.Item.CounterPath is not { } path) return;
        History.Add(path, -job.Copies);
        job.Counted = false;
    }

    private async Task RunAsync()
    {
        while (!_stop.IsCancellationRequested)
        {
            try
            {
                await _signal.WaitAsync(_poll, _stop.Token).ConfigureAwait(false);
                ProcessPending();
                Poll();
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception ex) when (ex is PrintException or IOException)
            {
                // A queue query failed (printer removed, spooler restarted): try again at the next interval.
            }
        }
    }

    public void Dispose()
    {
        _stop.Cancel();
        try
        {
            _worker?.Wait(TimeSpan.FromSeconds(2));
        }
        catch (AggregateException)
        {
        }

        _stop.Dispose();
        _signal.Dispose();
    }
}
