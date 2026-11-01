using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Styling;
using PhotoX.App.Services;
using PhotoX.App.ViewModels;
using PhotoX.App.Views;
using PhotoX.Storage;

namespace PhotoX.App;

public sealed class App : Application
{
    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    /// <summary>Light or dark mode for the whole application (prompt_06 §3).</summary>
    public static void ApplyTheme(string theme)
    {
        if (Current is { } app) app.RequestedThemeVariant = theme == "dark" ? ThemeVariant.Dark : ThemeVariant.Light;
    }

    public override void OnFrameworkInitializationCompleted()
    {
        AppServices.Current ??= new AppServices(new AppPaths());
        ApplyTheme(AppServices.Current.Settings.Theme);
        var cache = AppServices.Current.ThumbnailCache;
        _ = Task.Run(() =>
        {
            try
            {
                cache.Prune();
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                Serilog.Log.Warning(ex, "Thumbnail cache pruning failed");
            }
        });
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var shell = new ShellViewModel(AppServices.Current);
            desktop.MainWindow = new MainWindow { DataContext = shell };
            shell.Initialize();
        }

        base.OnFrameworkInitializationCompleted();
    }
}
