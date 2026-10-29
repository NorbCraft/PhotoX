using System.Globalization;
using Avalonia.Data.Converters;

namespace PhotoX.App.Services;

/// <summary>True when the bound value's string form equals the converter parameter (used for selected chips).</summary>
public sealed class EqualsConverter : IValueConverter
{
    public static readonly EqualsConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        string.Equals(value?.ToString(), parameter?.ToString(), StringComparison.Ordinal);

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotSupportedException();
}

/// <summary>Small display helpers for icons, badges and names.</summary>
public static class IconConverter
{
    public static readonly Avalonia.Media.Geometry FolderGeometry = Avalonia.Media.Geometry.Parse("M3 7a2 2 0 0 1 2-2h4l2 2h8a2 2 0 0 1 2 2v8a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2z");
    public static readonly Avalonia.Media.Geometry DriveGeometry = Avalonia.Media.Geometry.Parse("M4 7h16a1 1 0 0 1 1 1v8a1 1 0 0 1-1 1H4a1 1 0 0 1-1-1V8a1 1 0 0 1 1-1z M7 12h2 M15 12h2");
    public static readonly Avalonia.Media.Geometry UsbGeometry = Avalonia.Media.Geometry.Parse("M9 3h6v6H9z M7 9h10v7a5 5 0 0 1-10 0z");
    public static readonly Avalonia.Media.Geometry ClockGeometry = Avalonia.Media.Geometry.Parse("M12 21a9 9 0 1 0 0-18 9 9 0 0 0 0 18z M12 7v5l3 2");

    public static readonly IValueConverter Drive = new FuncValueConverter<bool, Avalonia.Media.Geometry>(removable => removable ? UsbGeometry : DriveGeometry);

    public static readonly IValueConverter FolderName = new FuncValueConverter<string?, string>(p =>
        p is null ? string.Empty : Path.GetFileName(p.TrimEnd(Path.DirectorySeparatorChar)) is { Length: > 0 } n ? n : p);

    public static readonly IValueConverter BadgeGlyph = new FuncValueConverter<bool, string>(warn => warn ? "!" : "✓");

    public static readonly IValueConverter BadgeBrush = new FuncValueConverter<bool, Avalonia.Media.IBrush>(warn =>
        warn ? new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#F5A524")) : new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#34C759")));

    public static readonly IValueConverter CurrentBorder = new FuncValueConverter<bool, Avalonia.Thickness>(current => current ? new Avalonia.Thickness(3) : new Avalonia.Thickness(1));

    public static readonly IValueConverter StateBrush = new FuncValueConverter<bool, Avalonia.Media.IBrush>(warn =>
        warn ? new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#F5A524")) : new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#34C759")));
}
