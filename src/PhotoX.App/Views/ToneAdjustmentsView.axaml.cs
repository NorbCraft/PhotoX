using Avalonia.Controls;
using Avalonia.Input;

namespace PhotoX.App.Views;

public sealed partial class ToneAdjustmentsView : UserControl
{
    public ToneAdjustmentsView() => InitializeComponent();

    /// <summary>Double-click resets a slider to its neutral value.</summary>
    private void OnSliderDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (sender is Slider slider && slider.Minimum < 0) slider.Value = 0;
    }
}
