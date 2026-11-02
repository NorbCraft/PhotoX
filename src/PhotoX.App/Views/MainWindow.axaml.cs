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
        Closing += OnClosing;
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        if (DataContext is not ShellViewModel shell) return;
        shell.ExitRequested += CloseConfirmed;
        shell.OpenUrlRequested += url => _ = Launcher.LaunchUriAsync(new Uri(url));
    }

    private bool _closeConfirmed;

    /// <summary>
    /// Unsaved manual edits are never discarded silently (FR-019): ask once before closing. Print jobs not yet handed
    /// to Windows would be lost, so closing asks first as well (prompt_06 §7).
    /// </summary>
    private async void OnClosing(object? sender, WindowClosingEventArgs e)
    {
        if (_closeConfirmed || DataContext is not ShellViewModel shell || shell.Dialog is not null) return;
        if (!shell.HasPendingEdits && shell.Print.Queue.UnsentCount == 0) return;
        e.Cancel = true;
        if (!await shell.ConfirmCloseAsync()) return;
        _closeConfirmed = true;
        Close();
    }

    /// <summary>Closes without asking again (the shell has already asked, e.g. before installing an update).</summary>
    public void CloseConfirmed()
    {
        _closeConfirmed = true;
        Close();
    }

    /// <summary>
    /// Window-level shortcuts (FR-018): held-key auto-repeat is ignored, and nothing is intercepted while a text or
    /// numeric input, a list box or a combo box has keyboard focus.
    /// </summary>
    private void OnKeyDownTunnel(object? sender, KeyEventArgs e)
    {
        var repeat = !_held.Add(e.Key);
        if (DataContext is not ShellViewModel shell || IsInputFocused()) return;
        if (shell.Dialog is not null || shell.Settings is not null) return;
        if (repeat)
        {
            if (shell.CurrentPage is EditorViewModel or IdViewModel && e.Key is Key.Enter or Key.Left or Key.Right or Key.PageDown or Key.PageUp or Key.Space)
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
            case IdViewModel id:
                e.Handled = id.HandleKey(e.Key, e.KeyModifiers);
                break;
            case CollageViewModel collage:
                e.Handled = collage.HandleKey(e.Key);
                break;
            case PrintViewModel print when e.Key == Key.Escape:
                print.BackCommand.Execute(null); // back to where the print came from (prompt_07 §3.7)
                e.Handled = true;
                break;
            case BrowseViewModel browse when e.Key == Key.Enter && e.KeyModifiers == KeyModifiers.None:
                browse.OpenSelected(); // Edit this photo (prompt_05 §5), or open the selected folder (prompt_08 §1)
                e.Handled = true;
                break;
            case BrowseViewModel browse when e.Key == Key.Delete && e.KeyModifiers == KeyModifiers.None && !browse.IsDeletedView:
                browse.DeleteSelectedCommand.Execute(null); // asks first (prompt_08 §5)
                e.Handled = true;
                break;
            case BrowseViewModel browse when e.Key == Key.Back && e.KeyModifiers == KeyModifiers.None:
                browse.Explorer.GoUpCommand.Execute(null);
                e.Handled = true;
                break;
            case BrowseViewModel browse when !browse.IsDeletedView && (e.KeyModifiers & ~KeyModifiers.Shift) == KeyModifiers.None && QuantityKey(e.Key) is { } key:
                // Copies of the selected photos (prompt_09 §4): + and − change them, 0–9 set them
                if (key == '+') browse.QuantityPlusCommand.Execute(null);
                else if (key == '-') browse.QuantityMinusCommand.Execute(null);
                else browse.SetSelectedQuantity(key - '0');
                e.Handled = true;
                break;
        }
    }

    /// <summary>'+', '-' or the digit of a key on the main keyboard or the number pad.</summary>
    private static char? QuantityKey(Key key) => key switch
    {
        Key.OemPlus or Key.Add => '+',
        Key.OemMinus or Key.Subtract => '-',
        >= Key.D0 and <= Key.D9 => (char)('0' + (key - Key.D0)),
        >= Key.NumPad0 and <= Key.NumPad9 => (char)('0' + (key - Key.NumPad0)),
        _ => null,
    };

    private void OnKeyUpTunnel(object? sender, KeyEventArgs e)
    {
        _held.Remove(e.Key);
        if (DataContext is not ShellViewModel shell || IsInputFocused()) return;
        e.Handled = shell.CurrentPage switch
        {
            EditorViewModel editor => editor.HandleKeyUp(e.Key),
            IdViewModel id => id.HandleKeyUp(e.Key),
            _ => false,
        };
    }

    private bool IsInputFocused() =>
        FocusManager?.GetFocusedElement() is TextBox or NumericUpDown or ComboBox or AutoCompleteBox;
}
