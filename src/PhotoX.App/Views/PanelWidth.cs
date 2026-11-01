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

    public static void Apply(ColumnDefinition column, double width, double min = Min)
    {
        column.MinWidth = min;
        column.MaxWidth = Max;
        column.Width = new GridLength(Math.Clamp(width, min, Max));
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

    /// <summary>
    /// Shows or folds a side panel (prompt_08 §3, §4): folded, its column and splitter take no space and the centre
    /// grows; shown again, <paramref name="restore"/> gives it back its remembered width and limits.
    /// </summary>
    public static void Show(ColumnDefinition column, Control panel, Control? splitter, bool visible, Action restore)
    {
        panel.IsVisible = visible;
        if (splitter is not null) splitter.IsVisible = visible;
        if (visible)
        {
            restore();
            return;
        }

        column.MinWidth = 0;
        column.Width = new GridLength(0);
    }

    /// <summary>Keeps the folder tree column of a workspace in step with its explorer's <see cref="ViewModels.ExplorerViewModel.IsTreeVisible"/>.</summary>
    public static void FollowTree(ViewModels.ExplorerViewModel explorer, ColumnDefinition column, Control panel, Control splitter, Action restore)
    {
        void Update() => Show(column, panel, splitter, explorer.IsTreeVisible, restore);
        explorer.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(ViewModels.ExplorerViewModel.IsTreeVisible)) Update();
        };
        Update();
    }
}
