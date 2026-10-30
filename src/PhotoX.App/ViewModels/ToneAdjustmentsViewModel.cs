using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PhotoX.App.Localization;
using PhotoX.Core.Editing;

namespace PhotoX.App.ViewModels;

/// <summary>
/// The editing sidebar shared by the Photos editor and the ID Photo Creator (prompt_03 §7, prompt_06 §1): Undo, Redo
/// and Reset, then the histogram with levels, Auto Adjust, exposure, contrast, white balance, tint and saturation
/// (FR-013, FR-014, FR-043, FR-044). The owner keeps the recipe and the undo history; this class only reports which
/// value the operator changed and offers the owner's commands.
/// </summary>
public sealed partial class ToneAdjustmentsViewModel : ViewModelBase
{
    public const double ExposureStep = 1.0 / 6.0;

    private readonly string _resetTipKey;
    private bool _syncing;

    /// <param name="autoAdjust">Auto Adjust of the owner (one name in both workspaces, prompt_06 §1).</param>
    /// <param name="undo">The owner's Undo, Redo and Reset (image adjustments only).</param>
    /// <param name="resetTipKey">Text key of the Reset tooltip: what stays when resetting.</param>
    public ToneAdjustmentsViewModel(IAsyncRelayCommand? autoAdjust = null, IRelayCommand? undo = null, IRelayCommand? redo = null, IRelayCommand? reset = null,
        string resetTipKey = "ResetAdjustmentsTip")
    {
        AutoAdjustCommand = autoAdjust;
        UndoCommand = undo;
        RedoCommand = redo;
        ResetAdjustmentsCommand = reset;
        _resetTipKey = resetTipKey;
        Localizer.Instance.LanguageChanged += (_, _) => RaiseTexts();
    }

    /// <summary>Raised when the operator changes a value. The tag lets quick successive changes share one undo step.</summary>
    public event Action<string?>? Changed;

    public IAsyncRelayCommand? AutoAdjustCommand { get; }

    public bool HasAutoAdjust => AutoAdjustCommand is not null;

    public IRelayCommand? UndoCommand { get; }

    public IRelayCommand? RedoCommand { get; }

    public IRelayCommand? ResetAdjustmentsCommand { get; }

    /// <summary>Set by the owner whenever its history changes.</summary>
    [ObservableProperty]
    public partial bool CanUndo { get; set; }

    [ObservableProperty]
    public partial bool CanRedo { get; set; }

    public string ResetTip => Localizer.Instance[_resetTipKey];

    [ObservableProperty]
    public partial double Exposure { get; set; }

    [ObservableProperty]
    public partial double Contrast { get; set; }

    [ObservableProperty]
    public partial double Temperature { get; set; }

    [ObservableProperty]
    public partial double Tint { get; set; }

    [ObservableProperty]
    public partial double Saturation { get; set; }

    [ObservableProperty]
    public partial double LevelsBlack { get; set; }

    [ObservableProperty]
    public partial double LevelsMid { get; set; } = 1;

    [ObservableProperty]
    public partial double LevelsWhite { get; set; } = 1;

    [ObservableProperty]
    public partial Histogram? Histogram { get; set; }

    public string ExposureText => Localizer.Instance.Format("ExposureValue", Exposure);

    public string ContrastText => Signed(Contrast);

    public string TemperatureText => Signed(Temperature);

    /// <summary>The tint slider runs from magenta (left) to green (right), so green is shown as positive.</summary>
    public string TintText => Signed(-Tint);

    public string SaturationText => Signed(Saturation);

    public string LevelsText => string.Format(Localizer.Instance.Culture, "{0:0}  ·  {1:0.00}  ·  {2:0}", LevelsBlack * 255, LevelsMid, LevelsWhite * 255);

    partial void OnExposureChanged(double value) => Report(nameof(ExposureText), "exposure");

    partial void OnContrastChanged(double value) => Report(nameof(ContrastText), "contrast");

    partial void OnTemperatureChanged(double value) => Report(nameof(TemperatureText), "temperature");

    partial void OnTintChanged(double value) => Report(nameof(TintText), "tint");

    partial void OnSaturationChanged(double value) => Report(nameof(SaturationText), "saturation");

    partial void OnLevelsBlackChanged(double value) => Report(nameof(LevelsText), "levels-black");

    partial void OnLevelsMidChanged(double value) => Report(nameof(LevelsText), "levels-mid");

    partial void OnLevelsWhiteChanged(double value) => Report(nameof(LevelsText), "levels-white");

    /// <summary>Keyboard − (prompt_04 §7): 1/6 EV darker.</summary>
    [RelayCommand]
    public void ExposureMinus() => Exposure = Math.Clamp(Math.Round((Exposure - ExposureStep) * 6) / 6, -2, 2);

    /// <summary>Keyboard + (prompt_04 §7): 1/6 EV brighter.</summary>
    [RelayCommand]
    public void ExposurePlus() => Exposure = Math.Clamp(Math.Round((Exposure + ExposureStep) * 6) / 6, -2, 2);

    /// <summary>Clears every tone adjustment (levels, exposure, contrast, white balance, tint, saturation) as one change.</summary>
    [RelayCommand]
    public void Reset() => Batch(() => (Exposure, Contrast, Temperature, Tint, Saturation, LevelsBlack, LevelsMid, LevelsWhite) = (0, 0, 0, 0, 0, 0, 1, 1));

    /// <summary>The recipe with these tone values (rounded as stored).</summary>
    public EditRecipe ApplyTo(EditRecipe recipe) => recipe with
    {
        Exposure = Math.Round(Exposure, 3),
        Contrast = Math.Round(Contrast),
        Temperature = Math.Round(Temperature),
        Tint = Math.Round(Tint),
        Saturation = Math.Round(Saturation),
        LevelsBlack = Math.Round(LevelsBlack, 3),
        LevelsMid = Math.Round(LevelsMid, 2),
        LevelsWhite = Math.Round(LevelsWhite, 3),
    };

    /// <summary>Shows the tone values of <paramref name="recipe"/> without reporting a change.</summary>
    public void Load(EditRecipe recipe)
    {
        _syncing = true;
        try
        {
            Exposure = recipe.Exposure;
            Contrast = recipe.Contrast;
            Temperature = recipe.Temperature;
            Tint = recipe.Tint;
            Saturation = recipe.Saturation;
            LevelsBlack = recipe.LevelsBlack;
            LevelsMid = recipe.LevelsMid;
            LevelsWhite = recipe.LevelsWhite;
        }
        finally
        {
            _syncing = false;
        }
    }

    private void Batch(Action change)
    {
        _syncing = true;
        try
        {
            change();
        }
        finally
        {
            _syncing = false;
        }

        Changed?.Invoke(null);
    }

    private void Report(string textProperty, string tag)
    {
        OnPropertyChanged(textProperty);
        if (!_syncing) Changed?.Invoke(tag);
    }

    private void RaiseTexts()
    {
        OnPropertyChanged(nameof(ExposureText));
        OnPropertyChanged(nameof(ContrastText));
        OnPropertyChanged(nameof(TemperatureText));
        OnPropertyChanged(nameof(TintText));
        OnPropertyChanged(nameof(SaturationText));
        OnPropertyChanged(nameof(LevelsText));
        OnPropertyChanged(nameof(ResetTip));
    }

    private static string Signed(double v) => Math.Round(v) == 0 ? "0" : v.ToString("+0;−0", Localizer.Instance.Culture);
}
