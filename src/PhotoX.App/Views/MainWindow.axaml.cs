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

    private bool _closeConfirmed;

    /// <summary>Unsaved manual edits are never discarded silently (FR-019): ask once before closing.</summary>
    private async void OnClosing(object? sender, WindowClosingEventArgs e)
    {
        if (_closeConfirmed || DataContext is not ShellViewModel shell || !shell.HasPendingEdits || shell.Dialog is not null) return;
        e.Cancel = true;
        var count = shell.Browse.AllPhotos.Count(p => p.PendingRecipe is not null);
        var names = string.Join(", ", shell.Browse.AllPhotos.Where(p => p.PendingRecipe is not null).Take(6).Select(p => p.Name));
        var d = new DialogViewModel(Localization.Localizer.Instance["UnsavedTitle"], Localization.Localizer.Instance.Format("UnsavedMessage", count, names));
        d.Buttons.Add(new DialogButton("cancel", Localization.Localizer.Instance["Cancel"], false));
        d.Buttons.Add(new DialogButton("discard", Localization.Localizer.Instance["CloseWithoutSaving"], false));
        d.Buttons.Add(new DialogButton("save", Localization.Localizer.Instance["SaveAndClose"], true));
        var answer = await shell.ShowDialogAsync(d);
        if (answer == "discard" || (answer == "save" && await shell.SavePendingAsync()))
        {
            _closeConfirmed = true;
            Close();
        }
    }

    /// <summary>
    /// Window-level shortcuts (FR-018): held-key auto-repeat is ignored, and nothing is intercepted while a text or
    /// numeric input, a list box or a combo box has keyboard focus.
    /// </summary>
    private void OnKeyDownTunnel(object? sender, KeyEventArgs e)
    {
        var repeat = !_held.Add(e.Key);
        if (DataContext is not ShellViewModel shell || IsInputFocused()) return;
        if (shell.Dialog is not null || shell.Settings is not null || shell.PrintDialog is not null) return;
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
            case BrowseViewModel browse when e.Key == Key.Enter && e.KeyModifiers == KeyModifiers.None:
                browse.EditPhotoCommand.Execute(null); // Edit this photo (prompt_05 §5)
                e.Handled = true;
                break;
        }
    }

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
