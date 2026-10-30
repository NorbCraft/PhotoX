using PhotoX.Core.Editing;
using PhotoX.Core.Orders;
using PhotoX.Imaging;

namespace PhotoX.Storage;

/// <summary>
/// Saves an ordinary photo in place (FR-004, FR-005, FR-021–FR-024, DEC-025):
/// render from the preserved original (or the file), encode to a temp file in the same folder, check it,
/// copy the original to <c>Original/</c> on the first save, record it in the sidecar, then atomically replace the photo.
/// If anything fails the existing photo and backup are unchanged.
/// </summary>
public sealed class PhotoSaver
{
    private readonly int _jpegQuality;

    public PhotoSaver(int jpegQuality = 95) => _jpegQuality = jpegQuality;

    /// <summary>Hook for tests to simulate a failure just before the photo is replaced.</summary>
    internal Action<string>? BeforeReplace { get; set; }

    public SaveResult Save(string photoPath, EditRecipe recipe, FileStamp loadedStamp)
    {
        var folder = Path.GetDirectoryName(photoPath)!;
        var name = Path.GetFileName(photoPath);
        var store = new OrderStore(folder);

        // 1. Checks (FR-023).
        var now = FileStamp.Of(photoPath);
        if (now is null) return SaveResult.Fail(SaveError.FileMissing);
        if (now.Value != loadedStamp) return SaveResult.Fail(SaveError.ChangedExternally);
        if ((File.GetAttributes(photoPath) & FileAttributes.ReadOnly) != 0) return SaveResult.Fail(SaveError.ReadOnlyFile);
        if (!HasFreeSpace(folder, now.Value.Length * 3 + 20_000_000)) return SaveResult.Fail(SaveError.DiskFull);

        OrderSidecar sidecar;
        try
        {
            sidecar = store.Load();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return SaveResult.Fail(SaveError.AccessDenied, ex.Message);
        }

        var record = sidecar.Find(name);
        var backupPath = SourceSelector.TrustedBackup(store, sidecar, name, now.Value);
        var sourcePath = backupPath ?? photoPath;
        var kind = FormatSniffer.Detect(photoPath);
        var temp = SaveNames.TempFor(photoPath);

        try
        {
            using var source = ImageLoader.Load(sourcePath);
            var plan = RenderPlan.Create((int)source.Width, (int)source.Height, recipe);
            var unchanged = plan.IsGeometryIdentity((int)source.Width, (int)source.Height) && !recipe.HasToneAdjustments;

            if (unchanged && backupPath is null)
            {
                // FR-024: effectively unchanged and never edited: mark reviewed, write nothing else.
                sidecar.Photos[name] = new PhotoRecord { State = SavedState.Reviewed, Recipe = recipe, SavedUtc = DateTime.UtcNow, OutputLength = now.Value.Length, OutputWriteTimeUtc = now.Value.LastWriteUtc };
                store.Save(sidecar);
                return new SaveResult(SaveOutcome.Reviewed, NewStamp: now);
            }

            // 2. Produce the new file content in a temp file next to the photo.
            if (unchanged)
            {
                // Re-edited back to the original: restore the original bytes instead of re-encoding.
                File.Copy(backupPath!, temp, overwrite: true);
            }
            else
            {
                using var rendered = RecipeRenderer.RenderFinal(source, recipe);
                var format = recipe.Format;
                double? ppi = format is null ? null : Math.Max(rendered.Width, rendered.Height) / format.PrintedLongEdgeInches;
                var bytes = ImageEncoder.Encode(rendered, kind == ImageKind.Png ? ImageKind.Png : ImageKind.Jpeg, new OutputOptions(_jpegQuality, ppi));
                WriteDurably(temp, bytes);
                if (!ImageVerifier.Verify(temp, kind == ImageKind.Png ? ImageKind.Png : ImageKind.Jpeg, (int)rendered.Width, (int)rendered.Height))
                {
                    TryDelete(temp);
                    return SaveResult.Fail(SaveError.VerifyFailed);
                }
            }

            // 3. First save: preserve the untouched original under a collision-safe name (FR-005, FR-021).
            var backupName = backupPath is not null ? Path.GetFileName(backupPath) : null;
            long? backupLength = record?.BackupLength;
            DateTime? backupWriteTime = record?.BackupWriteTimeUtc;
            string? createdBackup = null;
            if (backupName is null)
            {
                Directory.CreateDirectory(store.OriginalDir);
                backupName = store.ChooseBackupName(name);
                var backupTemp = Path.Combine(store.OriginalDir, "." + backupName + SaveNames.TempSuffix);
                File.Copy(photoPath, backupTemp, overwrite: true);
                File.SetLastWriteTimeUtc(backupTemp, now.Value.LastWriteUtc);
                createdBackup = Path.Combine(store.OriginalDir, backupName);
                File.Move(backupTemp, createdBackup, overwrite: false);
                ClearHidden(createdBackup);
                var info = new FileInfo(createdBackup);
                backupLength = info.Length;
                backupWriteTime = info.LastWriteTimeUtc;
            }

            // 4. Record the edit before the photo changes, so the original is always known.
            var tempInfo = new FileInfo(temp);
            var previous = record;
            sidecar.Photos[name] = new PhotoRecord
            {
                Backup = backupName,
                BackupLength = backupLength,
                BackupWriteTimeUtc = backupWriteTime,
                State = unchanged ? SavedState.Reviewed : SavedState.Edited,
                Recipe = recipe,
                SavedUtc = DateTime.UtcNow,
                OutputLength = tempInfo.Length,
                OutputWriteTimeUtc = tempInfo.LastWriteTimeUtc,
            };
            store.Save(sidecar);

            // 5. Atomic replace of the photo.
            try
            {
                BeforeReplace?.Invoke(photoPath);
                File.Move(temp, photoPath, overwrite: true);
                ClearHidden(photoPath);
            }
            catch
            {
                if (previous is null) sidecar.Photos.Remove(name); else sidecar.Photos[name] = previous;
                TrySave(store, sidecar);
                if (createdBackup is not null) TryDelete(createdBackup);
                throw;
            }

            return new SaveResult(unchanged ? SaveOutcome.Reviewed : SaveOutcome.Saved, NewStamp: FileStamp.Of(photoPath));
        }
        catch (ImageLoadException ex)
        {
            TryDelete(temp);
            return SaveResult.Fail(SaveError.SourceUnreadable, ex.Message, ex.Error);
        }
        catch (UnauthorizedAccessException ex)
        {
            TryDelete(temp);
            return SaveResult.Fail(SaveError.AccessDenied, ex.Message);
        }
        catch (IOException ex)
        {
            TryDelete(temp);
            return SaveResult.Fail(IsDiskFull(ex) ? SaveError.DiskFull : SaveError.WriteFailed, ex.Message);
        }
        catch (ImageMagick.MagickException ex)
        {
            TryDelete(temp);
            return SaveResult.Fail(ex.Message.Contains("space", StringComparison.OrdinalIgnoreCase) ? SaveError.DiskFull : SaveError.WriteFailed, ex.Message);
        }
    }

    private static void WriteDurably(string path, byte[] bytes)
    {
        using var fs = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None, 1 << 16, FileOptions.WriteThrough);
        fs.Write(bytes);
        fs.Flush(true);
    }

    /// <summary>
    /// A rename keeps the attributes of the moved file. PhotoX 0.4.0 marked its temp files Hidden on Windows, so
    /// saved photos became hidden and seemed to vanish (prompt_03 §6); make sure a written photo is never hidden.
    /// </summary>
    internal static void ClearHidden(string path)
    {
        var attributes = File.GetAttributes(path);
        if ((attributes & FileAttributes.Hidden) != 0) File.SetAttributes(path, attributes & ~FileAttributes.Hidden);
    }

    internal static bool HasFreeSpace(string folder, long needed)
    {
        try
        {
            var full = Path.GetFullPath(folder);
            var drive = DriveInfo.GetDrives()
                .Where(d => d.IsReady && full.StartsWith(d.RootDirectory.FullName, OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal))
                .OrderByDescending(d => d.RootDirectory.FullName.Length)
                .FirstOrDefault();
            return drive is null || drive.AvailableFreeSpace >= needed;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return true;
        }
    }

    private static bool IsDiskFull(IOException ex) =>
        (ex.HResult & 0xFFFF) is 0x70 or 0x27 || ex.HResult == 28 || ex.Message.Contains("space", StringComparison.OrdinalIgnoreCase);

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path)) File.Delete(path);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private static void TrySave(OrderStore store, OrderSidecar sidecar)
    {
        try
        {
            store.Save(sidecar);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }
    }
}
