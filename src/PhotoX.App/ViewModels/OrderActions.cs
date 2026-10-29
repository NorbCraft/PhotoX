using PhotoX.App.Localization;
using PhotoX.App.Services;
using PhotoX.Storage;
using Serilog;

namespace PhotoX.App.ViewModels;

/// <summary>Finish Order and saving pending edits (FR-072, FR-019).</summary>
public sealed partial class ShellViewModel
{
    public bool HasPendingEdits
    {
        get
        {
            if (CurrentPage is EditorViewModel) Editor.KeepPending();
            return Browse.AllPhotos.Any(p => p.PendingRecipe is not null);
        }
    }

    /// <summary>Saves all unsaved manual edits; stops at the first failure and reports it.</summary>
    public async Task<bool> SavePendingAsync()
    {
        Editor.KeepPending();
        foreach (var item in Browse.AllPhotos.Where(p => p.PendingRecipe is not null).ToList())
        {
            var recipe = item.PendingRecipe!;
            var stamp = item.Entry.Stamp;
            var result = await Task.Run(() => _services.Saver.Save(item.Path, recipe, stamp));
            if (!result.Succeeded)
            {
                Log.Warning("Saving pending edit failed for {Path}: {Error}", item.Path, result.Error);
                var d = new DialogViewModel(Localizer.Instance["SaveFailedTitle"], Localizer.Instance.Format("SaveFailedFor", item.Name, ErrorTexts.ForSave(result)));
                d.Buttons.Add(new DialogButton("ok", Localizer.Instance["Ok"], true));
                await ShowDialogAsync(d);
                return false;
            }

            item.PendingRecipe = null;
            item.State = result.Outcome == SaveOutcome.Reviewed ? PhotoState.Reviewed : PhotoState.Edited;
            item.UpdateEntry();
            Browse.RefreshThumbnail(item);
        }

        Browse.RefreshStates();
        return true;
    }

    public async Task FinishOrderAsync()
    {
        var folder = Browse.CurrentFolder;
        if (folder is null || Editor.IsBusy || Dialog is not null) return;

        // 1. Unsaved manual edits: save them or go back; never discard silently.
        var pending = Browse.AllPhotos.Where(p => p.PendingRecipe is not null).ToList();
        if (CurrentPage is EditorViewModel { IsDirty: true }) Editor.KeepPending();
        pending = Browse.AllPhotos.Where(p => p.PendingRecipe is not null).ToList();
        if (pending.Count > 0)
        {
            var ask = new DialogViewModel(Localizer.Instance["UnsavedTitle"],
                Localizer.Instance.Format("UnsavedMessage", pending.Count, string.Join(", ", pending.Take(6).Select(p => p.Name))));
            ask.Buttons.Add(new DialogButton("back", Localizer.Instance["ReturnToEditing"], false));
            ask.Buttons.Add(new DialogButton("save", Localizer.Instance["SaveThem"], true));
            if (await ShowDialogAsync(ask) != "save")
            {
                await OpenEditorAsync(pending[0]);
                return;
            }

            if (!await SavePendingAsync()) return;
        }

        // 2. One confirmation: erase (default) or keep.
        Browse.RefreshStates();
        var confirm = new DialogViewModel(Localizer.Instance.Format("FinishTitle", Browse.FolderTitle),
            Localizer.Instance.Format("FinishStatus", Browse.EditedCount, Browse.ReviewedCount))
        {
            Note = Localizer.Instance["FinishNote"],
        };
        var erase = new DialogChoice("erase", Localizer.Instance["FinishErase"], Localizer.Instance["FinishEraseDesc"]);
        confirm.Choices.Add(erase);
        confirm.Choices.Add(new DialogChoice("keep", Localizer.Instance["FinishKeep"], Localizer.Instance["FinishKeepDesc"]));
        confirm.SelectedChoice = erase;
        confirm.Buttons.Add(new DialogButton("cancel", Localizer.Instance["Cancel"], false));
        confirm.Buttons.Add(new DialogButton("finish", Localizer.Instance["FinishButton"], true));
        if (await ShowDialogAsync(confirm) != "finish") return;

        string message;
        if (confirm.SelectedChoice?.Id == "erase")
        {
            var result = await Task.Run(() => OrderCleanup.Erase(folder, _services.ThumbnailCache));
            Log.Information("Finish order (erase) {Folder}: {Originals} originals, {Temps} temp files, remaining {Remaining}",
                folder, result.DeletedOriginals, result.DeletedTemporary, result.Remaining.Count);
            message = result.Complete
                ? Localizer.Instance.Format("FinishedErased", result.DeletedOriginals)
                : Localizer.Instance.Format("FinishedRemaining", string.Join("\n", result.Remaining.Take(10)));
        }
        else
        {
            Log.Information("Finish order (keep) {Folder}", folder);
            message = Localizer.Instance["FinishedKept"];
        }

        CurrentPage = Browse;
        Browse.OpenFolder(folder);
        var done = new DialogViewModel(Localizer.Instance["FinishButton"], message);
        done.Buttons.Add(new DialogButton("ok", Localizer.Instance["Ok"], true));
        await ShowDialogAsync(done);
    }

    public async Task<string?> ShowDialogAsync(DialogViewModel dialog)
    {
        Dialog = dialog;
        try
        {
            return await dialog.Result;
        }
        finally
        {
            if (Dialog == dialog) Dialog = null;
        }
    }
}
