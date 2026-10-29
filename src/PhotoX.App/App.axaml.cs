using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using PhotoX.App.Services;
using PhotoX.App.ViewModels;
using PhotoX.App.Views;
using PhotoX.Storage;

namespace PhotoX.App;

public sealed class App : Application
{
    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        AppServices.Current ??= new AppServices(new AppPaths());
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
