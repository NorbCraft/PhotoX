using Avalonia.Headless.XUnit;
using ImageMagick;
using ImageMagick.Drawing;
using PhotoX.App.ViewModels;
using PhotoX.Core.Id;
using PhotoX.Tests;

namespace PhotoX.App.Tests;

/// <summary>TS-14 / TS-15 through the UI: framing, sheet, export names, source untouched.</summary>
public class IdWorkflowTests
{
    private static string SyntheticPortrait(string dir)
    {
        var path = Path.Combine(dir, "portrait.jpg");
        using var img = new MagickImage(new MagickColor("#C9D1D8"), 1200, 1600);
        new Drawables()
            .FillColor(new MagickColor("#2F3E52")).Ellipse(600, 1600, 520, 360, 0, 360)          // shoulders
            .FillColor(new MagickColor("#E4B696")).Ellipse(600, 700, 230, 300, 0, 360)           // face
            .FillColor(new MagickColor("#3E2C22")).Ellipse(600, 470, 250, 150, 180, 360)          // hair
            .FillColor(new MagickColor("#4B3A2E")).Ellipse(520, 680, 22, 14, 0, 360).Ellipse(680, 680, 22, 14, 0, 360)
            .Draw(img);
        img.Write(path, MagickFormat.Jpeg);
        return path;
    }

    [AvaloniaFact]
    public async Task Frame_portrait_and_save_3x4_sheet_with_timestamp_names()
    {
        using var dir = new TempDir();
        var portrait = SyntheticPortrait(dir.Path);
        var hash = Fixtures.Sha(portrait);
        var (window, shell) = UiHelpers.CreateWindow(Path.Combine(dir.Path, "cfg"));
        shell.ShowIdCommand.Execute(null);
        var id = shell.Id;
        await id.LoadSourceAsync(portrait);
        await UiHelpers.WaitUntil(() => id.Preview is not null);
        UiHelpers.Capture(window, "id-framing-1366x768");

        id.SelectSizeCommand.Execute(id.Sizes.Single(s => s.Size.Id == "3x4"));
        Assert.Equal(8, id.Copies); // familiar count, below the capacity of 9
        Assert.Equal(9, id.Capacity);
        id.MoveBy(10, -20);
        id.ZoomBy(1.1);
        id.BackgroundBrightness = 40;
        id.ContinueToSheetCommand.Execute(null);
        await UiHelpers.WaitUntil(() => id.SheetPreview is not null);
        UiHelpers.Capture(window, "id-sheet-1366x768");

        id.OutputFolder = Path.Combine(dir.Path, "ID out");
        var first = await id.SaveSheetAsync();
        var second = await id.SaveSheetAsync();
        Assert.NotNull(first);
        Assert.NotNull(second);
        Assert.Matches(@"^ID_\d{8}_\d{4}\.jpg$", Path.GetFileName(first!));
        Assert.Matches(@"^ID_\d{8}_\d{4}(_1)?\.jpg$", Path.GetFileName(second!));
        Assert.NotEqual(first, second);
        using (var sheet = new MagickImage(first!))
        {
            Assert.Equal((1200u, 1800u), (sheet.Width, sheet.Height));
            Assert.Equal(300, sheet.Density.X, 1);
        }

        Assert.Equal(hash, Fixtures.Sha(portrait)); // the portrait is never modified
        window.Close();
    }

    [AvaloniaFact]
    public async Task Owner_sample_frames_and_exports_on_a_copy()
    {
        var sample = SampleTests.SamplePath();
        Assert.SkipWhen(sample is null, "Owner sample not available");
        var hash = Fixtures.Sha(sample!);
        using var dir = new TempDir();
        var copy = Path.Combine(dir.Path, "IMG_6153.JPG");
        File.Copy(sample!, copy);
        var (window, shell) = UiHelpers.CreateWindow(Path.Combine(dir.Path, "cfg"), 1920, 1080, "ro");
        shell.ShowIdCommand.Execute(null);
        await shell.Id.LoadSourceAsync(copy);
        await UiHelpers.WaitUntil(() => shell.Id.Preview is not null);
        UiHelpers.Capture(window, "id-sample-framing-ro-1920x1080");
        shell.Id.ContinueToSheetCommand.Execute(null);
        await UiHelpers.WaitUntil(() => shell.Id.SheetPreview is not null);
        UiHelpers.Capture(window, "id-sample-sheet-ro-1920x1080");
        shell.Id.OutputFolder = Path.Combine(dir.Path, "out");
        Assert.NotNull(await shell.Id.SaveSheetAsync());
        Assert.Equal(hash, Fixtures.Sha(sample!));
        window.Close();
    }

    [AvaloniaFact]
    public async Task Use_for_id_photo_from_the_gallery()
    {
        using var dir = new TempDir();
        var order = Path.Combine(dir.Path, "order");
        Directory.CreateDirectory(order);
        File.Copy(SyntheticPortrait(dir.Path), Path.Combine(order, "p.jpg"));
        var (window, shell) = UiHelpers.CreateWindow(Path.Combine(dir.Path, "cfg"));
        shell.Browse.OpenFolder(order);
        shell.Browse.UseForIdCommand.Execute(null);
        await UiHelpers.WaitUntil(() => shell.CurrentPage is IdViewModel && shell.Id.Preview is not null);
        Assert.Equal("p.jpg", shell.Id.SourceName);
        window.Close();
    }
}

/// <summary>TS-13 through the UI: Auto Align on the owner's sample (copy), several faces, no face.</summary>
public class AutoAlignUiTests
{
    [AvaloniaFact]
    public async Task Auto_align_sample_then_choose_between_two_people()
    {
        var sample = SampleTests.SamplePath();
        Assert.SkipWhen(sample is null, "Owner sample not available");
        using var dir = new TempDir();
        var copy = Path.Combine(dir.Path, "s.jpg");
        File.Copy(sample!, copy);
        var (window, shell) = UiHelpers.CreateWindow(Path.Combine(dir.Path, "cfg"));
        shell.ShowIdCommand.Execute(null);
        var id = shell.Id;
        await id.LoadSourceAsync(copy);
        var before = id.Framing;
        await id.AutoAlignCommand.ExecuteAsync(null);
        Assert.NotEqual(before, id.Framing);
        Assert.False(id.HasFaceChoices);
        await UiHelpers.WaitUntil(() => id.Preview is not null);
        await Task.Delay(300);
        UiHelpers.Capture(window, "id-sample-auto-align");
        id.UndoCommand.Execute(null);
        Assert.Equal(before, id.Framing); // undoable

        // Two people side by side: the operator chooses.
        var two = Path.Combine(dir.Path, "two.jpg");
        using (var one = new MagickImage(copy))
        {
            one.Resize(new MagickGeometry(900, 900));
            using var both = new MagickImage(MagickColors.White, one.Width * 2, one.Height);
            both.Composite(one, 0, 0);
            both.Composite(one, (int)one.Width, 0);
            both.Write(two, MagickFormat.Jpeg);
        }

        await id.LoadSourceAsync(two);
        await id.AutoAlignCommand.ExecuteAsync(null);
        Assert.Equal(2, id.FaceChoices.Count);
        var start = id.Framing;
        id.ChooseFaceCommand.Execute(id.FaceChoices[1]);
        Assert.False(id.HasFaceChoices);
        Assert.True(id.Framing!.CenterX > start!.CenterX); // the right-hand person
        window.Close();
    }

    [AvaloniaFact]
    public async Task Auto_align_without_a_face_explains_and_keeps_framing()
    {
        using var dir = new TempDir();
        var path = Path.Combine(dir.Path, "plain.jpg");
        Fixtures.WriteJpeg(dir.Path, "plain.jpg", 800, 1000);
        var (window, shell) = UiHelpers.CreateWindow(Path.Combine(dir.Path, "cfg"));
        shell.ShowIdCommand.Execute(null);
        await shell.Id.LoadSourceAsync(path);
        var before = shell.Id.Framing;
        await shell.Id.AutoAlignCommand.ExecuteAsync(null);
        Assert.Equal(before, shell.Id.Framing);
        Assert.Equal(Localization.Localizer.Instance["AutoAlignNoFace"], shell.Id.ErrorMessage);
        window.Close();
    }
}
