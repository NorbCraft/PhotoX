using Avalonia.Controls;
using PhotoX.App.Services;

namespace PhotoX.App.Views;

/// <summary>Resizable side panels (prompt_03 §3): limits and the remembered width.</summary>
internal static class PanelWidth
{
    public const double Min = 300;
    public const double Max = 620;

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
}
