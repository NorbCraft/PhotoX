using Avalonia.Controls.Primitives;

namespace PhotoX.App.Controls;

/// <summary>
/// One choice of several shown as a toggle (the workspaces, the language, the ID steps, Fill | Borders …). A click runs
/// its command but never unchecks it: a plain toggle button unchecked itself when the choice already shown was clicked
/// again, while its one-way binding stayed true, so the blue highlight disappeared although the workspace was still
/// open (prompt_09 §9). Its state comes only from the binding. Styled as a toggle button.
/// </summary>
public sealed class ChoiceButton : ToggleButton
{
    protected override Type StyleKeyOverride => typeof(ToggleButton);

    protected override void Toggle()
    {
    }
}
