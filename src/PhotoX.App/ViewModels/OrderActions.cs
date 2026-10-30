using PhotoX.App.Localization;
using PhotoX.App.Services;
using PhotoX.Core.Formats;
using PhotoX.Printing;
using PhotoX.Storage;
using Serilog;

namespace PhotoX.App.ViewModels;

/// <summary>Save Changes, Restore Original, printing and saving pending edits (prompt_05 §5, FR-019).</summary>
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
    /// Save Changes (prompt_05 §5, replaces Finish Order): saves the unsaved edits of the chosen photos and, after a
    /// clear warning, deletes the originals PhotoX kept for them. Only verified PhotoX backups are deleted
    /// (<see cref="OrderBackups.RemoveBackups"/>); edited photos and unrelated files are never touched.
    /// </summary>
    public async Task SaveChangesAsync(IReadOnlyList<PhotoItemViewModel> items)
    {
        var folder = Browse.CurrentFolder;
        if (folder is null || Editor.IsBusy || Dialog is not null) return;
        Editor.KeepPending();
        var targets = items.Where(p => p.IsOpenable && (p.PendingRecipe is not null || p.State is PhotoState.Edited or PhotoState.Reviewed)).ToList();
        if (targets.Count == 0)
        {
            await ShowMessageAsync(Localizer.Instance["SaveChanges"], Localizer.Instance["NothingToSave"]);
            return;
        }

        var confirm = new DialogViewModel(Localizer.Instance["SaveChangesTitle"],
            Localizer.Instance.Format("SaveChangesWarning", targets.Count, Names(targets)))
        {
            Note = Localizer.Instance["SaveChangesNote"],
        };
        confirm.Buttons.Add(new DialogButton("cancel", Localizer.Instance["Cancel"], false));
        confirm.Buttons.Add(new DialogButton("save", Localizer.Instance["SaveAndDeleteOriginals"], true));
        if (await ShowDialogAsync(confirm) != "save") return;

        if (!await SavePendingAsync(targets)) return;
        var names = targets.Select(p => p.Name).ToList();
        var result = await Task.Run(() => OrderBackups.RemoveBackups(folder, names));
        Log.Information("Save changes {Folder}: {Finalized} finalized, {Kept} backups kept", folder, result.Finalized.Count, result.Kept.Count);
        Browse.RefreshStates();
        var message = result.Complete
            ? Localizer.Instance.Plural("SavedChangesDone", result.Finalized.Count)
            : Localizer.Instance.Format("SavedChangesKept", string.Join("\n", result.Kept.Take(10).Select(k => $"{k.Photo}: {Localizer.Instance["Keep" + k.Reason]}")));
        await ShowMessageAsync(Localizer.Instance["SaveChanges"], message);
    }

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

    /// <summary>Opens the Printing workspace for gallery photos: unsaved edits print as shown, untouched photos fill the paper.</summary>
    public void PrintPhotos(IReadOnlyList<PhotoItemViewModel> items)
    {
        Editor.KeepPending();
        var photos = items.Where(p => p.IsOpenable).ToList();
        if (photos.Count == 0) return;
        var sidecar = Browse.CurrentFolder is { } f ? new OrderStore(f).Load() : new Core.Orders.OrderSidecar();
        var printItems = photos.Select(p => (PrintItem)new PhotoPrintItem(p.Name, p.Path, p.PendingRecipe, p.State is PhotoState.Edited or PhotoState.Reviewed, 1)).ToList();
        // The print size follows the photos' format: 15×20 when every edited photo was made for 15×20 or 13×18.
        var formats = photos.Select(p => p.PendingRecipe?.FormatId ?? sidecar.Find(p.Name)?.Recipe?.FormatId).OfType<string>().ToList();
        var size = formats.Count > 0 && formats.All(id => id is "15x20" or "13x18") ? PrintFormats.P15x20 : PrintFormats.P10x15;
        OpenPrint(printItems, size, sizeFixed: false);
    }

    /// <summary>Opens the Printing workspace with what to print (prompt_06 §7); nothing is sent before Print is pressed there.</summary>
    public void OpenPrint(IReadOnlyList<PrintItem> items, PrintFormat size, bool sizeFixed)
    {
        if (items.Count == 0) return;
        _printOrigin = IsPhotosWorkspace ? Browse : CurrentPage is PrintViewModel ? _printOrigin : CurrentPage;
        var origin = _printOrigin switch
        {
            IdViewModel => Localizer.Instance["TabId"],
            CollageViewModel => Localizer.Instance["TabCollages"],
            _ => Localizer.Instance["TabPhotos"],
        };
        ShowPrinting();
        Print.Load(items, size, sizeFixed, origin);
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
