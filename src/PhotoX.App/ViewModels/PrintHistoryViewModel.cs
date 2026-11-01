using System.Collections.ObjectModel;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PhotoX.App.Localization;
using PhotoX.Core.Formats;
using PhotoX.Storage;
using Serilog;

namespace PhotoX.App.ViewModels;

public enum HistoryPeriod
{
    Today,
    Week,
    Month,
}

/// <summary>A job in the print history.</summary>
public sealed class PrintHistoryItem(PrintLogEntry entry, bool today)
{
    public PrintLogEntry Entry { get; } = entry;

    public string Time => Entry.TimeUtc.ToLocalTime().ToString(today ? "HH:mm" : "dd.MM HH:mm", Localizer.Instance.Culture);

    public string Name => Entry.Name;

    public string Details => Localizer.Instance.Format("JobDetails", SizeName(Entry.Size), Entry.Copies, Entry.Printer);

    public string OutcomeText => Entry.Outcome switch
    {
        PrintOutcome.Submitted => Localizer.Instance["HistSubmitted"],
        PrintOutcome.Finished => Localizer.Instance["JobFinished"],
        PrintOutcome.Canceled => Localizer.Instance["JobCanceled"],
        PrintOutcome.Removed => Localizer.Instance["JobRemoved"],
        _ => Localizer.Instance.Format("JobFailed", Entry.Detail ?? string.Empty),
    };

    public bool IsOk => Entry.Counts;

    public bool IsError => Entry.Outcome == PrintOutcome.Failed;

    public bool IsMuted => Entry.Outcome is PrintOutcome.Canceled or PrintOutcome.Removed;

    private static string SizeName(string id) => PrintFormats.FindPrintSize(id) is { } f ? PrintViewModel.SizeName(f) : id;
}

/// <summary>A statistics tile: prints counted in a period and the split by print size.</summary>
public sealed record PrintStatTile(string Title, string Prints, string Split);

/// <summary>
/// Print history and statistics (prompt_06 §7), read from the <see cref="PrintLog"/>: every job of today, this week or
/// this month with its outcome, kept across sessions. Prints are counted when Windows accepted the job and it was not
/// cancelled or removed, the same rule as the counters in the gallery; cancelled and failed jobs are listed but not
/// counted. Nothing here claims that paper came out of the printer.
/// </summary>
public sealed partial class PrintHistoryViewModel : ViewModelBase
{
    private readonly PrintLog _log;
    private readonly PrintQueueViewModel _queue;
    private readonly Dispatcher _ui;
    private int _version;

    public PrintHistoryViewModel(PrintLog log, PrintQueueViewModel queue)
    {
        _log = log;
        _queue = queue;
        _ui = Dispatcher.UIThread;
        log.Changed += _ => _ui.Post(Refresh);
        queue.JobEnded += Refresh;
        Localizer.Instance.LanguageChanged += (_, _) => Refresh();
    }

    public ObservableCollection<PrintHistoryItem> Entries { get; } = [];

    public ObservableCollection<PrintStatTile> Tiles { get; } = [];

    [ObservableProperty]
    public partial HistoryPeriod Period { get; set; }

    public bool HasEntries => Entries.Count > 0;

    partial void OnPeriodChanged(HistoryPeriod value) => Refresh();

    [RelayCommand]
    private void SetPeriod(HistoryPeriod period) => Period = period;

    /// <summary>Reads the history in the background; the newest request wins.</summary>
    public void Refresh() => _ = RefreshAsync();

    public async Task RefreshAsync()
    {
        var version = ++_version;
        var now = DateTime.Now;
        var from = Period switch
        {
            HistoryPeriod.Week => PrintLog.WeekStart(now),
            HistoryPeriod.Month => new DateTime(now.Year, now.Month, 1),
            _ => now.Date,
        };
        var active = _queue.ActiveIds;
        try
        {
            var (entries, stats) = await Task.Run(() => (_log.Read(from, now.Date.AddDays(1)), _log.Statistics(now)));
            if (version != _version) return;
            Entries.Clear();
            foreach (var e in entries.Where(e => !active.Contains(e.JobId))) Entries.Add(new PrintHistoryItem(e, Period == HistoryPeriod.Today));
            Tiles.Clear();
            Tiles.Add(Tile(Localizer.Instance["StatToday"], stats.Today));
            Tiles.Add(Tile(Localizer.Instance["StatWeek"], stats.Week));
            Tiles.Add(Tile(Localizer.Instance["StatMonth"], stats.Month));
            Tiles.Add(stats.All.FirstUtc is { } first
                ? Tile(Localizer.Instance.Format("StatAllSince", first.ToLocalTime().ToString("dd.MM.yyyy", System.Globalization.CultureInfo.InvariantCulture)), stats.All)
                : Tile(Localizer.Instance["StatAll"], stats.All));
            OnPropertyChanged(nameof(HasEntries));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Log.Warning(ex, "Print history");
        }
    }

    private static PrintStatTile Tile(string title, PrintTotals t) => new(
        title,
        t.Prints.ToString(Localizer.Instance.Culture),
        Localizer.Instance.Format("StatSplit", t.PrintsOf(PrintFormats.P10x15.Id), t.PrintsOf(PrintFormats.P15x20.Id)));
}
