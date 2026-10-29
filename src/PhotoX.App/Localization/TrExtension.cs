using Avalonia.Data;
using Avalonia.Markup.Xaml;

namespace PhotoX.App.Localization;

/// <summary>XAML: <c>Text="{l:Tr SaveAndNext}"</c> — live-updating localized string.</summary>
public sealed class TrExtension(string key) : MarkupExtension
{
    public string Key { get; set; } = key;

    public override object ProvideValue(IServiceProvider serviceProvider) =>
        new ReflectionBinding(nameof(LocalizedString.Value)) { Source = Localizer.Instance.Observe(Key), Mode = BindingMode.OneWay };
}
