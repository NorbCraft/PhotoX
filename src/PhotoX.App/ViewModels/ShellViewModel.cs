using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PhotoX.App.Localization;
using PhotoX.App.Services;

namespace PhotoX.App.ViewModels;

/// <summary>Main window: workspaces (Photos, ID Photos, Collages, Printing), language switch and page navigation (FR-008, FR-009).</summary>
public sealed partial class ShellViewModel : ViewModelBase
{
    private readonly AppServices _services;

    public ShellViewModel(AppServices services)
    {
        _services = services;
        Browse = new BrowseViewModel(services);
        Editor = new EditorViewModel(services, Browse);
        Id = new IdViewModel(services) { StartFolder = () => Browse.CurrentFolder };
        Collage = new CollageViewModel(services) { StartFolder = () => Browse.CurrentFolder };
        Print = new PrintViewModel(services);
        Print.BackRequested += BackFromPrinting;
        Browse.EditRequested += item => _ = OpenEditorAsync(item);
        Browse.PrintRequested += PrintPhotos;
        Browse.DiscardOriginalsRequested += () => _ = DiscardOriginalsAsync();
        Browse.RestoreRequested += items => _ = RestoreOriginalAsync(items);
        Browse.DeleteRequested += items => _ = DeletePhotosAsync(items);
        Browse.RestoreDeletedRequested += items => _ = RestoreDeletedAsync(items);
        Browse.ConfirmLeaveFolder = ConfirmLeaveFolderAsync;
        Id.PrintRequested += (item, size) => OpenPrint([item], size, sizeFixed: true);
        Collage.PrintRequested += (item, size) => OpenPrint([item], size, sizeFixed: true);
        Print.Queue.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(PrintQueueViewModel.ActiveCount)) OnPropertyChanged(nameof(PrintBadge));
        };
        Browse.UseForIdRequested += item => _ = UseForIdAsync(item);
        Browse.DuplicateFailed += message =>
        {
            var d = new DialogViewModel(Localizer.Instance["DuplicateFailedTitle"], Localizer.Instance.Format("DuplicateFailed", message));
            d.Buttons.Add(new DialogButton("ok", Localizer.Instance["Ok"], true));
            _ = ShowDialogAsync(d);
        };
        Editor.BackRequested += () => CurrentPage = Browse;
        Editor.PrintRequested += item => PrintPhotos([item]);
        Editor.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(EditorViewModel.Title) or nameof(EditorViewModel.IsDirty)) RaiseContext();
        };
        Browse.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(BrowseViewModel.CurrentFolder)) RaiseContext();
        };
        CurrentPage = Browse;
    }

    public BrowseViewModel Browse { get; }

    [ObservableProperty]
    public partial DialogViewModel? Dialog { get; set; }

    [ObservableProperty]
    public partial SettingsViewModel? Settings { get; set; }

    /// <summary>The Printing workspace (prompt_06 §7): what to print, the queue, the history and the statistics.</summary>
    public PrintViewModel Print { get; }

    /// <summary>Jobs in progress, shown on the Printing tab.</summary>
    public string PrintBadge => Print.Queue.ActiveCount.ToString(Localizer.Instance.Culture);

    public EditorViewModel Editor { get; }

    public IdViewModel Id { get; }

    public CollageViewModel Collage { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsPhotosWorkspace), nameof(IsIdWorkspace), nameof(IsCollageWorkspace), nameof(IsPrintWorkspace), nameof(IsEditing), nameof(IsBrowsing),
        nameof(CurrentExplorer), nameof(CanToggleTree), nameof(IsTreeVisible))]
    [NotifyCanExecuteChangedFor(nameof(ToggleTreeCommand))]
    public partial ViewModelBase CurrentPage { get; set; }

    /// <summary>The explorer of the page shown, whose folder tree the toolbar button shows or hides (prompt_08 §4).</summary>
    public ExplorerViewModel? CurrentExplorer => CurrentPage switch
    {
        BrowseViewModel => Browse.Explorer,
        IdViewModel => Id.Picker.Explorer,
        CollageViewModel => Collage.Picker.Explorer,
        _ => null,
    };

    public bool CanToggleTree => CurrentExplorer is not null;

    /// <summary>The button is marked while the shown workspace's tree is visible.</summary>
    public bool IsTreeVisible => CurrentExplorer?.IsTreeVisible == true;

    /// <summary>Shows or hides the folder tree of the workspace shown; each workspace keeps its own choice (prompt_08 §4).</summary>
    [RelayCommand(CanExecute = nameof(CanToggleTree))]
    private void ToggleTree()
    {
        CurrentExplorer?.ToggleTree();
        OnPropertyChanged(nameof(IsTreeVisible));
    }

    public bool IsPhotosWorkspace => CurrentPage is BrowseViewModel or EditorViewModel;

    public bool IsIdWorkspace => CurrentPage is IdViewModel;

    public bool IsCollageWorkspace => CurrentPage is CollageViewModel;

    public bool IsPrintWorkspace => CurrentPage is PrintViewModel;

    public bool IsEditing => CurrentPage is EditorViewModel;

    public bool IsBrowsing => CurrentPage is BrowseViewModel;

    public bool IsEnglish => Localizer.Instance.Language == "en";

    public bool IsRomanian => Localizer.Instance.Language == "ro";

    public string ContextText => CurrentPage switch
    {
        EditorViewModel e => e.Title,
        BrowseViewModel b => b.FolderTitle,
        _ => string.Empty,
    };

    public bool ShowUnsaved => CurrentPage is EditorViewModel { IsDirty: true };

    public string Version => typeof(ShellViewModel).Assembly.GetName().Version is { } v ? $"{v.Major}.{v.Minor}.{v.Build}" : "0.0.0";

    /// <summary>The native title bar shows the version (prompt_04 §1).</summary>
    public string WindowTitle => $"PhotoX {Version}";

    public void Initialize()
    {
        Localizer.Instance.SetLanguage(_services.Settings.Language);
        Browse.Initialize();
        RaiseLanguage();
    }

    [RelayCommand]
    private void SetLanguage(string language)
    {
        Localizer.Instance.SetLanguage(language);
        _services.Settings.Language = language;
        _services.SaveSettings();
        RaiseLanguage();
    }

    /// <summary>Back to Photos: the editor if it was open when the operator left, otherwise the gallery.</summary>
    [RelayCommand]
    private void ShowPhotos()
    {
        if (IsPhotosWorkspace) return;
        CurrentPage = Editor.Current is not null && _lastPhotosPage is EditorViewModel ? Editor : Browse;
    }

    [RelayCommand]
    private void ShowId()
    {
        RememberPhotosPage();
        CurrentPage = Id;
        Id.Activate();
    }

    [RelayCommand]
    private void ShowCollages()
    {
        RememberPhotosPage();
        CurrentPage = Collage;
        Collage.Activate();
    }

    /// <summary>
    /// Printing (prompt_07 §3.7, §3.8): remembers the workspace it was opened from, so Back and Escape return there with
    /// its state (the selection, or the photo open in the editor).
    /// </summary>
    [RelayCommand]
    private void ShowPrinting()
    {
        if (IsPrintWorkspace) return;
        RememberPhotosPage();
        _printOrigin = IsPhotosWorkspace ? Browse : CurrentPage;
        Print.OriginName = _printOrigin switch
        {
            IdViewModel => Localizer.Instance["TabId"],
            CollageViewModel => Localizer.Instance["TabCollages"],
            _ => Localizer.Instance["TabPhotos"],
        };
        CurrentPage = Print;
        Print.Activate();
    }

    /// <summary>Back from Printing to the workspace that sent the print.</summary>
    private void BackFromPrinting()
    {
        switch (_printOrigin)
        {
            case IdViewModel: ShowId(); break;
            case CollageViewModel: ShowCollages(); break;
            default: ShowPhotos(); break;
        }
    }

    private ViewModelBase? _printOrigin;

    private void RememberPhotosPage()
    {
        if (!IsPhotosWorkspace) return;
        if (CurrentPage is EditorViewModel editor) editor.KeepPending();
        _lastPhotosPage = CurrentPage;
    }

    private ViewModelBase? _lastPhotosPage;

    [RelayCommand]
    private void OpenSettings() => Settings = CreateSettings(OperatingSystem.IsWindows());

    /// <param name="updatesInstall">Windows installs updates itself; elsewhere the release page opens (tests choose).</param>
    internal SettingsViewModel CreateSettings(bool updatesInstall)
    {
        var vm = new SettingsViewModel(_services, Version, new UpdateHost(ConfirmUpdateAsync, PrepareUpdateCloseAsync, RunInstallerAndExit, url => OpenUrlRequested?.Invoke(url)))
        {
            UpdatesInstall = updatesInstall,
        };
        vm.Closed += () => Settings = null;
        return vm;
    }

    /// <summary>Raised to close PhotoX without asking again (the update installer has started).</summary>
    public event Action? ExitRequested;

    /// <summary>Raised to open a web page in the browser (the release page on Linux).</summary>
    public event Action<string>? OpenUrlRequested;

    /// <summary>Starts the downloaded installer; tests replace it.</summary>
    public Func<string, bool> StartInstaller { get; set; } = path =>
    {
        try
        {
            // Through the shell, so Windows applies its usual checks (SmartScreen) to the unsigned installer.
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(path) { UseShellExecute = true });
            return true;
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException or IOException)
        {
            Serilog.Log.Warning(ex, "Could not start the installer {Path}", path);
            return false;
        }
    };

    private async Task<bool> ConfirmUpdateAsync(Storage.Updates.ReleaseInfo release)
    {
        var tr = Localizer.Instance;
        var version = Storage.Updates.UpdateClient.Format(release.Version);
        var size = release.Installer is { Size: > 0 } i ? tr.Format("UpdateSize", i.Size / 1048576.0) : string.Empty;
        var d = new DialogViewModel(tr.Format("UpdateConfirmTitle", version), tr.Format("UpdateConfirmMessage", version, Version, size))
        {
            Note = tr["UpdateSmartScreen"],
        };
        d.Buttons.Add(new DialogButton("cancel", tr["Cancel"], false));
        d.Buttons.Add(new DialogButton("update", tr["UpdateInstall"], true));
        return await ShowDialogAsync(d) == "update";
    }

    /// <summary>Before the installer starts: nothing may be saving; unsent print jobs and unsaved edits are asked about.</summary>
    private async Task<bool> PrepareUpdateCloseAsync()
    {
        if (Editor.IsBusy || Id.IsBusy || Collage.IsBusy)
        {
            await ShowMessageAsync(Localizer.Instance["Updates"], Localizer.Instance["UpdateBusy"]);
            return false;
        }

        return await ConfirmCloseAsync("UpdateAnyway");
    }

    private bool RunInstallerAndExit(string installer)
    {
        if (!StartInstaller(installer)) return false;
        Serilog.Log.Information("Update installer started: {Path}; closing", installer);
        ExitRequested?.Invoke();
        return true;
    }

    private async Task UseForIdAsync(PhotoItemViewModel item)
    {
        ShowId();
        await Id.LoadSourceAsync(item.Path);
    }

    private async Task OpenEditorAsync(PhotoItemViewModel item)
    {
        CurrentPage = Editor;
        await Editor.OpenAsync(item);
    }

    partial void OnCurrentPageChanged(ViewModelBase value) => RaiseContext();

    private void RaiseContext()
    {
        OnPropertyChanged(nameof(ContextText));
        OnPropertyChanged(nameof(ShowUnsaved));
    }

    private void RaiseLanguage()
    {
        OnPropertyChanged(nameof(IsEnglish));
        OnPropertyChanged(nameof(IsRomanian));
    }
}
