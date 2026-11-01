using System.Security.Cryptography;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using PhotoX.App.ViewModels;
using PhotoX.App.Views;
using PhotoX.Storage;
using PhotoX.Tests;

namespace PhotoX.App.Tests;

/// <summary>prompt_08 through the UI: the shared explorer, the folder tree button, ID step 1, the collage panels,
/// deleting and restoring photos, Discard Originals for the folder, and the update flow (TS-23).</summary>
public class Prompt08Tests
{
    /// <summary>An order with photos and two subfolders, inside <paramref name="dir"/>.</summary>
    private static string Order(TempDir dir, int count = 3, string name = "order")
    {
        var order = Path.Combine(dir.Path, name);
        Directory.CreateDirectory(order);
        for (var i = 1; i <= count; i++) Fixtures.WriteJpeg(order, $"P{i}.jpg", 900, 600);
        foreach (var sub in new[] { "Retouch", "Album 2" })
        {
            Directory.CreateDirectory(Path.Combine(order, sub));
            Fixtures.WriteJpeg(Path.Combine(order, sub), $"{sub[0]}1.jpg", 600, 900);
        }

        return order;
    }

    private static T Find<T>(Window window, string name)
        where T : Control => window.GetVisualDescendants().OfType<T>().First(c => c.Name == name);

    private static T FindIn<TView, T>(Window window, string name)
        where TView : Control
        where T : Control => window.GetVisualDescendants().OfType<TView>().First().GetVisualDescendants().OfType<T>().First(c => c.Name == name);

    [AvaloniaFact]
    public async Task The_photos_gallery_lists_subfolders_first_and_opens_them_like_a_file_explorer()
    {
        using var dir = new TempDir();
        var order = Order(dir);
        var (window, shell) = UiHelpers.CreateWindow(Path.Combine(dir.Path, "cfg"), 1366, 768);
        var browse = shell.Browse;
        await browse.OpenFolderAsync(order);

        var folders = browse.GalleryItems.OfType<FolderItemViewModel>().Select(f => f.Name).ToList();
        Assert.Equal(["Album 2", "Retouch"], folders);
        Assert.Equal(5, browse.GalleryItems.Count);
        Assert.IsType<FolderItemViewModel>(browse.GalleryItems[0]);                     // folders first, then the photos
        Assert.Equal("3", browse.CountAll);                                              // the counts are photos only
        Assert.Equal("P1.jpg", browse.SelectedPhoto?.Name);
        browse.SetFilterCommand.Execute(PhotoFilter.ToDo);
        Assert.Equal(5, browse.GalleryItems.Count);                                     // folders stay reachable under To do
        browse.SetFilterCommand.Execute(PhotoFilter.All);
        UiHelpers.Capture(window, "p08-photos-folders");
        App.ApplyTheme("dark");
        UiHelpers.Capture(window, "p08-photos-folders-dark");
        App.ApplyTheme("light");
        Assert.Equal(2, window.GetVisualDescendants().OfType<FolderCard>().Count(c => c.IsEffectivelyVisible));

        browse.SelectedGalleryItem = browse.GalleryItems[1];                            // Enter on a selected folder opens it
        Assert.Null(browse.SelectedPhoto);
        UiHelpers.Tap(window, Avalonia.Input.PhysicalKey.Enter);
        await UiHelpers.WaitUntil(() => browse.CurrentFolder == Path.Combine(order, "Retouch") && !browse.IsScanning);
        Assert.Equal("R1.jpg", Assert.Single(browse.AllPhotos).Name);
        Assert.Equal(Path.Combine(order, "Retouch"), browse.Explorer.CurrentFolder);
        Assert.True(browse.Explorer.CanGoUp);

        browse.Explorer.GoUpCommand.Execute(null);                                      // Up to the parent folder
        await UiHelpers.WaitUntil(() => browse.CurrentFolder == order && browse.AllPhotos.Count == 3);
        ((FolderItemViewModel)browse.GalleryItems[0]).Open();                            // a double-click on a folder card
        await UiHelpers.WaitUntil(() => browse.CurrentFolder == Path.Combine(order, "Album 2") && browse.AllPhotos.Count == 1);
        UiHelpers.Tap(window, Avalonia.Input.PhysicalKey.Backspace);                    // Backspace goes up as well
        await UiHelpers.WaitUntil(() => browse.CurrentFolder == order && browse.AllPhotos.Count == 3);
        window.Close();
    }

    [AvaloniaFact]
    public async Task The_tree_button_hides_and_shows_each_workspaces_tree_on_its_own()
    {
        using var dir = new TempDir();
        var order = Order(dir);
        var cfg = Path.Combine(dir.Path, "cfg");
        var (window, shell) = UiHelpers.CreateWindow(cfg, 1366, 768);
        await shell.Browse.OpenFolderAsync(order);
        AppServices().Settings.PanelWidths["collage-left"] = 333;

        shell.ShowCollagesCommand.Execute(null);
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();                                // a view is built when first shown
        var collageLeft = FindIn<CollageView, Grid>(window, "Root").ColumnDefinitions[0];
        Assert.True(shell.CanToggleTree && shell.IsTreeVisible);
        shell.ToggleTreeCommand.Execute(null);
        UiHelpers.Capture(window, "p08-collage-tree-hidden");
        Assert.False(shell.IsTreeVisible);
        Assert.False(FindIn<CollageView, Border>(window, "TreePanel").IsVisible);
        Assert.Equal(0, collageLeft.ActualWidth);
        Assert.Contains("collage", AppServices().Settings.TreeHidden);

        shell.ShowPhotosCommand.Execute(null);
        Assert.True(shell.IsTreeVisible);                                                // Photos keeps its own tree
        Assert.True(FindIn<BrowseView, Border>(window, "TreePanel").IsVisible);
        shell.ShowIdCommand.Execute(null);
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        Assert.True(shell.IsTreeVisible);
        shell.ToggleTreeCommand.Execute(null);                                          // and ID Photos its own
        Assert.False(FindIn<IdView, Border>(window, "TreePanel").IsVisible);

        shell.ShowCollagesCommand.Execute(null);
        shell.ToggleTreeCommand.Execute(null);                                          // shown again: the remembered width
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        UiHelpers.Capture(window, "p08-collage-tree-shown");
        Assert.Equal(333, collageLeft.Width.Value);
        Assert.Equal(order, shell.Collage.Picker.Explorer.CurrentFolder);               // the folder stays as it was
        shell.ShowPrintingCommand.Execute(null);
        Assert.False(shell.CanToggleTree);                                               // Printing has no tree
        window.Close();

        (window, shell) = UiHelpers.CreateWindow(cfg, 1366, 768);                         // remembered per workspace
        Assert.True(shell.Browse.Explorer.IsTreeVisible);
        Assert.False(shell.Id.Picker.Explorer.IsTreeVisible);
        Assert.True(shell.Collage.Picker.Explorer.IsTreeVisible);
        window.Close();
    }

    private static Services.AppServices AppServices() => Services.AppServices.Current;

    [AvaloniaFact]
    public async Task Each_workspace_keeps_its_own_folder()
    {
        using var dir = new TempDir();
        var a = Order(dir, 2, "A");
        var b = Order(dir, 2, "B");
        var c = Order(dir, 2, "C");
        var cfg = Path.Combine(dir.Path, "cfg");
        var (window, shell) = UiHelpers.CreateWindow(cfg, 1366, 768);
        await shell.Browse.OpenFolderAsync(a);
        shell.ShowIdCommand.Execute(null);
        Assert.Equal(a, shell.Id.Picker.Explorer.CurrentFolder);                          // the first time: the folder of Photos
        shell.Id.Picker.Explorer.Navigate(b);
        shell.ShowCollagesCommand.Execute(null);
        shell.Collage.Picker.Explorer.Navigate(c);
        await UiHelpers.WaitUntil(() => shell.Collage.Picker.Photos.Count == 2);

        shell.ShowPhotosCommand.Execute(null);
        Assert.Equal(a, shell.Browse.CurrentFolder);
        shell.ShowIdCommand.Execute(null);
        Assert.Equal(b, shell.Id.Picker.Explorer.CurrentFolder);
        shell.ShowCollagesCommand.Execute(null);
        Assert.Equal(c, shell.Collage.Picker.Explorer.CurrentFolder);
        window.Close();

        (window, shell) = UiHelpers.CreateWindow(cfg, 1366, 768);                         // and after a restart
        await UiHelpers.WaitUntil(() => shell.Browse.CurrentFolder == a);
        shell.ShowIdCommand.Execute(null);
        Assert.Equal(b, shell.Id.Picker.Explorer.CurrentFolder);
        shell.ShowCollagesCommand.Execute(null);
        Assert.Equal(c, shell.Collage.Picker.Explorer.CurrentFolder);
        window.Close();
    }

    [AvaloniaFact]
    public async Task Id_photos_starts_with_a_select_photo_step_like_the_photos_gallery()
    {
        using var dir = new TempDir();
        var order = Order(dir, 2);
        var (window, shell) = UiHelpers.CreateWindow(Path.Combine(dir.Path, "cfg"), 1366, 768);
        await shell.Browse.OpenFolderAsync(order);
        var id = shell.Id;
        shell.ShowIdCommand.Execute(null);
        await UiHelpers.WaitUntil(() => id.Picker.Photos.Count == 2 && id.Picker.Photos.All(p => p.Thumbnail is not null));

        Assert.True(id.IsSelectStep);
        Assert.False(id.IsFraming || id.IsSheetStep);
        Assert.Equal(4, id.Picker.Items.Count);                                          // two folders, two photos
        Assert.False(FindIn<IdView, Border>(window, "RightPanel").IsVisible);             // the thumbnails get the room
        Assert.True(FindIn<IdView, PickerGrid>(window, "SelectGrid").IsEffectivelyVisible);
        UiHelpers.Capture(window, "p08-id-step1");

        id.Picker.SelectedPhoto = id.Picker.Photos[1];                                    // a click on a photo: step 2
        await UiHelpers.WaitUntil(() => id.ShowFraming && id.Preview is not null);
        Assert.False(id.IsSelectStep);
        Assert.True(FindIn<IdView, Border>(window, "RightPanel").IsVisible);
        Assert.EndsWith("P2.jpg", id.SourcePath);
        id.MoveBy(40, 0);
        var framing = id.Framing;
        UiHelpers.Capture(window, "p08-id-step2");

        id.ShowSelectStepCommand.Execute(null);                                           // back to step 1 …
        Assert.True(id.IsSelectStep);
        id.Picker.Pick(id.Picker.Photos[1]);                                              // … and the same photo continues as it was
        await UiHelpers.WaitUntil(() => id.ShowFraming);
        Assert.Equal(framing, id.Framing);

        id.ContinueToSheetCommand.Execute(null);                                          // step 3: the sheet
        Assert.True(id.ShowSheet);
        await UiHelpers.WaitUntil(() => id.SheetPreview is not null);
        UiHelpers.Capture(window, "p08-id-step3");

        id.ShowSelectStepCommand.Execute(null);
        id.Picker.Items.OfType<FolderItemViewModel>().First().Open();                     // folders open in step 1
        await UiHelpers.WaitUntil(() => id.Picker.Explorer.CurrentFolder == Path.Combine(order, "Album 2") && id.Picker.Photos.Count == 1);
        await id.ClearPhotoAsync();                                                       // Clear goes back to step 1
        Assert.True(id.IsSelectStep);
        window.Close();
    }

    [AvaloniaFact]
    public async Task Collage_thumbnails_and_settings_fold_away_and_come_back()
    {
        using var dir = new TempDir();
        var order = Order(dir, 4);
        var cfg = Path.Combine(dir.Path, "cfg");
        var (window, shell) = UiHelpers.CreateWindow(cfg, 1366, 768);
        await shell.Browse.OpenFolderAsync(order);
        shell.ShowCollagesCommand.Execute(null);
        var c = shell.Collage;
        await UiHelpers.WaitUntil(() => c.Picker.Photos.Count == 4 && c.Picker.Photos.All(p => p.Thumbnail is not null));
        var root = FindIn<CollageView, Grid>(window, "Root");
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        UiHelpers.Capture(window, "p08-collage");
        Assert.Equal(6, c.Picker.Items.Count);                                            // folders and photos, beside the tree
        var canvas = root.ColumnDefinitions[4].ActualWidth;
        var thumbs = root.ColumnDefinitions[2].ActualWidth;

        c.ToggleThumbsCommand.Execute(null);
        c.ToggleSettingsCommand.Execute(null);
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
        UiHelpers.Capture(window, "p08-collage-folded");
        Assert.True(FindIn<CollageView, Button>(window, "ThumbsStrip").IsVisible);
        Assert.True(FindIn<CollageView, Button>(window, "SettingsStrip").IsVisible);
        Assert.InRange(root.ColumnDefinitions[2].ActualWidth, 30, 50);                  // a strip with a clear button
        Assert.InRange(root.ColumnDefinitions[6].ActualWidth, 30, 50);
        Assert.True(root.ColumnDefinitions[4].ActualWidth > canvas + 400);               // the collage gets the room

        await c.PlaceAsync(c.Picker.Photos[0].Path);                                      // still works while folded
        Assert.True(c.HasPhotos);
        window.Close();

        (window, shell) = UiHelpers.CreateWindow(cfg, 1366, 768);                          // remembered
        Assert.True(shell.Collage.ThumbsCollapsed && shell.Collage.SettingsCollapsed);
        shell.ShowCollagesCommand.Execute(null);
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        shell.Collage.ToggleThumbsCommand.Execute(null);
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        Assert.Equal(thumbs, FindIn<CollageView, Grid>(window, "Root").ColumnDefinitions[2].Width.Value, 0);
        window.Close();
    }

    [AvaloniaFact]
    public async Task Deleting_photos_asks_first_moves_them_into_original_and_restore_brings_them_back()
    {
        using var dir = new TempDir();
        var order = Order(dir, 4);
        File.WriteAllText(Path.Combine(order, "notes.txt"), "customer notes");
        var hashes = Directory.GetFiles(order).ToDictionary(f => Path.GetFileName(f), Fixtures.Sha);
        var (window, shell) = UiHelpers.CreateWindow(Path.Combine(dir.Path, "cfg"), 1366, 768);
        var browse = shell.Browse;
        await browse.OpenFolderAsync(order);
        await UiHelpers.WaitUntil(() => browse.AllPhotos.All(p => p.Thumbnail is not null));
        var p1 = browse.AllPhotos[0];
        p1.PendingRecipe = new Core.Editing.EditRecipe { Exposure = 0.5 };                // an unsaved edit of another photo
        p1.State = PhotoState.Unsaved;
        var targets = browse.AllPhotos.Where(p => p.Name is "P2.jpg" or "P4.jpg").ToList();
        browse.SetSelection(targets);

        UiHelpers.Tap(window, Avalonia.Input.PhysicalKey.Delete);                        // the Delete key asks first
        await UiHelpers.WaitUntil(() => shell.Dialog is not null);
        Assert.Equal("Delete 2 photos?", shell.Dialog!.Title);
        Assert.Contains("P2.jpg, P4.jpg", shell.Dialog.Message);
        UiHelpers.Capture(window, "p08-delete-confirm");
        shell.Dialog.PressCommand.Execute("cancel");
        await UiHelpers.WaitUntil(() => shell.Dialog is null);
        Assert.Equal(4, browse.AllPhotos.Count);                                         // cancelled: nothing moved
        Assert.Equal(hashes, Directory.GetFiles(order).ToDictionary(f => Path.GetFileName(f), Fixtures.Sha));

        var delete = shell.DeletePhotosAsync(targets);
        await UiHelpers.WaitUntil(() => shell.Dialog is not null);
        shell.Dialog!.PressCommand.Execute("delete");
        await delete;

        Assert.Equal(["P1.jpg", "P3.jpg"], browse.AllPhotos.Select(p => p.Name));
        Assert.Equal(["P2.jpg", "P4.jpg"], browse.DeletedPhotos.Select(p => p.Name));
        Assert.Equal("2", browse.CountDeleted);
        Assert.Same(p1, browse.AllPhotos[0]);
        Assert.NotNull(p1.PendingRecipe);                                                 // other photos keep their unsaved edits
        Assert.Equal(hashes["P2.jpg"], Fixtures.Sha(Path.Combine(order, "Original", "P2 (deleted).jpg")));
        Assert.DoesNotContain(shell.Editor.Sequence, p => p.Name is "P2.jpg" or "P4.jpg"); // not edited or printed until restored

        browse.SetFilterCommand.Execute(PhotoFilter.Deleted);
        Assert.Equal(2, browse.GalleryItems.Count);                                       // only the deleted photos, no folders
        Assert.All(browse.VisiblePhotos, p => Assert.True(p.IsDeleted && !p.IsOpenable));
        await UiHelpers.WaitUntil(() => browse.VisiblePhotos.All(p => p.Thumbnail is not null));
        browse.SetSelection(browse.VisiblePhotos);
        UiHelpers.Capture(window, "p08-deleted-view");

        await shell.RestoreDeletedAsync(browse.VisiblePhotos.ToList());

        Assert.Empty(browse.DeletedPhotos);
        Assert.Equal(["P1.jpg", "P2.jpg", "P3.jpg", "P4.jpg"], browse.AllPhotos.Select(p => p.Name));
        Assert.Equal(hashes, Directory.GetFiles(order).ToDictionary(f => Path.GetFileName(f), Fixtures.Sha)); // every file as before
        Assert.False(Directory.Exists(Path.Combine(order, "Original")));
        window.Close();
    }

    [AvaloniaFact]
    public async Task A_folder_with_deleted_photos_shows_them_again_when_reopened()
    {
        using var dir = new TempDir();
        var order = Order(dir, 3);
        var cfg = Path.Combine(dir.Path, "cfg");
        PhotoTrash.Delete(order, ["P3.jpg"]);
        var (window, shell) = UiHelpers.CreateWindow(cfg, 1366, 768);
        await shell.Browse.OpenFolderAsync(order);

        Assert.Equal(["P1.jpg", "P2.jpg"], shell.Browse.AllPhotos.Select(p => p.Name));
        Assert.Equal("P3.jpg", Assert.Single(shell.Browse.DeletedPhotos).Name);
        shell.ShowIdCommand.Execute(null);                                                 // not offered elsewhere either
        await UiHelpers.WaitUntil(() => shell.Id.Picker.Photos.Count == 2);
        Assert.DoesNotContain(shell.Id.Picker.Photos, p => p.Name == "P3.jpg");
        window.Close();
    }

    [AvaloniaFact]
    public async Task Discard_originals_covers_the_whole_folder_and_names_the_deleted_photos()
    {
        using var dir = new TempDir();
        var order = Order(dir, 4);
        File.WriteAllText(Path.Combine(order, "notes.txt"), "customer notes");
        var saver = new PhotoSaver();
        foreach (var n in new[] { "P1.jpg", "P2.jpg", "P3.jpg" })
        {
            var path = Path.Combine(order, n);
            Assert.True(saver.Save(path, new Core.Editing.EditRecipe { Exposure = 0.4 }, FileStamp.Of(path)!.Value).Succeeded);
        }

        PhotoTrash.Delete(order, ["P3.jpg"]);
        var edited = new[] { "P1.jpg", "P2.jpg", "P4.jpg", "notes.txt" }.ToDictionary(n => n, n => Fixtures.Sha(Path.Combine(order, n)));
        var (window, shell) = UiHelpers.CreateWindow(Path.Combine(dir.Path, "cfg"), 1366, 768);
        await shell.Browse.OpenFolderAsync(order);
        shell.Browse.SetSelection([shell.Browse.AllPhotos[0]]);                             // one photo selected: the folder still counts

        shell.Browse.DiscardOriginalsCommand.Execute(null);
        await UiHelpers.WaitUntil(() => shell.Dialog is not null);
        var dialog = shell.Dialog!;
        Assert.Contains("whole folder", dialog.Message);
        Assert.Contains("2 originals", dialog.Message);
        Assert.Contains("1 photo marked Deleted", dialog.Message);
        Assert.Equal("Discard originals and deleted photos", dialog.Buttons[1].Text);
        Assert.True(dialog.Buttons[0].IsPrimary);                                           // Cancel is the highlighted choice
        UiHelpers.Capture(window, "p08-discard-confirm");
        dialog.PressCommand.Execute("discard");
        await UiHelpers.WaitUntil(() => shell.Dialog is { Buttons.Count: 1 });
        Assert.Contains("1 deleted photo was removed for good", shell.Dialog!.Message);
        shell.Dialog.PressCommand.Execute("ok");

        Assert.False(Directory.Exists(Path.Combine(order, "Original")));
        foreach (var (n, sha) in edited) Assert.Equal(sha, Fixtures.Sha(Path.Combine(order, n)));       // edited photos and other files stay
        Assert.True(Directory.Exists(Path.Combine(order, "Retouch")));
        Assert.Empty(shell.Browse.DeletedPhotos);
        Assert.Equal(3, shell.Browse.AllPhotos.Count);
        window.Close();
    }

    [AvaloniaFact]
    public async Task Opening_another_folder_with_unsaved_edits_asks_first()
    {
        using var dir = new TempDir();
        var order = Order(dir, 2);
        var other = Path.Combine(order, "Retouch");
        var (window, shell) = UiHelpers.CreateWindow(Path.Combine(dir.Path, "cfg"), 1366, 768);
        var browse = shell.Browse;
        await browse.OpenFolderAsync(order);
        browse.AllPhotos[0].PendingRecipe = new Core.Editing.EditRecipe { Exposure = 0.5 };

        browse.Explorer.Navigate(other);                                                   // a click in the tree or on a folder card
        await UiHelpers.WaitUntil(() => shell.Dialog is not null);
        Assert.Contains("P1.jpg", shell.Dialog!.Message);
        shell.Dialog.PressCommand.Execute("cancel");
        await UiHelpers.WaitUntil(() => shell.Dialog is null);
        Assert.Equal(order, browse.CurrentFolder);                                         // stays, with the edit
        Assert.Equal(order, browse.Explorer.CurrentFolder);
        Assert.NotNull(browse.AllPhotos[0].PendingRecipe);

        browse.Explorer.Navigate(other);
        await UiHelpers.WaitUntil(() => shell.Dialog is not null);
        shell.Dialog!.PressCommand.Execute("discard");
        await UiHelpers.WaitUntil(() => browse.CurrentFolder == other && !browse.IsScanning);
        Assert.Single(browse.AllPhotos);
        window.Close();
    }

    [AvaloniaTheory]
    [InlineData("en", "Version", "Photo preparation, made simple.", "Third-party licences")]
    [InlineData("ro", "Versiunea", "Pregătirea fotografiilor, simplu.", "Licențe terțe")]
    public async Task About_shows_this_builds_version_and_the_third_party_licences(string language, string version, string tagline, string licences)
    {
        using var dir = new TempDir();
        var (window, shell) = UiHelpers.CreateWindow(Path.Combine(dir.Path, "cfg"), 1366, 768, language);
        shell.SetLanguageCommand.Execute(language);
        string? opened = null;
        shell.OpenUrlRequested += url => opened = url;
        shell.Settings = shell.CreateSettings(updatesInstall: false);
        var settings = shell.Settings;
        var build = typeof(ShellViewModel).Assembly.GetName().Version!;

        Assert.Equal($"{version} {build.Major}.{build.Minor}.{build.Build}", settings.AboutVersionText);   // from the build, not a fixed text
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        var texts = window.GetVisualDescendants().OfType<TextBlock>().Where(t => t.IsEffectivelyVisible).Select(t => t.Text).ToList();
        Assert.Contains(tagline, texts);
        Assert.DoesNotContain(texts, t => t?.Contains("THIRD-PARTY-NOTICES", StringComparison.Ordinal) == true);
        var link = window.GetVisualDescendants().OfType<Button>().Single(b => Equals(b.Content, licences));
        window.GetVisualDescendants().OfType<ScrollViewer>().First(v => v.IsEffectivelyVisible && v.FindAncestorOfType<Border>()?.DataContext is SettingsViewModel)
            .ScrollToEnd();
        UiHelpers.Capture(window, $"p08-about-{language}");

        link.Command!.Execute(null);                                                          // the licences open inside Settings
        Assert.True(settings.ShowLicences);
        Assert.True(settings.Components.Count >= 10);
        var magick = Assert.Single(settings.Components, c => c.Name.StartsWith("Magick.NET", StringComparison.Ordinal));
        Assert.Contains("Apache-2.0", magick.Detail);
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        UiHelpers.Capture(window, $"p08-licences-{language}");
        settings.OpenSourceCommand.Execute(magick);
        Assert.Equal("https://github.com/dlemstra/Magick.NET", opened);
        settings.ToggleLicencesCommand.Execute(null);
        Assert.False(settings.ShowLicences);
        shell.Settings = null;
        window.Close();
        await Task.CompletedTask;
    }

    [AvaloniaFact]
    public async Task Check_for_updates_downloads_the_verified_installer_then_starts_it_and_closes()
    {
        using var dir = new TempDir();
        var installer = new byte[200_000];
        new Random(3).NextBytes(installer);
        var github = new FakeGitHub().Release("9.0.0", installer);
        var (window, shell) = UiHelpers.CreateWindow(Path.Combine(dir.Path, "cfg"), 1366, 768);
        AppServices().UpdateHandler = () => github;
        AppServices().UpdateDownloadDir = Path.Combine(dir.Path, "update");
        string? started = null;
        shell.StartInstaller = path =>
        {
            started = path;
            return true;
        };
        var closed = false;
        window.Closed += (_, _) => closed = true;

        shell.Settings = shell.CreateSettings(updatesInstall: true);
        var settings = shell.Settings;
        Assert.Equal($"Installed version: PhotoX {shell.Version}", settings.InstalledVersionText);
        await settings.CheckUpdatesCommand.ExecuteAsync(null);
        Assert.Equal($"PhotoX 9.0.0 is available (installed: {shell.Version}).", settings.UpdateStatus);
        Assert.True(settings.CanInstall);
        UiHelpers.Capture(window, "p08-update-available");

        var install = settings.InstallUpdateCommand.ExecuteAsync(null);
        await UiHelpers.WaitUntil(() => shell.Dialog is not null);
        Assert.Equal("Update to PhotoX 9.0.0?", shell.Dialog!.Title);
        UiHelpers.Capture(window, "p08-update-confirm");
        shell.Dialog.PressCommand.Execute("update");
        await install;

        Assert.NotNull(started);
        Assert.Equal(Convert.ToHexString(SHA256.HashData(installer)), Fixtures.Sha(started!));
        Assert.Equal(Path.Combine(dir.Path, "update", "PhotoX-9.0.0-win-Setup.exe"), started);
        Assert.True(closed);                                                                 // PhotoX closed for the installer
    }

    [AvaloniaFact]
    public async Task An_update_waits_for_unsaved_edits_and_linux_opens_the_release_page()
    {
        using var dir = new TempDir();
        var order = Order(dir, 1);
        var github = new FakeGitHub().Release("9.0.0", new byte[1000]);
        var (window, shell) = UiHelpers.CreateWindow(Path.Combine(dir.Path, "cfg"), 1366, 768);
        AppServices().UpdateHandler = () => github;
        AppServices().UpdateDownloadDir = Path.Combine(dir.Path, "update");
        await shell.Browse.OpenFolderAsync(order);
        shell.Browse.AllPhotos[0].PendingRecipe = new Core.Editing.EditRecipe { Exposure = 0.5 };
        var started = false;
        shell.StartInstaller = _ => started = true;

        shell.Settings = shell.CreateSettings(updatesInstall: true);
        await shell.Settings.CheckUpdatesCommand.ExecuteAsync(null);
        var install = shell.Settings.InstallUpdateCommand.ExecuteAsync(null);
        await UiHelpers.WaitUntil(() => shell.Dialog is not null);
        shell.Dialog!.PressCommand.Execute("update");
        await UiHelpers.WaitUntil(() => shell.Dialog is { Title: "Unsaved changes" });     // asked before closing
        shell.Dialog!.PressCommand.Execute("cancel");
        await install;
        Assert.False(started);
        Assert.Equal("The update was not started. PhotoX keeps running.", shell.Settings.UpdateStatus);
        Assert.NotNull(shell.Browse.AllPhotos[0].PendingRecipe);

        string? opened = null;
        shell.OpenUrlRequested += url => opened = url;
        shell.Settings = shell.CreateSettings(updatesInstall: false);                        // Linux: a link to the release
        await shell.Settings.CheckUpdatesCommand.ExecuteAsync(null);
        Assert.False(shell.Settings.CanInstall);
        Assert.True(shell.Settings.CanOpenPage);
        shell.Settings.OpenReleasePageCommand.Execute(null);
        Assert.Equal("https://github.com/NorbCraft/PhotoX/releases/tag/v9.0.0", opened);

        github.Offline = true;
        await shell.Settings.CheckUpdatesCommand.ExecuteAsync(null);
        Assert.True(shell.Settings.UpdateStatusIsWarning);
        Assert.Contains("could not be reached", shell.Settings.UpdateStatus);
        shell.Settings = null;
        window.Close();
    }
}
