using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using PhotoX.App.Controls;
using PhotoX.Tests;

namespace PhotoX.App.Tests;

/// <summary>
/// Buttons with an icon and a label (DEC-075, DEC-076) keep their whole label at a common laptop size in both
/// languages: a long label wraps between words, it is never cut off or broken inside a word.
/// </summary>
public class ButtonFitTests
{
    [AvaloniaTheory]
    [InlineData("en", false)]
    [InlineData("ro", false)]
    [InlineData("en", true)]
    [InlineData("ro", true)]
    public async Task Labels_beside_icons_fit_in_every_workspace(string language, bool narrowPanels)
    {
        using var dir = new TempDir();
        if (narrowPanels)
        {
            // Every side panel dragged to its narrowest.
            var config = Path.Combine(dir.Path, "cfg", "config");
            Directory.CreateDirectory(config);
            File.WriteAllText(Path.Combine(config, "settings.json"), """
                { "version": 2, "editorPanelWidth": 1, "idPanelWidth": 1,
                  "panelWidths": { "browse-left": 1, "browse-right": 1, "collage-left": 1, "collage-thumbs": 1, "collage-right": 1, "id-left": 1, "print-left": 1, "print-right": 1 } }
                """);
        }

        var order = Path.Combine(dir.Path, "order");
        Directory.CreateDirectory(order);
        Fixtures.WriteJpeg(order, "P1.jpg", 900, 600);
        var (window, shell) = UiHelpers.CreateWindow(Path.Combine(dir.Path, "cfg"), 1366, 768, language);
        shell.SetLanguageCommand.Execute(language);
        await shell.Browse.OpenFolderAsync(order);
        shell.Browse.SetSelection(shell.Browse.AllPhotos);
        UiHelpers.Capture(window, $"buttons-browse-{language}{(narrowPanels ? "-narrow" : "")}");
        AssertLabelsFit(window);
        shell.Browse.EditPhotoCommand.Execute(shell.Browse.AllPhotos[0]);
        await UiHelpers.WaitUntil(() => shell.Editor is { Preview: not null, IsLoading: false });
        UiHelpers.Capture(window, $"buttons-editor-{language}{(narrowPanels ? "-narrow" : "")}");
        AssertLabelsFit(window);
        shell.ShowIdCommand.Execute(null);
        await shell.Id.LoadSourceAsync(Path.Combine(order, "P1.jpg"));
        shell.Id.ContinueToSheetCommand.Execute(null);
        UiHelpers.Capture(window, $"buttons-id-{language}{(narrowPanels ? "-narrow" : "")}");
        AssertLabelsFit(window);
        shell.ShowCollagesCommand.Execute(null);
        UiHelpers.Capture(window, $"buttons-collage-{language}{(narrowPanels ? "-narrow" : "")}");
        AssertLabelsFit(window);
        shell.PrintPhotos(shell.Browse.AllPhotos);
        UiHelpers.Capture(window, $"buttons-printing-{language}{(narrowPanels ? "-narrow" : "")}");
        AssertLabelsFit(window);
        window.Close();
    }

    private static void AssertLabelsFit(Window window)
    {
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        // Buttons whose icon, label and key chip stay on one line (Save & Next, Continue).
        foreach (var row in window.GetVisualDescendants().OfType<StackPanel>().Where(p => p.Classes.Contains("bs") && p.IsEffectivelyVisible))
        {
            var button = row.FindAncestorOfType<Button>()!;
            var room = button.Bounds.Width - button.Padding.Left - button.Padding.Right - button.BorderThickness.Left - button.BorderThickness.Right;
            Assert.True(row.DesiredSize.Width <= room + 0.5, $"a button row in {row.FindAncestorOfType<UserControl>()?.GetType().Name} needs {row.DesiredSize.Width:0} px of {room:0}");
        }

        foreach (var label in window.GetVisualDescendants().OfType<IconLabel>().Where(l => l.IsEffectivelyVisible))
        {
            var button = label.FindAncestorOfType<Button>()!;
            var text = label.GetVisualDescendants().OfType<TextBlock>().Single();
            Assert.True(label.Bounds.Width <= button.Bounds.Width - button.Padding.Left - button.Padding.Right + 0.5,
                $"\"{text.Text}\" is wider than its button ({label.Bounds.Width:0} > {button.Bounds.Width:0})");
            var lines = text.TextLayout.TextLines;
            Assert.True(lines.Count <= 2, $"\"{text.Text}\" takes {lines.Count} lines");
            var position = 0;
            foreach (var line in lines.Take(lines.Count - 1))
            {
                position += line.Length;
                Assert.True(position >= text.Text!.Length || char.IsWhiteSpace(text.Text[position - 1]) || char.IsWhiteSpace(text.Text[position]),
                    $"\"{text.Text}\" is broken inside a word in {label.FindAncestorOfType<UserControl>()?.GetType().Name} (button {button.Bounds.Width:0} px, label {label.Bounds.Width:0} px)");
            }
        }
    }
}
