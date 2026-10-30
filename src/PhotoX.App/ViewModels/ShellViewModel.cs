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
        Browse.SaveChangesRequested += items => _ = SaveChangesAsync(items);
        Browse.RestoreRequested += items => _ = RestoreOriginalAsync(items);
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
    [NotifyPropertyChangedFor(nameof(IsPhotosWorkspace), nameof(IsIdWorkspace), nameof(IsCollageWorkspace), nameof(IsPrintWorkspace), nameof(IsEditing), nameof(IsBrowsing))]
    public partial ViewModelBase CurrentPage { get; set; }

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

    [RelayCommand]
    private void ShowPrinting()
    {
        if (IsPrintWorkspace) return;
        RememberPhotosPage();
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
    private void OpenSettings()
    {
        var vm = new SettingsViewModel(_services, Version);
        vm.Closed += () => Settings = null;
        Settings = vm;
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
