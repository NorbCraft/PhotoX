using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PhotoX.App.Localization;
using PhotoX.App.Services;
using PhotoX.Storage.Updates;
using Serilog;

namespace PhotoX.App.ViewModels;

/// <summary>
/// What installing an update needs from the rest of PhotoX (prompt_08 §6): the update confirmation, permission to close
/// (nothing being saved, unsent print jobs and unsaved edits answered), starting the installer and closing, and opening
/// the release page in the browser (Linux).
/// </summary>
public sealed record UpdateHost(
    Func<ReleaseInfo, Task<bool>> ConfirmUpdate,
    Func<Task<bool>> PrepareClose,
    Func<string, bool> RunInstallerAndExit,
    Action<string> OpenPage);

/// <summary>
/// Infrequent options (FR-040): light or dark mode, JPEG quality, ID scale, updates, thumbnail cache, and About with the
/// third-party licences.
/// </summary>
public sealed partial class SettingsViewModel : ViewModelBase
{
    private readonly AppServices _services;
    private readonly UpdateHost? _host;
    private readonly Version _installed;
    private HttpClient? _http;
    private CancellationTokenSource? _download;

    public SettingsViewModel(AppServices services, string version, UpdateHost? host = null)
    {
        _services = services;
        _host = host;
        _installed = UpdateClient.ParseTag(version) ?? new Version(0, 0, 0);
        InstalledVersionText = Localizer.Instance.Format("InstalledVersion", version);
        JpegQuality = services.Settings.JpegQuality;
        IdScalePercent = (decimal)services.Settings.IdScalePercent;
        DataFilesText = Localizer.Instance.Format("DataFiles", services.Paths.ConfigDir);
        // The version comes from the running assembly (the build's version), never from a fixed text.
        AboutVersionText = Localizer.Instance.Format("VersionLabel", version);
        LogsText = Localizer.Instance.Format("Logs", services.Paths.LogDir);
        // Where PhotoX is installed and how to remove it (prompt_06 §8).
        InstallText = Localizer.Instance.Format("InstalledIn", AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar))
            + (OperatingSystem.IsWindows() ? " " + Localizer.Instance["UninstallHint"] : string.Empty);
        RefreshCache();
    }

    public event Action? Closed;

    [ObservableProperty]
    public partial decimal JpegQuality { get; set; }

    /// <summary>ID print scale correction (FR-071), percent.</summary>
    [ObservableProperty]
    public partial decimal IdScalePercent { get; set; }

    [ObservableProperty]
    public partial string CacheText { get; set; } = string.Empty;

    public string DataFilesText { get; }

    [ObservableProperty]
    public partial string? CacheMessage { get; set; }

    /// <summary>About: "Version 0.11.0", from the running build.</summary>
    public string AboutVersionText { get; }

    /// <summary>Settings shows the third-party licences instead of the options.</summary>
    [ObservableProperty]
    public partial bool ShowLicences { get; set; }

    /// <summary>The components and their licences, from THIRD-PARTY-NOTICES.md embedded in the app.</summary>
    public IReadOnlyList<ThirdPartyComponent> Components => _components ??= ThirdPartyNotices.Load();

    private IReadOnlyList<ThirdPartyComponent>? _components;

    [RelayCommand]
    private void ToggleLicences() => ShowLicences = !ShowLicences;

    /// <summary>A component's project page, in the browser.</summary>
    [RelayCommand]
    private void OpenSource(ThirdPartyComponent? component)
    {
        if (component is { HasSource: true }) _host?.OpenPage(component.Source);
    }

    public string LogsText { get; }

    public string InstallText { get; }

    public bool IsLight => _services.Settings.Theme != "dark";

    /// <summary>Light or dark mode (prompt_06 §3), applied at once and remembered.</summary>
    [RelayCommand]
    private void SetTheme(string theme)
    {
        _services.Settings.Theme = theme == "dark" ? "dark" : "light";
        _services.SaveSettings();
        App.ApplyTheme(_services.Settings.Theme);
        OnPropertyChanged(nameof(IsLight));
    }

    partial void OnJpegQualityChanged(decimal value)
    {
        _services.Settings.JpegQuality = (int)Math.Clamp(value, 80, 100);
        _services.SaveSettings();
    }

    partial void OnIdScalePercentChanged(decimal value)
    {
        _services.Settings.IdScalePercent = (double)Math.Clamp(value, 90, 110);
        _services.SaveSettings();
    }

    [RelayCommand]
    private async Task ClearCacheAsync()
    {
        await Task.Run(() => _services.ThumbnailCache.Clear());
        CacheMessage = Localizer.Instance["CacheCleared"];
        RefreshCache();
    }

    // ------------------------------------------------------------------ updates (prompt_08 §6)

    public string InstalledVersionText { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsUpdateIdle))]
    [NotifyCanExecuteChangedFor(nameof(CheckUpdatesCommand))]
    public partial bool IsChecking { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsUpdateIdle), nameof(CanInstall))]
    [NotifyCanExecuteChangedFor(nameof(CheckUpdatesCommand), nameof(InstallUpdateCommand))]
    public partial bool IsDownloading { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DownloadText))]
    public partial double DownloadProgress { get; set; }

    public string DownloadText => Localizer.Instance.Format("Downloading", Math.Round(DownloadProgress * 100));

    public bool IsUpdateIdle => !IsChecking && !IsDownloading;

    /// <summary>The answer of the last check (or of the download), in plain words.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasUpdateStatus))]
    public partial string? UpdateStatus { get; set; }

    public bool HasUpdateStatus => UpdateStatus is not null;

    [ObservableProperty]
    public partial bool UpdateStatusIsWarning { get; set; }

    /// <summary>A newer stable version published on GitHub.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasUpdate), nameof(CanInstall), nameof(CanOpenPage))]
    [NotifyCanExecuteChangedFor(nameof(InstallUpdateCommand))]
    public partial ReleaseInfo? AvailableRelease { get; set; }

    public bool HasUpdate => AvailableRelease is not null;

    /// <summary>Windows installs by itself; elsewhere the release page opens in the browser.</summary>
    public bool CanInstall => AvailableRelease?.Installer is not null && UpdatesInstall && !IsDownloading;

    public bool CanOpenPage => AvailableRelease is not null && !UpdatesInstall;

    /// <summary>The installer-based update applies to the Windows build only (prompt_08 §6); tests may switch it on.</summary>
    public bool UpdatesInstall { get; init; } = OperatingSystem.IsWindows();

    private HttpClient Http => _http ??= new HttpClient(_services.UpdateHandler()) { Timeout = TimeSpan.FromMinutes(15) }.WithUserAgent(_installed);

    /// <summary>Asks GitHub for the latest stable release and says whether it is newer than this PhotoX.</summary>
    [RelayCommand(CanExecute = nameof(IsUpdateIdle))]
    private async Task CheckUpdatesAsync()
    {
        IsChecking = true;
        AvailableRelease = null;
        UpdateStatus = Localizer.Instance["CheckingUpdates"];
        UpdateStatusIsWarning = false;
        try
        {
            var result = await new UpdateClient(Http).CheckAsync(_installed);
            Log.Information("Update check: {Status} {Version} {Detail}", result.Status, result.Release?.Version, result.Detail);
            var tr = Localizer.Instance;
            (UpdateStatus, UpdateStatusIsWarning) = result.Status switch
            {
                UpdateCheckStatus.UpToDate => (tr.Format("UpToDate", UpdateClient.Format(_installed)), false),
                UpdateCheckStatus.Available when result.Release!.Installer is null && UpdatesInstall =>
                    (tr.Format("UpdateAvailable", UpdateClient.Format(result.Release.Version), UpdateClient.Format(_installed)) + " " + tr["UpdateNoInstaller"], true),
                UpdateCheckStatus.Available => (tr.Format("UpdateAvailable", UpdateClient.Format(result.Release!.Version), UpdateClient.Format(_installed)), false),
                UpdateCheckStatus.NoRelease => (tr["UpdateNoRelease"], false),
                UpdateCheckStatus.Offline => (tr["UpdateOffline"], true),
                _ => (tr.Format("UpdateCheckFailed", result.Detail ?? string.Empty), true),
            };
            if (result.Status == UpdateCheckStatus.Available) AvailableRelease = result.Release;
        }
        finally
        {
            IsChecking = false;
        }
    }

    /// <summary>
    /// After the confirmation: downloads the installer with progress, verifies it against its published checksum, then
    /// (once nothing is being saved and unsent prints and unsaved edits are answered) starts it and closes PhotoX. The
    /// installer upgrades the installation; settings, print history and photos live elsewhere and stay.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanInstall))]
    private async Task InstallUpdateAsync()
    {
        if (AvailableRelease is not { } release || _host is null) return;
        if (!await _host.ConfirmUpdate(release)) return;
        var tr = Localizer.Instance;
        string installer;
        _download = new CancellationTokenSource();
        DownloadProgress = 0;
        IsDownloading = true;
        UpdateStatus = null;
        try
        {
            RemoveOldDownloads();
            installer = await new UpdateClient(Http).DownloadInstallerAsync(release, _services.UpdateDownloadDir,
                new Progress<double>(p => DownloadProgress = p), _download.Token);
            Log.Information("Update {Version} downloaded and verified: {Path}", release.Version, installer);
        }
        catch (OperationCanceledException)
        {
            (UpdateStatus, UpdateStatusIsWarning) = (tr["DownloadCancelled"], false);
            return;
        }
        catch (UpdateDownloadException ex)
        {
            Log.Warning("Update download failed: {Message}", ex.Message);
            (UpdateStatus, UpdateStatusIsWarning) = (ex.Integrity ? tr["UpdateVerifyFailed"] : tr.Format("UpdateFailed", ex.Message), true);
            return;
        }
        finally
        {
            IsDownloading = false;
            _download = null;
        }

        if (!await _host.PrepareClose())
        {
            (UpdateStatus, UpdateStatusIsWarning) = (tr["UpdateNotStarted"], false);
            return;
        }

        if (!_host.RunInstallerAndExit(installer)) (UpdateStatus, UpdateStatusIsWarning) = (tr["UpdateStartFailed"], true);
    }

    [RelayCommand]
    private void CancelDownload() => _download?.Cancel();

    /// <summary>Linux: the release page, where the Windows installer and the notes are.</summary>
    [RelayCommand]
    private void OpenReleasePage()
    {
        if (AvailableRelease is { } release) _host?.OpenPage(release.PageUrl);
    }

    /// <summary>Installers from earlier updates in PhotoX's own temp folder (each is large).</summary>
    private void RemoveOldDownloads()
    {
        try
        {
            if (!Directory.Exists(_services.UpdateDownloadDir)) return;
            foreach (var f in Directory.EnumerateFiles(_services.UpdateDownloadDir, "PhotoX-*-win-Setup.exe*")) File.Delete(f);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }
    }

    [RelayCommand]
    private void Close()
    {
        _download?.Cancel();
        _http?.Dispose();
        Closed?.Invoke();
    }

    private void RefreshCache() => CacheText = Localizer.Instance.Format("CacheInfo", _services.ThumbnailCache.SizeBytes() / 1048576.0,
        _services.Settings.ThumbnailCacheMaxMb, _services.Settings.ThumbnailCacheDays);
}
