using PhotoX.App.Localization;
using PhotoX.App.Services;
using PhotoX.Core.Formats;
using PhotoX.Printing;
using PhotoX.Storage;
using Serilog;

namespace PhotoX.App.ViewModels;

/// <summary>
/// Discard Originals, Restore Original, Delete and restore deleted photos, printing and saving pending edits
/// (prompt_05 §5, prompt_08 §5, FR-019).
/// </summary>
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
    public Task<bool> SavePendingAsync()
    {
        Editor.KeepPending();
        return SavePendingAsync(Browse.AllPhotos.Where(p => p.PendingRecipe is not null).ToList());
    }

    private async Task<bool> SavePendingAsync(IReadOnlyList<PhotoItemViewModel> items)
    {
        foreach (var item in items.Where(p => p.PendingRecipe is not null))
        {
            var recipe = item.PendingRecipe!;
            var stamp = item.Entry.Stamp;
            var result = await Task.Run(() => _services.Saver.Save(item.Path, recipe, stamp));
            if (!result.Succeeded)
            {
                Log.Warning("Saving pending edit failed for {Path}: {Error}", item.Path, result.Error);
                await ShowMessageAsync(Localizer.Instance["SaveFailedTitle"], Localizer.Instance.Format("SaveFailedFor", item.Name, ErrorTexts.ForSave(result)));
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

    /// <summary>
    /// Discard Originals (prompt_05 §5, for the whole open folder since prompt_08 §5): saves every unsaved edit of the
    /// folder and, after a clear warning that also counts the photos marked Deleted, permanently deletes the originals
    /// PhotoX kept and the deleted photos. Only verified PhotoX files are deleted, one at a time
    /// (<see cref="OrderBackups.DiscardFolder"/>); edited photos and unrelated files are never touched.
    /// </summary>
    public async Task DiscardOriginalsAsync()
    {
        var folder = Browse.CurrentFolder;
        if (folder is null || Editor.IsBusy || Dialog is not null) return;
        Editor.KeepPending();
        var sidecar = await Task.Run(() => new OrderStore(folder).Load());
        var pending = Browse.AllPhotos.Where(p => p.PendingRecipe is not null).ToList();
        var originals = sidecar.Photos.Values.Count(r => r.Backup is not null);
        var deleted = Browse.DeletedPhotos.Count;
        if (pending.Count == 0 && sidecar.Photos.Count == 0 && sidecar.Deleted.Count == 0)
        {
            await ShowMessageAsync(Localizer.Instance["SaveChanges"], Localizer.Instance["NothingToDiscard"]);
            return;
        }

        var message = Localizer.Instance.Format("DiscardFolderWarning", Browse.FolderTitle, Localizer.Instance.Plural("OriginalsCount", originals));
        if (pending.Count > 0) message += "\n\n" + Localizer.Instance.Format("DiscardSavesFirst", Names(pending));
        if (deleted > 0) message += "\n\n" + Localizer.Instance.Plural("DiscardDeletedWarning", deleted);
        var confirm = new DialogViewModel(Localizer.Instance["SaveChangesTitle"], message) { Note = Localizer.Instance["SaveChangesNote"] };
        // A permanent deletion: Cancel is the highlighted choice.
        confirm.Buttons.Add(new DialogButton("cancel", Localizer.Instance["Cancel"], true));
        confirm.Buttons.Add(new DialogButton("discard", Localizer.Instance[deleted > 0 ? "DiscardWithDeleted" : "SaveAndDeleteOriginals"], false));
        if (await ShowDialogAsync(confirm) != "discard") return;

        if (!await SavePendingAsync(pending)) return;
        var result = await Task.Run(() => OrderBackups.DiscardFolder(folder));
        Log.Information("Discard originals {Folder}: {Finalized} finalized, {Deleted} deleted photos removed, {Kept} kept",
            folder, result.Finalized.Count, result.RemovedDeleted.Count, result.Kept.Count);
        Browse.RefreshStates();
        Browse.RemoveDeletedCards(result.RemovedDeleted);
        var done = Localizer.Instance.Plural("SavedChangesDone", result.Finalized.Count)
            + (result.RemovedDeleted.Count > 0 ? " " + Localizer.Instance.Plural("DeletedRemoved", result.RemovedDeleted.Count) : string.Empty);
        var text = result.Complete
            ? done
            : done + "\n\n" + Localizer.Instance.Format("SavedChangesKept", string.Join("\n", result.Kept.Take(10).Select(k => $"{k.Photo}: {Localizer.Instance["Keep" + k.Reason]}")));
        await ShowMessageAsync(Localizer.Instance["SaveChanges"], text);
    }

    /// <summary>
    /// Delete (prompt_08 §5): after a confirmation that names how many photos, moves them into <c>Original/</c> where
    /// they stay until Discard Originals; they can be restored from the Deleted filter. Their unsaved edits go.
    /// </summary>
    public async Task DeletePhotosAsync(IReadOnlyList<PhotoItemViewModel> items)
    {
        var folder = Browse.CurrentFolder;
        if (folder is null || Editor.IsBusy || Dialog is not null) return;
        var targets = items.Where(p => !p.IsDeleted).ToList();
        if (targets.Count == 0) return;
        var message = Localizer.Instance.Format("DeleteMessage", Names(targets));
        if (targets.Any(p => p.PendingRecipe is not null)) message += "\n\n" + Localizer.Instance["DeleteUnsavedNote"];
        var confirm = new DialogViewModel(Localizer.Instance.Plural("DeleteTitle", targets.Count), message);
        confirm.Buttons.Add(new DialogButton("cancel", Localizer.Instance["Cancel"], false));
        confirm.Buttons.Add(new DialogButton("delete", Localizer.Instance.Plural("DeleteConfirm", targets.Count), true));
        if (await ShowDialogAsync(confirm) != "delete") return;

        var result = await Task.Run(() => PhotoTrash.Delete(folder, targets.Select(p => p.Name)));
        Log.Information("Deleted {Count} photos in {Folder} (moved to Original): {Names}; {Failed} failed", result.Deleted.Count, folder,
            string.Join(", ", result.Deleted.Select(d => $"{d.Name} → {d.StoredName}")), result.Failed.Count);
        foreach (var item in targets.Where(t => result.Deleted.Any(d => string.Equals(d.Name, t.Name, StringComparison.OrdinalIgnoreCase)))) item.PendingRecipe = null;
        Browse.ApplyDeleted(targets, result.Deleted);
        if (result.Failed.Count > 0) await ShowMessageAsync(Localizer.Instance["DeleteMenu"], Localizer.Instance.Format("DeleteFailed", Failures(result.Failed)));
    }

    /// <summary>Restore (Deleted filter, prompt_08 §5): moves deleted photos back; a taken name gets a free one, reported.</summary>
    public async Task RestoreDeletedAsync(IReadOnlyList<PhotoItemViewModel> items)
    {
        var folder = Browse.CurrentFolder;
        if (folder is null || Dialog is not null) return;
        var keys = items.Select(p => p.DeletedKey).OfType<string>().ToList();
        if (keys.Count == 0) return;
        var result = await Task.Run(() => PhotoTrash.Restore(folder, keys));
        Log.Information("Restored {Count} deleted photos in {Folder}: {Names}; {Failed} failed", result.Restored.Count, folder,
            string.Join(", ", result.Restored.Select(r => $"{r.StoredName} → {r.Name}")), result.Failed.Count);
        Browse.ApplyRestored(result.Restored);
        var renamed = result.Restored
            .Select(r => (r, Original: items.FirstOrDefault(p => string.Equals(p.DeletedKey, r.StoredName, StringComparison.OrdinalIgnoreCase))?.Name))
            .Where(x => x.Original is not null && x.r.Renamed(x.Original))
            .Select(x => $"{x.Original} → {x.r.Name}")
            .ToList();
        var lines = new List<string>();
        if (renamed.Count > 0) lines.Add(Localizer.Instance.Format("RestoredRenamed", string.Join("\n", renamed)));
        if (result.Failed.Count > 0) lines.Add(Localizer.Instance.Format("RestoreDeletedFailed", Failures(result.Failed)));
        if (lines.Count > 0) await ShowMessageAsync(Localizer.Instance["RestoreDeletedMenu"], string.Join("\n\n", lines));
    }

    /// <summary>
    /// Before another folder opens while photos of this one have unsaved edits (FR-019, prompt_08 §1): save them,
    /// discard them, or stay. True when the folder may change.
    /// </summary>
    public async Task<bool> ConfirmLeaveFolderAsync()
    {
        Editor.KeepPending();
        var pending = Browse.AllPhotos.Where(p => p.PendingRecipe is not null).ToList();
        if (pending.Count == 0) return true;
        if (Dialog is not null) return false;
        var d = new DialogViewModel(Localizer.Instance["UnsavedTitle"], Localizer.Instance.Format("LeaveFolderMessage", pending.Count, Names(pending)));
        d.Buttons.Add(new DialogButton("cancel", Localizer.Instance["Cancel"], false));
        d.Buttons.Add(new DialogButton("discard", Localizer.Instance["DiscardChanges"], false));
        d.Buttons.Add(new DialogButton("save", Localizer.Instance["SaveAndOpen"], true));
        return await ShowDialogAsync(d) switch
        {
            "discard" => true,
            "save" => await SavePendingAsync(pending),
            _ => false,
        };
    }

    /// <summary>
    /// Before PhotoX closes (window close, installing an update): print jobs not yet handed to Windows would be lost
    /// and unsaved edits exist only in memory, so ask once for each (FR-019, prompt_06 §7). True when it may close.
    /// </summary>
    public async Task<bool> ConfirmCloseAsync(string closeAnywayKey = "CloseAnyway")
    {
        if (Dialog is not null) return false;
        var tr = Localizer.Instance;
        var unsent = Print.Queue.UnsentCount;
        if (unsent > 0)
        {
            var p = new DialogViewModel(tr["UnsentTitle"], tr.Plural("UnsentMessage", unsent));
            p.Buttons.Add(new DialogButton("cancel", tr["Cancel"], true));
            p.Buttons.Add(new DialogButton("close", tr[closeAnywayKey], false));
            if (await ShowDialogAsync(p) != "close") return false;
        }

        if (!HasPendingEdits) return true;
        var pending = Browse.AllPhotos.Where(p => p.PendingRecipe is not null).ToList();
        var d = new DialogViewModel(tr["UnsavedTitle"], tr.Format("UnsavedMessage", pending.Count, string.Join(", ", pending.Take(6).Select(p => p.Name))));
        d.Buttons.Add(new DialogButton("cancel", tr["Cancel"], false));
        d.Buttons.Add(new DialogButton("discard", tr["CloseWithoutSaving"], false));
        d.Buttons.Add(new DialogButton("save", tr["SaveAndClose"], true));
        var answer = await ShowDialogAsync(d);
        return answer == "discard" || (answer == "save" && await SavePendingAsync());
    }

    private static string Failures(IEnumerable<TrashFailure> failed) =>
        string.Join("\n", failed.Take(10).Select(f => $"{f.Name}: {Localizer.Instance["Trash" + f.Error]}"));

    /// <summary>
    /// Restore Original (prompt_05 §5): after a warning, discards the unsaved changes of the chosen photos and copies
    /// each preserved original back (verified before its backup is removed).
    /// </summary>
    public async Task RestoreOriginalAsync(IReadOnlyList<PhotoItemViewModel> items)
    {
        if (Browse.CurrentFolder is null || Editor.IsBusy || Dialog is not null) return;
        Editor.KeepPending();
        var sidecar = new OrderStore(Browse.CurrentFolder).Load();
        var targets = items.Where(p => p.IsOpenable && (p.PendingRecipe is not null || sidecar.Find(p.Name)?.Backup is not null)).ToList();
        if (targets.Count == 0)
        {
            await ShowMessageAsync(Localizer.Instance["RestoreOriginal"], Localizer.Instance["NothingToRestore"]);
            return;
        }

        var confirm = new DialogViewModel(Localizer.Instance["RestoreTitle"], Localizer.Instance.Format("RestoreWarning", targets.Count, Names(targets)));
        confirm.Buttons.Add(new DialogButton("cancel", Localizer.Instance["Cancel"], false));
        confirm.Buttons.Add(new DialogButton("restore", Localizer.Instance["RestoreOriginals"], true));
        if (await ShowDialogAsync(confirm) != "restore") return;

        var failed = new List<string>();
        foreach (var item in targets)
        {
            item.PendingRecipe = null;
            if (sidecar.Find(item.Name)?.Backup is null) continue;
            var result = await Task.Run(() => OrderBackups.Restore(item.Path));
            Log.Information("Restore original {Path}: {Error} {Detail}", item.Path, result.Error, result.Detail);
            if (!result.Succeeded) failed.Add($"{item.Name}: {Localizer.Instance["Restore" + result.Error]}");
            item.UpdateEntry();
            Browse.RefreshThumbnail(item);
        }

        Browse.RefreshStates();
        if (failed.Count > 0) await ShowMessageAsync(Localizer.Instance["RestoreOriginal"], Localizer.Instance.Format("RestoreFailed", string.Join("\n", failed)));
    }

    /// <summary>
    /// Opens the Printing workspace for gallery photos: unsaved edits print as shown, untouched photos fill the paper.
    /// Each photo prints its requested copies; photos set to 0 are left out (prompt_09 §5), and when every chosen photo
    /// is set to 0 nothing opens and the operator is told why.
    /// </summary>
    public void PrintPhotos(IReadOnlyList<PhotoItemViewModel> items)
    {
        Editor.KeepPending();
        var photos = items.Where(p => p.IsOpenable).ToList();
        if (photos.Count == 0) return;
        if (photos.All(p => p.Quantity == 0))
        {
            _ = ShowMessageAsync(Localizer.Instance["NothingToPrintTitle"], Localizer.Instance.Plural("AllExcluded", photos.Count));
            return;
        }

        var sidecar = Browse.CurrentFolder is { } f ? new OrderStore(f).Load() : new Core.Orders.OrderSidecar();
        // Photos set to 0 go along, hidden: the Printing workspace follows later changes of the quantities.
        var printItems = photos.Select(p => (PrintItem)new PhotoPrintItem(p.Name, p.Path, p.PendingRecipe, p.State is PhotoState.Edited or PhotoState.Reviewed, p.Quantity)).ToList();
        // The print size follows the photos' format: 15×20 when every edited photo was made for 15×20 or 13×18.
        var formats = photos.Where(p => p.Quantity > 0).Select(p => p.PendingRecipe?.FormatId ?? sidecar.Find(p.Name)?.Recipe?.FormatId).OfType<string>().ToList();
        var size = formats.Count > 0 && formats.All(id => id is "15x20" or "13x18") ? PrintFormats.P15x20 : PrintFormats.P10x15;
        OpenPrint(printItems, size, sizeFixed: false);
    }

    /// <summary>Opens the Printing workspace with what to print (prompt_06 §7); nothing is sent before Print is pressed there.</summary>
    public void OpenPrint(IReadOnlyList<PrintItem> items, PrintFormat size, bool sizeFixed)
    {
        if (items.Count == 0) return;
        ShowPrinting();
        Print.Load(items, size, sizeFixed, Print.OriginName);
    }

    private static string Names(IReadOnlyList<PhotoItemViewModel> items) =>
        string.Join(", ", items.Take(6).Select(p => p.Name)) + (items.Count > 6 ? ", …" : string.Empty);

    private async Task ShowMessageAsync(string title, string message)
    {
        var d = new DialogViewModel(title, message);
        d.Buttons.Add(new DialogButton("ok", Localizer.Instance["Ok"], true));
        await ShowDialogAsync(d);
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
