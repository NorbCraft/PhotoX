using Avalonia.Controls;
using PhotoX.App.Services;

namespace PhotoX.App.Views;

/// <summary>
/// Resizable side panels (prompt_03 §3, prompt_06 §1): limits and the remembered width. The centre column takes the
/// rest, so the photo preview grows and shrinks with the panels.
/// </summary>
internal static class PanelWidth
{
    public const double Min = 300;
    public const double Max = 620;

    /// <summary>The narrowest explorer panel that still shows folder names and two thumbnails per row.</summary>
    public const double MinLeft = 230;

    public static void Apply(ColumnDefinition column, double width)
    {
        column.MinWidth = Min;
        column.MaxWidth = Max;
        column.Width = new GridLength(Math.Clamp(width, Min, Max));
    }

    public static void Save(Action<PhotoX.Storage.Settings> store)
    {
        if (AppServices.Current is not { } services) return;
        store(services.Settings);
        services.SaveSettings();
    }

    /// <summary>Applies the width remembered under <paramref name="name"/> (or the default) with the panel's limits.</summary>
    public static void Apply(ColumnDefinition column, string name, double defaultWidth, double min = Min, double max = Max)
    {
        var width = AppServices.Current?.Settings.PanelWidths.TryGetValue(name, out var w) == true ? w : defaultWidth;
        column.MinWidth = min;
        column.MaxWidth = max;
        column.Width = new GridLength(Math.Clamp(width, min, max));
    }

    public static void Save(string name, ColumnDefinition column) => Save(s => s.PanelWidths[name] = Math.Round(column.ActualWidth));
}
