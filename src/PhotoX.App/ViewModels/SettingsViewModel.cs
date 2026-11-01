using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PhotoX.App.Localization;
using PhotoX.App.Services;

namespace PhotoX.App.ViewModels;

/// <summary>Infrequent options (FR-040): light or dark mode, JPEG quality, ID scale, thumbnail cache, about.</summary>
public sealed partial class SettingsViewModel : ViewModelBase
{
    private readonly AppServices _services;

    public SettingsViewModel(AppServices services, string version)
    {
        _services = services;
        JpegQuality = services.Settings.JpegQuality;
        IdScalePercent = (decimal)services.Settings.IdScalePercent;
        DataFilesText = Localizer.Instance.Format("DataFiles", services.Paths.ConfigDir);
        AboutText = Localizer.Instance.Format("AboutText", version);
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

    public string AboutText { get; }

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

    [RelayCommand]
    private void Close() => Closed?.Invoke();

    private void RefreshCache() => CacheText = Localizer.Instance.Format("CacheInfo", _services.ThumbnailCache.SizeBytes() / 1048576.0,
        _services.Settings.ThumbnailCacheMaxMb, _services.Settings.ThumbnailCacheDays);
}
