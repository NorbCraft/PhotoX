using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PhotoX.App.Localization;
using PhotoX.App.Services;

namespace PhotoX.App.ViewModels;

/// <summary>Main window: workspaces, language switch and page navigation (FR-008, FR-009).</summary>
public sealed partial class ShellViewModel : ViewModelBase
{
    private readonly AppServices _services;

    public ShellViewModel(AppServices services)
    {
        _services = services;
        Browse = new BrowseViewModel(services);
        Editor = new EditorViewModel(services, Browse);
        Id = new IdViewModel();
        Browse.EditRequested += item => _ = OpenEditorAsync(item);
        Browse.FinishRequested += () => _ = FinishOrderAsync();
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

    public EditorViewModel Editor { get; }

    public IdViewModel Id { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsPhotosWorkspace), nameof(IsIdWorkspace), nameof(IsEditing))]
    public partial ViewModelBase CurrentPage { get; set; }

    public bool IsPhotosWorkspace => CurrentPage is BrowseViewModel or EditorViewModel;

    public bool IsIdWorkspace => CurrentPage is IdViewModel;

    public bool IsEditing => CurrentPage is EditorViewModel;

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

    [RelayCommand]
    private void ShowPhotos() => CurrentPage = Editor.Current is not null && CurrentPage is IdViewModel && _lastPhotosPage is EditorViewModel ? Editor : Browse;

    [RelayCommand]
    private void ShowId()
    {
        _lastPhotosPage = CurrentPage;
        CurrentPage = Id;
    }

    private ViewModelBase? _lastPhotosPage;

    [RelayCommand]
    private void OpenSettings()
    {
        var vm = new SettingsViewModel(_services, Version);
        vm.Closed += () => Settings = null;
        Settings = vm;
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
