using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PhotoX.App.Localization;
using PhotoX.App.Services;

namespace PhotoX.App.ViewModels;

/// <summary>Infrequent options (FR-040): JPEG quality, thumbnail cache, about.</summary>
public sealed partial class SettingsViewModel : ViewModelBase
{
    private readonly AppServices _services;

    public SettingsViewModel(AppServices services, string version)
    {
        _services = services;
        JpegQuality = services.Settings.JpegQuality;
        AboutText = Localizer.Instance.Format("AboutText", version);
        LogsText = Localizer.Instance.Format("Logs", services.Paths.LogDir);
        RefreshCache();
    }

    public event Action? Closed;

    [ObservableProperty]
    public partial decimal JpegQuality { get; set; }

    [ObservableProperty]
    public partial string CacheText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string? CacheMessage { get; set; }

    public string AboutText { get; }

    public string LogsText { get; }

    partial void OnJpegQualityChanged(decimal value)
    {
        _services.Settings.JpegQuality = (int)Math.Clamp(value, 80, 100);
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

    private void RefreshCache() => CacheText = Localizer.Instance.Format("CacheInfo", _services.ThumbnailCache.SizeBytes() / 1048576.0);
}
