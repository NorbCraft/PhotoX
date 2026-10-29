using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using PhotoX.App.ViewModels;

namespace PhotoX.App.Views;

public sealed partial class MainWindow : Window
{
    private readonly HashSet<Key> _held = [];

    public MainWindow()
    {
        InitializeComponent();
        AddHandler(KeyDownEvent, OnKeyDownTunnel, RoutingStrategies.Tunnel);
        AddHandler(KeyUpEvent, OnKeyUpTunnel, RoutingStrategies.Tunnel);
        Deactivated += (_, _) => _held.Clear();
    }

    /// <summary>
    /// Window-level shortcuts (FR-018): held-key auto-repeat is ignored, and nothing is intercepted while a text or
    /// numeric input, a list box or a combo box has keyboard focus.
    /// </summary>
    private void OnKeyDownTunnel(object? sender, KeyEventArgs e)
    {
        var repeat = !_held.Add(e.Key);
        if (DataContext is not ShellViewModel shell || IsInputFocused()) return;
        if (repeat)
        {
            if (shell.CurrentPage is EditorViewModel && e.Key is Key.Enter or Key.Left or Key.Right or Key.PageDown or Key.PageUp or Key.Space)
            {
                e.Handled = true;
            }

            return;
        }

        switch (shell.CurrentPage)
        {
            case EditorViewModel editor:
                e.Handled = editor.HandleKey(e.Key, e.KeyModifiers);
                break;
            case BrowseViewModel browse when e.Key == Key.Enter && e.KeyModifiers == KeyModifiers.None:
                browse.StartEditingCommand.Execute(null);
                e.Handled = true;
                break;
        }
    }

    private void OnKeyUpTunnel(object? sender, KeyEventArgs e)
    {
        _held.Remove(e.Key);
        if (DataContext is ShellViewModel { CurrentPage: EditorViewModel editor } && !IsInputFocused())
        {
            e.Handled = editor.HandleKeyUp(e.Key);
        }
    }

    private bool IsInputFocused() =>
        FocusManager?.GetFocusedElement() is TextBox or NumericUpDown or ComboBox or AutoCompleteBox;
}
