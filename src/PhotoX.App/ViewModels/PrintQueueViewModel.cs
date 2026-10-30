using System.Collections.ObjectModel;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.Input;
using PhotoX.App.Localization;
using PhotoX.Printing;

namespace PhotoX.App.ViewModels;

/// <summary>A job as shown in the print queue.</summary>
public sealed partial class PrintJobItem(PrintJob job) : ViewModelBase
{
    public PrintJob Job { get; } = job;

    public string Name => Job.Name;

    public string Details => Localizer.Instance.Format("JobDetails", PrintViewModel.SizeName(Job.Size), Job.Copies, Job.Printer);

    public string StatusText => Job.State switch
    {
        PrintJobState.Waiting => Localizer.Instance["JobWaiting"],
        PrintJobState.Preparing => Localizer.Instance["JobPreparing"],
        PrintJobState.Sending => Localizer.Instance["JobSending"],
        PrintJobState.InQueue when Job.Detail == "cancel-failed" => Localizer.Instance["JobCancelFailed"],
        PrintJobState.InQueue when Job.Detail is not null => Localizer.Instance["JobPrinting"],
        PrintJobState.InQueue => Localizer.Instance["JobInQueue"],
        PrintJobState.Attention => Localizer.Instance.Format("JobAttention", AttentionText(Job.Detail)),
        PrintJobState.Finished => Localizer.Instance["JobFinished"],
        PrintJobState.Removed => Localizer.Instance["JobRemoved"],
        PrintJobState.Canceled => Localizer.Instance["JobCanceled"],
        _ => Localizer.Instance.Format("JobFailed", Job.Detail ?? string.Empty),
    };

    public bool IsOk => Job.State == PrintJobState.Finished;

    public bool IsWarn => Job.State == PrintJobState.Attention || Job.Detail == "cancel-failed";

    public bool IsError => Job.State == PrintJobState.Failed;

    public bool CanCancel => Job.CanCancel;

    /// <summary>The job is being prepared, sent or printed: shown with an indeterminate progress bar.</summary>
    public bool IsBusy => Job.State is PrintJobState.Preparing or PrintJobState.Sending || Job is { State: PrintJobState.InQueue, Detail: not null and not "cancel-failed" };

    public void Refresh()
    {
        OnPropertyChanged(nameof(StatusText));
        OnPropertyChanged(nameof(IsOk));
        OnPropertyChanged(nameof(IsWarn));
        OnPropertyChanged(nameof(IsError));
        OnPropertyChanged(nameof(CanCancel));
        OnPropertyChanged(nameof(IsBusy));
        OnPropertyChanged(nameof(Details));
    }

    private static string AttentionText(string? detail) => detail switch
    {
        "paper-out" => Localizer.Instance["AttentionPaperOut"],
        "offline" => Localizer.Instance["AttentionOffline"],
        "paused" => Localizer.Instance["AttentionPaused"],
        "intervention" or "error" or null => Localizer.Instance["AttentionError"],
        _ => detail,
    };
}

/// <summary>
/// The print queue (prompt_05 §4, prompt_06 §7): PhotoX's jobs of this session that are still in progress, with their
/// state in the Windows queue. Finished, cancelled and failed jobs move to the history. "Done" means Windows finished
/// the job, not that a photo came out of the printer. Failed jobs are not resubmitted.
/// </summary>
public sealed partial class PrintQueueViewModel : ViewModelBase
{
    private readonly PrintService _service;

    public PrintQueueViewModel(PrintService service)
    {
        _service = service;
        var ui = Dispatcher.UIThread; // jobs change on the print worker; updates are posted to this (UI) thread
        service.JobChanged += job => ui.Post(() => Update(job));
        Localizer.Instance.LanguageChanged += (_, _) =>
        {
            foreach (var j in Active) j.Refresh();
        };
    }

    /// <summary>Jobs in progress, newest first.</summary>
    public ObservableCollection<PrintJobItem> Active { get; } = [];

    /// <summary>Raised on the UI thread when a job leaves the queue (it then shows in the history).</summary>
    public event Action? JobEnded;

    public int ActiveCount => Active.Count;

    public bool HasActive => Active.Count > 0;

    /// <summary>Jobs not yet accepted by Windows: they would be lost if PhotoX closed now.</summary>
    public int UnsentCount => Active.Count(j => j.Job.State is PrintJobState.Waiting or PrintJobState.Preparing or PrintJobState.Sending);

    public bool NeedsAttention => Active.Any(j => j.Job.State == PrintJobState.Attention || j.Job.Detail == "cancel-failed");

    /// <summary>History ids of the jobs in progress, which the history leaves out while the queue shows them.</summary>
    public IReadOnlySet<string> ActiveIds => Active.Select(j => j.Job.HistoryId).ToHashSet();

    [RelayCommand]
    private void Cancel(PrintJobItem item) => _ = Task.Run(() => _service.Cancel(item.Job));

    private void Update(PrintJob job)
    {
        var item = Active.FirstOrDefault(j => j.Job == job);
        if (job.IsActive)
        {
            if (item is null) Active.Insert(0, new PrintJobItem(job));
            else item.Refresh();
        }
        else if (item is not null)
        {
            Active.Remove(item);
            JobEnded?.Invoke();
        }
        else
        {
            JobEnded?.Invoke();
        }

        OnPropertyChanged(nameof(ActiveCount));
        OnPropertyChanged(nameof(HasActive));
        OnPropertyChanged(nameof(NeedsAttention));
    }
}
