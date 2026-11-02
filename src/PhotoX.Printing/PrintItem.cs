using ImageMagick;
using PhotoX.Core.Editing;
using PhotoX.Core.Formats;
using PhotoX.Core.Printing;
using PhotoX.Imaging;
using PhotoX.Storage;

namespace PhotoX.Printing;

/// <summary>Something to print, with the number of copies.</summary>
public abstract record PrintItem(string Name, int Copies)
{
    /// <summary>The gallery photo whose print counter this job updates (prompt_05 §6); none for ID sheets and collages.</summary>
    public virtual string? CounterPath => null;
}

/// <summary>
/// An order photo (prompt_05 §3): the unsaved edit shown in PhotoX when there is one (rendered like a save, from the
/// preserved original, but nothing is written), else the photo as saved, else the untouched original. An edit keeps
/// its own crop; an untouched photo is cropped to fill the paper, or with <paramref name="Whole"/> shown whole with
/// white borders (prompt_07 §3.5).
/// </summary>
public sealed record PhotoPrintItem(string Name, string Path, EditRecipe? PendingRecipe, bool IsEdited, int Copies, bool Whole = false) : PrintItem(Name, Copies)
{
    public override string? CounterPath => Path;
}

/// <summary>A finished page (ID sheet, collage), rendered on demand: <c>true</c> asks for a quick preview rendering.</summary>
public sealed record ImagePrintItem(string Name, Func<bool, MagickImage> Render, int Copies) : PrintItem(Name, Copies);

/// <summary>A page made for the chosen print size, such as the calibration sheet of 10×15 or 15×20 (FR-071).</summary>
public sealed record SizedPrintItem(string Name, Func<bool, PrintFormat, MagickImage> Render, int Copies) : PrintItem(Name, Copies);

/// <summary>Produces the image to print for an item. Read-only: printing never writes to the photo or its folder.</summary>
public static class PrintContent
{
    /// <summary>Long edge of the source used for previews.</summary>
    public const int PreviewEdge = 1000;

    /// <summary>
    /// Returns the content and how it goes on the page (prompt_11 §1): an untouched photo fills the page with its bleed
    /// (Crop) or is shown whole; an edit without borders is borderless; canvases, borders, ID sheets, collages and the
    /// calibration sheet keep their true size.
    /// </summary>
    /// <param name="size">The print size, for items made for it (<see cref="SizedPrintItem"/>).</param>
    /// <param name="previewEdge">Long edge of a photo loaded for a preview; smaller for the small pages of a long list.</param>
    public static (MagickImage Image, PrintFit Fit) Load(PrintItem item, bool preview, PrintFormat? size = null, int previewEdge = PreviewEdge)
    {
        switch (item)
        {
            case PhotoPrintItem photo:
                var maxEdge = preview ? previewEdge : (int?)null;
                if (photo.PendingRecipe is { } recipe)
                {
                    using var source = ImageLoader.Load(SourceFor(photo.Path), maxEdge);
                    return (RecipeRenderer.RenderFinal(source, recipe), FitFor(recipe));
                }

                var fit = !photo.IsEdited ? (photo.Whole ? PrintFit.Fit : PrintFit.Crop) : FitFor(SavedRecipe(photo.Path));
                return (ImageLoader.Load(photo.Path, maxEdge), fit);
            case ImagePrintItem image:
                return (image.Render(preview), PrintFit.Fit);
            case SizedPrintItem sized:
                return (sized.Render(preview, size ?? PrintFormats.P10x15), PrintFit.Fit);
            default:
                throw new ArgumentException("Unknown print item.", nameof(item));
        }
    }

    /// <summary>
    /// An edit keeps its true size when it has borders by design: the whole photo with borders, or a format on a
    /// larger white canvas (9×13 on 10×15, 13×18 on 15×20, custom sizes). Otherwise it prints borderless.
    /// </summary>
    public static PrintFit FitFor(EditRecipe? recipe) =>
        recipe is { Composition: Composition.WholeWithBorder } || recipe?.Format is { HasCanvas: true } ? PrintFit.Fit : PrintFit.Borderless;

    /// <summary>The recipe PhotoX saved the photo with, from the order sidecar (null when there is none).</summary>
    private static EditRecipe? SavedRecipe(string path)
    {
        var store = new OrderStore(System.IO.Path.GetDirectoryName(path)!);
        return store.Load().Find(System.IO.Path.GetFileName(path))?.Recipe;
    }

    /// <summary>Where an unsaved edit starts from: the preserved original when PhotoX made one, like saving (FR-026).</summary>
    private static string SourceFor(string path)
    {
        var stamp = FileStamp.Of(path) ?? throw new ImageLoadException(ImageError.Unreadable, "File missing");
        var store = new OrderStore(System.IO.Path.GetDirectoryName(path)!);
        return SourceSelector.TrustedBackup(store, store.Load(), System.IO.Path.GetFileName(path), stamp) ?? path;
    }
}
