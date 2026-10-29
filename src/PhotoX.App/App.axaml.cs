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
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var shell = new ShellViewModel(AppServices.Current);
            desktop.MainWindow = new MainWindow { DataContext = shell };
            shell.Initialize();
        }

        base.OnFrameworkInitializationCompleted();
    }
}
