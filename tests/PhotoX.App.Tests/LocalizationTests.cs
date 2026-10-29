using System.Text.RegularExpressions;
using Avalonia.Headless.XUnit;
using PhotoX.App.Localization;
using PhotoX.Tests;

namespace PhotoX.App.Tests;

/// <summary>TS-16: key parity, Romanian diacritics, live switching.</summary>
public class LocalizationTests
{
    [Fact]
    public void English_and_romanian_have_the_same_keys()
    {
        var en = Localizer.Instance.Keys("en").ToHashSet();
        var ro = Localizer.Instance.Keys("ro").ToHashSet();
        Assert.Empty(en.Except(ro));
        Assert.Empty(ro.Except(en));
    }

    [Fact]
    public void Romanian_uses_comma_below_diacritics_only()
    {
        Localizer.Instance.SetLanguage("ro");
        try
        {
            foreach (var key in Localizer.Instance.Keys("ro"))
            {
                Assert.DoesNotMatch(new Regex("[şţŞŢ]"), Localizer.Instance[key]);
            }
        }
        finally
        {
            Localizer.Instance.SetLanguage("en");
        }
    }

    [Theory]
    [InlineData(1, "A rămas 1 fotografie")]
    [InlineData(2, "Au rămas 2 fotografii")]
    [InlineData(19, "Au rămas 19 fotografii")]
    [InlineData(20, "Au rămas 20 de fotografii")]
    [InlineData(101, "Au rămas 101 fotografii")]
    [InlineData(120, "Au rămas 120 de fotografii")]
    public void Romanian_plurals(int count, string expected)
    {
        Localizer.Instance.SetLanguage("ro");
        try
        {
            Assert.Equal(expected, Localizer.Instance.Plural("PhotosLeft", count));
        }
        finally
        {
            Localizer.Instance.SetLanguage("en");
        }
    }

    [AvaloniaFact]
    public async Task Language_switch_updates_the_interface_live()
    {
        using var dir = new TempDir();
        var order = Path.Combine(dir.Path, "order");
        Directory.CreateDirectory(order);
        Fixtures.WriteJpeg(order, "a.jpg", 600, 400);
        var (window, shell) = UiHelpers.CreateWindow(Path.Combine(dir.Path, "cfg"), 1920, 1080);
        shell.Browse.OpenFolder(order);
        await UiHelpers.WaitUntil(() => shell.Browse.AllPhotos.All(p => p.Thumbnail is not null));
        Assert.Contains(Texts(window), t => t == "ID Photos");
        shell.SetLanguageCommand.Execute("ro");
        UiHelpers.Capture(window, "browse-ro-1920x1080");
        var texts = Texts(window);
        Assert.Contains("Foto acte", texts);
        Assert.Contains("Începe editarea", texts);
        Assert.DoesNotContain("ID Photos", texts);
        Assert.Equal("ro", Localizer.Instance.Language);
        Assert.Contains("din", shell.Browse.ProgressText);
        shell.Browse.StartEditingCommand.Execute(null);
        await UiHelpers.WaitUntil(() => shell.Editor is { Preview: not null, IsLoading: false });
        UiHelpers.Capture(window, "editor-ro-1920x1080");
        shell.SetLanguageCommand.Execute("en");
        Assert.Contains("of", shell.Browse.ProgressText);
        Assert.Contains("Save & Next", Texts(window));
        window.Close();
    }

    private static List<string> Texts(Avalonia.Controls.Window window)
    {
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        return Avalonia.VisualTree.VisualExtensions.GetVisualDescendants(window)
            .OfType<Avalonia.Controls.TextBlock>().Select(t => t.Text ?? string.Empty)
            .Concat(Avalonia.VisualTree.VisualExtensions.GetVisualDescendants(window).OfType<Avalonia.Controls.ContentControl>().Select(c => c.Content as string ?? string.Empty))
            .ToList();
    }
}

/// <summary>TS-18: layouts at small logical sizes (e.g. 1920×1080 at 150 % scaling = 1280×720).</summary>
public class ScalingTests
{
    [AvaloniaFact]
    public async Task Screens_fit_at_1280x720_logical()
    {
        using var dir = new TempDir();
        var order = Path.Combine(dir.Path, "order");
        Directory.CreateDirectory(order);
        Fixtures.WriteJpeg(order, "a.jpg", 800, 600);
        var (window, shell) = UiHelpers.CreateWindow(Path.Combine(dir.Path, "cfg"), 1280, 720, "ro");
        shell.Browse.OpenFolder(order);
        await UiHelpers.WaitUntil(() => shell.Browse.AllPhotos.All(p => p.Thumbnail is not null));
        UiHelpers.Capture(window, "small-browse-ro-1280x720");
        shell.Browse.StartEditingCommand.Execute(null);
        await UiHelpers.WaitUntil(() => shell.Editor is { Preview: not null, IsLoading: false });
        UiHelpers.Capture(window, "small-editor-ro-1280x720");
        shell.ShowIdCommand.Execute(null);
        await shell.Id.LoadSourceAsync(Path.Combine(order, "a.jpg"));
        await UiHelpers.WaitUntil(() => shell.Id.Preview is not null);
        UiHelpers.Capture(window, "small-id-ro-1280x720");
        window.Close();
    }
}
