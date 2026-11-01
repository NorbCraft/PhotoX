using System.Globalization;
using System.Text.Json;

namespace PhotoX.Storage;

/// <summary>What became of a print job, as far as Windows reports it (prompt_06 §7).</summary>
public enum PrintOutcome
{
    /// <summary>Accepted by Windows and still in its queue when last seen.</summary>
    Submitted,

    /// <summary>Left the Windows queue normally. Not a confirmation that paper came out of the printer.</summary>
    Finished,

    Canceled,

    /// <summary>Removed from the Windows queue before it finished (outside PhotoX).</summary>
    Removed,

    /// <summary>Never accepted by Windows (PhotoX could not prepare it, or Windows refused it).</summary>
    Failed,
}

/// <summary>
/// One print job in the history. <paramref name="TimeUtc"/> is when the job was created; <paramref name="Size"/> is
/// the print size id (10x15, 15x20); <paramref name="Path"/> is the gallery photo, none for ID sheets and collages.
/// </summary>
public sealed record PrintLogEntry(
    string JobId,
    DateTime TimeUtc,
    string Name,
    string Size,
    int Copies,
    string Printer,
    PrintOutcome Outcome,
    string? Path = null,
    string? Detail = null)
{
    /// <summary>
    /// Counted as printed: Windows accepted the job and it was not cancelled or removed. The same rule as the print
    /// counters of the gallery (prompt_05 §6).
    /// </summary>
    public bool Counts => Outcome is PrintOutcome.Submitted or PrintOutcome.Finished;
}

/// <summary>
/// Copies counted as printed in a period, by print size, and the jobs that did not print. <paramref name="FirstUtc"/> is
/// the time of the earliest job of the period (shown as "since …" for all time, prompt_07 §3.2).
/// </summary>
public sealed record PrintTotals(int Prints, IReadOnlyDictionary<string, int> PrintsBySize, int Jobs, int CanceledJobs, int FailedJobs, DateTime? FirstUtc = null)
{
    public static PrintTotals Of(IEnumerable<PrintLogEntry> entries)
    {
        var list = entries.ToList();
        var counted = list.Where(e => e.Counts).ToList();
        return new PrintTotals(
            counted.Sum(e => e.Copies),
            counted.GroupBy(e => e.Size).ToDictionary(g => g.Key, g => g.Sum(e => e.Copies)),
            list.Count,
            list.Count(e => e.Outcome is PrintOutcome.Canceled or PrintOutcome.Removed),
            list.Count(e => e.Outcome == PrintOutcome.Failed),
            list.Count > 0 ? list.Min(e => e.TimeUtc) : null);
    }

    public int PrintsOf(string size) => PrintsBySize.TryGetValue(size, out var n) ? n : 0;
}

/// <summary>
/// The print history (prompt_06 §7): every print job sent through PhotoX with its outcome, kept in the user's data
/// folder (never in a customer's folder) as one JSON-lines file per month (<c>yyyy-MM.jsonl</c>). Records are only
/// appended; a later record of the same job replaces the earlier one when read, so a crash can at worst lose the
/// last line. Periods are in local time; weeks start on Monday.
/// </summary>
public sealed class PrintLog
{
    private static readonly JsonSerializerOptions LineOptions = new(JsonFile.Options) { WriteIndented = false };
    private readonly object _gate = new();

    public PrintLog(string directory) => Directory = directory;

    public string Directory { get; }

    /// <summary>Raised after a record was written.</summary>
    public event Action<PrintLogEntry>? Changed;

    /// <summary>Appends the job's current state. Failures to write are ignored: printing never stops for the history.</summary>
    public void Record(PrintLogEntry entry)
    {
        try
        {
            var line = JsonSerializer.Serialize(entry, LineOptions) + "\n";
            lock (_gate)
            {
                System.IO.Directory.CreateDirectory(Directory);
                using var fs = new FileStream(FileFor(entry.TimeUtc.ToLocalTime()), FileMode.Append, FileAccess.Write, FileShare.Read);
                var bytes = System.Text.Encoding.UTF8.GetBytes(line);
                fs.Write(bytes);
                fs.Flush(true);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return;
        }

        Changed?.Invoke(entry);
    }

    /// <summary>The jobs created in [<paramref name="fromLocal"/>, <paramref name="toLocal"/>), newest first.</summary>
    public IReadOnlyList<PrintLogEntry> Read(DateTime fromLocal, DateTime toLocal)
    {
        var months = new List<string>();
        for (var m = new DateTime(fromLocal.Year, fromLocal.Month, 1); m < toLocal; m = m.AddMonths(1)) months.Add(FileFor(m));
        return Latest(months).Where(e => e.TimeUtc.ToLocalTime() >= fromLocal && e.TimeUtc.ToLocalTime() < toLocal).OrderByDescending(e => e.TimeUtc).ToList();
    }

    /// <summary>Every job ever recorded.</summary>
    public IReadOnlyList<PrintLogEntry> ReadAll()
    {
        string[] files;
        try
        {
            files = System.IO.Directory.Exists(Directory) ? System.IO.Directory.GetFiles(Directory, "*.jsonl") : [];
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            files = [];
        }

        return Latest(files.Order(StringComparer.Ordinal)).OrderByDescending(e => e.TimeUtc).ToList();
    }

    /// <summary>Totals for today, this week, this month and all time, as of <paramref name="nowLocal"/>.</summary>
    public (PrintTotals Today, PrintTotals Week, PrintTotals Month, PrintTotals All) Statistics(DateTime nowLocal)
    {
        var today = nowLocal.Date;
        var week = WeekStart(nowLocal);
        var month = new DateTime(nowLocal.Year, nowLocal.Month, 1);
        var all = ReadAll();
        var end = today.AddDays(1);
        IEnumerable<PrintLogEntry> Between(DateTime from) => all.Where(e => e.TimeUtc.ToLocalTime() >= from && e.TimeUtc.ToLocalTime() < end);
        return (PrintTotals.Of(Between(today)), PrintTotals.Of(Between(week)), PrintTotals.Of(Between(month)), PrintTotals.Of(all));
    }

    /// <summary>Monday of the week of <paramref name="local"/> (weeks start on Monday in Romania).</summary>
    public static DateTime WeekStart(DateTime local) => local.Date.AddDays(-(((int)local.DayOfWeek + 6) % 7));

    private string FileFor(DateTime local) => Path.Combine(Directory, local.ToString("yyyy-MM", CultureInfo.InvariantCulture) + ".jsonl");

    private List<PrintLogEntry> Latest(IEnumerable<string> files)
    {
        var jobs = new Dictionary<string, PrintLogEntry>(StringComparer.Ordinal);
        lock (_gate)
        {
            foreach (var file in files)
            {
                IEnumerable<string> lines;
                try
                {
                    if (!File.Exists(file)) continue;
                    lines = File.ReadAllLines(file);
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    continue;
                }

                foreach (var line in lines)
                {
                    if (string.IsNullOrWhiteSpace(line)) continue;
                    try
                    {
                        if (JsonSerializer.Deserialize<PrintLogEntry>(line, LineOptions) is { JobId.Length: > 0 } e) jobs[e.JobId] = e;
                    }
                    catch (JsonException)
                    {
                        // A line cut short by a crash or power loss: skip it.
                    }
                }
            }
        }

        return jobs.Values.ToList();
    }
}
