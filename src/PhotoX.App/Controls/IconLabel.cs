using Avalonia;
using Avalonia.Controls;
using Path = Avalonia.Controls.Shapes.Path;
using Avalonia.Layout;
using Avalonia.Media;

namespace PhotoX.App.Controls;

/// <summary>
/// The content of a button with an icon beside its label (the owner's choice B of the button mockups, DEC-075). The
/// label wraps onto a second line instead of being cut off when the button is narrow, such as two or three buttons in
/// a row of a side panel with Romanian labels. Colours come from the <c>Path.bi</c> and <c>TextBlock.bl</c> styles
/// (white on blue buttons, dimmed when disabled); the font size and weight are inherited from the button.
/// </summary>
public sealed class IconLabel : Grid
{
    public static readonly StyledProperty<Geometry?> IconProperty = AvaloniaProperty.Register<IconLabel, Geometry?>(nameof(Icon));
    public static readonly StyledProperty<string?> TextProperty = AvaloniaProperty.Register<IconLabel, string?>(nameof(Text));

    private readonly Path _icon = new();
    private readonly TextBlock _text = new()
    {
        TextWrapping = TextWrapping.Wrap,
        TextAlignment = TextAlignment.Center,
        VerticalAlignment = VerticalAlignment.Center,
        Margin = new Thickness(6, 0, 0, 0),
    };

    public IconLabel()
    {
        ColumnDefinitions = new ColumnDefinitions("Auto,*");
        HorizontalAlignment = HorizontalAlignment.Center;
        VerticalAlignment = VerticalAlignment.Center;
        _icon.Classes.Add("bi");
        _text.Classes.Add("bl");
        var box = new Viewbox { Child = _icon };
        box.Classes.Add("bic");
        SetColumn(_text, 1);
        Children.Add(box);
        Children.Add(_text);
    }

    public Geometry? Icon { get => GetValue(IconProperty); set => SetValue(IconProperty, value); }

    public string? Text { get => GetValue(TextProperty); set => SetValue(TextProperty, value); }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IconProperty) _icon.Data = Icon;
        else if (change.Property == TextProperty) _text.Text = Text;
    }
}
