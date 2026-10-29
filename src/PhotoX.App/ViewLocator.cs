using System.Diagnostics.CodeAnalysis;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using PhotoX.App.ViewModels;
using PhotoX.App.Views;

namespace PhotoX.App;

/// <summary>Maps page view models to views (no reflection, so it is trim-safe).</summary>
public sealed class ViewLocator : IDataTemplate
{
    public Control? Build(object? param) => param switch
    {
        BrowseViewModel => new BrowseView(),
        EditorViewModel => new EditorView(),
        IdViewModel => new IdView(),
        _ => new TextBlock { Text = param?.GetType().Name },
    };

    public bool Match(object? data) => data is BrowseViewModel or EditorViewModel or IdViewModel;
}
