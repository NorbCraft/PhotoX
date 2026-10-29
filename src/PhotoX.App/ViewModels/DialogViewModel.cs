using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace PhotoX.App.ViewModels;

public sealed partial class DialogChoice(string id, string title, string description) : ViewModelBase
{
    public string Id { get; } = id;

    public string Title { get; } = title;

    public string Description { get; } = description;

    [ObservableProperty]
    public partial bool IsSelected { get; set; }
}

public sealed record DialogButton(string Id, string Text, bool IsPrimary);

/// <summary>In-window dialog (no OS message boxes): a message, optional choices and buttons.</summary>
public sealed partial class DialogViewModel(string title, string message) : ViewModelBase
{
    private readonly TaskCompletionSource<string?> _result = new();

    public string Title { get; } = title;

    public string Message { get; } = message;

    public string? Note { get; init; }

    public ObservableCollection<DialogChoice> Choices { get; } = [];

    public ObservableCollection<DialogButton> Buttons { get; } = [];

    [ObservableProperty]
    public partial DialogChoice? SelectedChoice { get; set; }

    partial void OnSelectedChoiceChanged(DialogChoice? value)
    {
        foreach (var c in Choices) c.IsSelected = c == value;
    }

    [RelayCommand]
    private void Choose(DialogChoice choice) => SelectedChoice = choice;

    public bool HasChoices => Choices.Count > 0;

    public bool HasNote => Note is not null;

    public Task<string?> Result => _result.Task;

    [RelayCommand]
    private void Press(string id) => _result.TrySetResult(id);

    public void Cancel() => _result.TrySetResult(null);
}
