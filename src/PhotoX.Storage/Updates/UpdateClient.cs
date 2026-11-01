using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace PhotoX.Storage.Updates;

/// <summary>A stable PhotoX release on GitHub with its Windows installer and the installer's published checksum.</summary>
public sealed record ReleaseInfo(Version Version, string Tag, string PageUrl, ReleaseAsset? Installer, ReleaseAsset? Checksum);

public sealed record ReleaseAsset(string Name, string DownloadUrl, long Size, string? Digest);

public enum UpdateCheckStatus
{
    /// <summary>The installed version is the latest published one (or newer).</summary>
    UpToDate,

    /// <summary>A newer stable version is published.</summary>
    Available,

    /// <summary>No stable version has been published on GitHub yet.</summary>
    NoRelease,

    /// <summary>GitHub could not be reached (offline, timeout, refused).</summary>
    Offline,

    /// <summary>GitHub answered, but not as expected.</summary>
    Failed,
}

public sealed record UpdateCheck(UpdateCheckStatus Status, ReleaseInfo? Release = null, string? Detail = null);

/// <summary>Why a downloaded installer was not kept.</summary>
public sealed class UpdateDownloadException(string message, bool integrity = false) : Exception(message)
{
    /// <summary>The file arrived but is not the published one (size or checksum differ).</summary>
    public bool Integrity { get; } = integrity;
}

/// <summary>
/// Check for Updates (prompt_08 §6): asks the public GitHub Releases API for the latest stable release of the
/// configured repository (no token; drafts and pre-releases are never returned by "latest"), compares it with the
/// installed version and downloads only the expected installer asset from that repository's release downloads. The
/// installer is written to a temp file, checked against its size and its published SHA-256 checksum and only then
/// given its final name, so an incomplete or altered download is never offered for installation.
/// </summary>
public sealed class UpdateClient(HttpClient http, string repository = UpdateClient.Repository)
{
    /// <summary>The PhotoX repository on GitHub (public since prompt_08).</summary>
    public const string Repository = "NorbCraft/PhotoX";

    /// <summary>The installer's file name for a version, as the release workflow publishes it.</summary>
    public static string InstallerName(Version version) => $"PhotoX-{Format(version)}-win-Setup.exe";

    /// <summary>The checksum file published next to the installer: "&lt;sha256 hex&gt;  &lt;installer name&gt;".</summary>
    public static string ChecksumName(Version version) => InstallerName(version) + ".sha256";

    public static string Format(Version v) => $"{v.Major}.{v.Minor}.{Math.Max(0, v.Build)}";

    /// <summary>"v0.11.0" or "0.11.0" as a three-part version; anything else (pre-release suffixes included) is null.</summary>
    public static Version? ParseTag(string? tag)
    {
        if (string.IsNullOrWhiteSpace(tag)) return null;
        var text = tag.Trim().TrimStart('v', 'V');
        var parts = text.Split('.');
        if (parts.Length != 3 || !parts.All(p => p.Length > 0 && p.All(char.IsAsciiDigit))) return null;
        return Version.TryParse(text, out var v) ? v : null;
    }


    /// <summary>Asks GitHub for the latest stable release and compares it with <paramref name="installed"/>.</summary>
    public async Task<UpdateCheck> CheckAsync(Version installed, CancellationToken ct = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"https://api.github.com/repos/{repository}/releases/latest");
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        request.Headers.Add("X-GitHub-Api-Version", "2022-11-28");
        HttpResponseMessage response;
        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeout.CancelAfter(TimeSpan.FromSeconds(20));
            response = await http.SendAsync(request, timeout.Token);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException && !ct.IsCancellationRequested)
        {
            return new UpdateCheck(UpdateCheckStatus.Offline, Detail: ex.Message);
        }

        using (response)
        {
            if (response.StatusCode == HttpStatusCode.NotFound) return new UpdateCheck(UpdateCheckStatus.NoRelease);
            if (!response.IsSuccessStatusCode) return new UpdateCheck(UpdateCheckStatus.Failed, Detail: $"HTTP {(int)response.StatusCode}");
            GitHubRelease? release;
            try
            {
                release = await response.Content.ReadFromJsonAsync(ct);
            }
            catch (JsonException ex)
            {
                return new UpdateCheck(UpdateCheckStatus.Failed, Detail: ex.Message);
            }

            if (release is null || release.Draft || release.Prerelease || ParseTag(release.TagName) is not { } version)
            {
                return new UpdateCheck(UpdateCheckStatus.NoRelease);
            }

            var info = new ReleaseInfo(version, release.TagName!, release.HtmlUrl ?? $"https://github.com/{repository}/releases",
                FindAsset(release, InstallerName(version)), FindAsset(release, ChecksumName(version)));
            return new UpdateCheck(Compare(version, installed) > 0 ? UpdateCheckStatus.Available : UpdateCheckStatus.UpToDate, info);
        }
    }

    /// <summary>Compares major, minor and patch only.</summary>
    public static int Compare(Version a, Version b) =>
        (a.Major, a.Minor, Math.Max(0, a.Build)).CompareTo((b.Major, b.Minor, Math.Max(0, b.Build)));

    /// <summary>
    /// Downloads the release's installer into <paramref name="folder"/> and verifies it. Returns the verified file;
    /// throws <see cref="UpdateDownloadException"/> (and leaves nothing behind) when the installer or its checksum is
    /// missing, the download is incomplete, or the checksum differs.
    /// </summary>
    public async Task<string> DownloadInstallerAsync(ReleaseInfo release, string folder, IProgress<double>? progress = null, CancellationToken ct = default)
    {
        if (release.Installer is not { } installer) throw new UpdateDownloadException("The release has no Windows installer.");
        if (release.Checksum is not { } checksumAsset) throw new UpdateDownloadException("The release has no published checksum for the installer.");
        if (!IsOwnDownload(installer.DownloadUrl) || !IsOwnDownload(checksumAsset.DownloadUrl)) throw new UpdateDownloadException("Unexpected download address.");

        var expected = ParseChecksum(await http.GetStringAsync(checksumAsset.DownloadUrl, ct), installer.Name)
            ?? throw new UpdateDownloadException("The published checksum could not be read.");
        if (installer.Digest is { } digest && digest.StartsWith("sha256:", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(digest[7..], expected, StringComparison.OrdinalIgnoreCase))
        {
            throw new UpdateDownloadException("The published checksums do not agree.", integrity: true);
        }

        Directory.CreateDirectory(folder);
        var target = Path.Combine(folder, installer.Name);
        var partial = target + ".partial";
        try
        {
            using (var response = await http.GetAsync(installer.DownloadUrl, HttpCompletionOption.ResponseHeadersRead, ct))
            {
                response.EnsureSuccessStatusCode();
                var total = response.Content.Headers.ContentLength ?? installer.Size;
                await using var source = await response.Content.ReadAsStreamAsync(ct);
                await using var file = new FileStream(partial, FileMode.Create, FileAccess.Write, FileShare.None, 1 << 16, FileOptions.Asynchronous);
                var buffer = new byte[1 << 16];
                long done = 0;
                int read;
                while ((read = await source.ReadAsync(buffer, ct)) > 0)
                {
                    await file.WriteAsync(buffer.AsMemory(0, read), ct);
                    done += read;
                    if (total > 0) progress?.Report(Math.Min(1, (double)done / total));
                }

                await file.FlushAsync(ct);
            }

            var length = new FileInfo(partial).Length;
            if (installer.Size > 0 && length != installer.Size) throw new UpdateDownloadException("The download is incomplete.", integrity: true);
            string actual;
            await using (var fs = File.OpenRead(partial)) actual = Convert.ToHexString(await SHA256.HashDataAsync(fs, ct));
            if (!string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase)) throw new UpdateDownloadException("The installer does not match its published checksum.", integrity: true);
            File.Move(partial, target, overwrite: true);
            return target;
        }
        catch (Exception ex) when (ex is HttpRequestException or IOException or UnauthorizedAccessException)
        {
            TryDelete(partial);
            throw new UpdateDownloadException(ex.Message);
        }
        catch
        {
            TryDelete(partial);
            throw;
        }
    }

    /// <summary>The hex SHA-256 for <paramref name="fileName"/> in a "sha256sum" line (or a bare hash).</summary>
    public static string? ParseChecksum(string text, string fileName)
    {
        foreach (var line in text.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var parts = line.Split((char[])[' ', '\t'], 2, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0 || parts[0].Length != 64 || !parts[0].All(char.IsAsciiHexDigit)) continue;
            if (parts.Length == 1 || string.Equals(parts[1].TrimStart('*').Trim(), fileName, StringComparison.OrdinalIgnoreCase)) return parts[0].ToUpperInvariant();
        }

        return null;
    }

    /// <summary>Only this repository's release downloads (GitHub redirects them to its storage itself).</summary>
    private bool IsOwnDownload(string url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps
        && string.Equals(uri.Host, "github.com", StringComparison.OrdinalIgnoreCase)
        && uri.AbsolutePath.StartsWith($"/{repository}/releases/download/", StringComparison.OrdinalIgnoreCase);

    private static ReleaseAsset? FindAsset(GitHubRelease release, string name) =>
        release.Assets?.FirstOrDefault(a => string.Equals(a.Name, name, StringComparison.Ordinal)) is { BrowserDownloadUrl: { } url } a
            ? new ReleaseAsset(a.Name!, url, a.Size, a.Digest)
            : null;

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path)) File.Delete(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }
    }

    internal sealed class GitHubRelease
    {
        [JsonPropertyName("tag_name")]
        public string? TagName { get; set; }

        [JsonPropertyName("html_url")]
        public string? HtmlUrl { get; set; }

        public bool Draft { get; set; }

        public bool Prerelease { get; set; }

        public List<GitHubAsset>? Assets { get; set; }
    }

    internal sealed class GitHubAsset
    {
        public string? Name { get; set; }

        [JsonPropertyName("browser_download_url")]
        public string? BrowserDownloadUrl { get; set; }

        public long Size { get; set; }

        public string? Digest { get; set; }
    }
}

public static class UpdateHttp
{
    /// <summary>GitHub's API requires a User-Agent; PhotoX names itself and its version.</summary>
    public static HttpClient WithUserAgent(this HttpClient http, Version installed)
    {
        http.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("PhotoX", UpdateClient.Format(installed)));
        return http;
    }
}

internal static class UpdateJson
{
    private static readonly JsonSerializerOptions Options = new() { PropertyNameCaseInsensitive = true };

    public static async Task<UpdateClient.GitHubRelease?> ReadFromJsonAsync(this HttpContent content, CancellationToken ct)
    {
        await using var stream = await content.ReadAsStreamAsync(ct);
        return await JsonSerializer.DeserializeAsync<UpdateClient.GitHubRelease>(stream, Options, ct);
    }
}
