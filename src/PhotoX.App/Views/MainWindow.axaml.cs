using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
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
        shell.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(ShellViewModel.Dialog)) OnDialogChanged(shell.Dialog);
        };
    }

    private IInputElement? _focusBeforeDialog;

    /// <summary>
    /// A dialog takes the keyboard (prompt_10 §4): focus moves to its Cancel button (the safe default, else the
    /// highlighted one) and returns to where it was when the dialog closes, such as the gallery.
    /// </summary>
    private void OnDialogChanged(DialogViewModel? dialog)
    {
        if (dialog is null)
        {
            if (_focusBeforeDialog is InputElement { IsEffectivelyVisible: true } previous) previous.Focus(NavigationMethod.Tab);
            _focusBeforeDialog = null;
            return;
        }

        _focusBeforeDialog ??= FocusManager?.GetFocusedElement();
        Dispatcher.UIThread.Post(() =>
        {
            var buttons = DialogButtonControls();
            var start = buttons.FirstOrDefault(b => b.CommandParameter is "cancel")
                ?? buttons.FirstOrDefault(b => b.Classes.Contains("primary")) ?? buttons.LastOrDefault();
            start?.Focus(NavigationMethod.Tab);
        }, DispatcherPriority.Loaded);
    }

    private List<Button> DialogButtonControls() => DialogButtons.GetVisualDescendants().OfType<Button>().ToList();

    /// <summary>Keys while a dialog is open: none reaches the workspace behind it (the gallery moved its selection before).</summary>
    private void HandleDialogKey(DialogViewModel dialog, KeyEventArgs e)
    {
        var buttons = DialogButtonControls();
        var focused = FocusManager?.GetFocusedElement() is Button b ? buttons.IndexOf(b) : -1;
        switch (e.Key)
        {
            case Key.Escape:
                dialog.Cancel();
                break;
            case Key.Left or Key.Right or Key.Tab when buttons.Count > 0:
                var back = e.Key == Key.Left || (e.Key == Key.Tab && e.KeyModifiers.HasFlag(KeyModifiers.Shift));
                var target = focused < 0 ? (back ? buttons.Count - 1 : 0) : Math.Clamp(focused + (back ? -1 : 1), 0, buttons.Count - 1);
                buttons[target].Focus(NavigationMethod.Tab);
                break;
            case Key.Up or Key.Down when dialog.HasChoices:
                var i = dialog.SelectedChoice is { } c ? dialog.Choices.IndexOf(c) : -1;
                dialog.SelectedChoice = dialog.Choices[Math.Clamp(i + (e.Key == Key.Up ? -1 : 1), 0, dialog.Choices.Count - 1)];
                break;
            case Key.Enter or Key.Space when focused >= 0:
                buttons[focused].Command?.Execute(buttons[focused].CommandParameter);
                break;
        }

        e.Handled = true;
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
        if (DataContext is not ShellViewModel shell) return;
        if (shell.Dialog is { } dialog)
        {
            HandleDialogKey(dialog, e);
            return;
        }

        if (shell.Settings is { } settings)
        {
            // Escape closes Settings like its Close button; settings are applied as they change (prompt_11 §2).
            if (e.Key == Key.Escape && !repeat)
            {
                settings.CloseCommand.Execute(null);
                e.Handled = true;
            }

            return;
        }

        if (IsInputFocused()) return;
        if (repeat)
        {
            // A held arrow key keeps moving the crop frame (prompt_10 §1); other held keys act once.
            if (shell.CurrentPage is EditorViewModel editor && e.Key is Key.Left or Key.Right or Key.Up or Key.Down)
            {
                e.Handled = editor.HandleKey(e.Key, e.KeyModifiers);
                return;
            }

            if (shell.CurrentPage is EditorViewModel or IdViewModel
                && (e.Key is Key.Enter or Key.Left or Key.Right or Key.Up or Key.Down or Key.PageDown or Key.PageUp or Key.Space or Key.Back || e.PhysicalKey == EditorViewModel.BeforeAfterKey))
            {
                e.Handled = true;
            }

            return;
        }

        switch (shell.CurrentPage)
        {
            case EditorViewModel editor:
                e.Handled = editor.HandleKey(e.Key, e.KeyModifiers, e.PhysicalKey);
                break;
            case IdViewModel id:
                e.Handled = id.HandleKey(e.Key, e.KeyModifiers, e.PhysicalKey);
                break;
            case CollageViewModel collage:
                e.Handled = collage.HandleKey(e.Key);
                break;
            case PrintViewModel print when e.Key == Key.Escape:
                print.BackCommand.Execute(null); // back to where the print came from (prompt_07 §3.7)
                e.Handled = true;
                break;
            case PrintViewModel print when print.HasCopiesTargets && (e.KeyModifiers & ~KeyModifiers.Shift) == KeyModifiers.None && QuantityKey(e.Key) is { } printKey:
                // Copies of the selected pages, as in Photos (prompt_10 §6)
                if (printKey == '+') print.PhotoCopiesPlusCommand.Execute(null);
                else if (printKey == '-') print.PhotoCopiesMinusCommand.Execute(null);
                else print.SetSelectedCopies(printKey - '0');
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
            EditorViewModel editor => editor.HandleKeyUp(e.Key, e.PhysicalKey),
            IdViewModel id => id.HandleKeyUp(e.Key, e.PhysicalKey),
            _ => false,
        };
    }

    private bool IsInputFocused() =>
        FocusManager?.GetFocusedElement() is TextBox or NumericUpDown or ComboBox or AutoCompleteBox;
}
