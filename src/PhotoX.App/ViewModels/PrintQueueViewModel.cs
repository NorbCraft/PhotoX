using System.Collections.ObjectModel;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PhotoX.App.Localization;
using PhotoX.Printing;

namespace PhotoX.App.ViewModels;

/// <summary>A job as shown in the print queue.</summary>
public sealed partial class PrintJobItem(PrintJob job) : ViewModelBase
{
    public PrintJob Job { get; } = job;

    public string Name => Job.Name;

    public string Details => Localizer.Instance.Format("JobDetails", PrintDialogViewModel.SizeName(Job.Size), Job.Copies, Job.Printer);

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

    /// <summary>ok, warn, error or neutral: the colour of the status.</summary>
    public string Tone => Job.State switch
    {
        PrintJobState.Finished => "ok",
        PrintJobState.Attention => "warn",
        PrintJobState.Failed => "error",
        _ => "neutral",
    };

    public bool CanCancel => Job.CanCancel;

    public void Refresh()
    {
        OnPropertyChanged(nameof(StatusText));
        OnPropertyChanged(nameof(Tone));
        OnPropertyChanged(nameof(CanCancel));
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
/// The print queue (prompt_05 §4): PhotoX's jobs of this session with their state in the Windows queue. "Done"
/// means Windows finished the job, not that a photo came out of the printer. Failed jobs are not resubmitted.
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
            foreach (var j in Jobs) j.Refresh();
        };
    }

    /// <summary>Newest first.</summary>
    public ObservableCollection<PrintJobItem> Jobs { get; } = [];

    [ObservableProperty]
    public partial bool IsOpen { get; set; }

    public int ActiveCount => Jobs.Count(j => j.Job.IsActive);

    public bool HasActive => ActiveCount > 0;

    public bool HasJobs => Jobs.Count > 0;

    public bool NeedsAttention => Jobs.Any(j => j.Job.State is PrintJobState.Attention or PrintJobState.Failed);

    [RelayCommand]
    private void Toggle() => IsOpen = !IsOpen;

    [RelayCommand]
    private void Close() => IsOpen = false;

    [RelayCommand]
    private void Cancel(PrintJobItem item) => _ = Task.Run(() => _service.Cancel(item.Job));

    [RelayCommand]
    private void ClearFinished()
    {
        _service.ClearFinished();
        foreach (var j in Jobs.Where(j => !j.Job.IsActive).ToList()) Jobs.Remove(j);
        Raise();
    }

    private void Update(PrintJob job)
    {
        var item = Jobs.FirstOrDefault(j => j.Job == job);
        if (item is null) Jobs.Insert(0, new PrintJobItem(job));
        else item.Refresh();
        Raise();
    }

    private void Raise()
    {
        OnPropertyChanged(nameof(ActiveCount));
        OnPropertyChanged(nameof(HasActive));
        OnPropertyChanged(nameof(HasJobs));
        OnPropertyChanged(nameof(NeedsAttention));
    }
}
