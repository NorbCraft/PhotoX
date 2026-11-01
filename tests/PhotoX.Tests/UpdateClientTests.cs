using System.Net;
using System.Security.Cryptography;
using System.Text;
using PhotoX.Storage.Updates;

namespace PhotoX.Tests;

/// <summary>A fake GitHub: answers by URL, counts requests, and can fail like a missing connection.</summary>
public sealed class FakeGitHub : HttpMessageHandler
{
    public Dictionary<string, Func<HttpResponseMessage>> Routes { get; } = [];

    public List<string> Requested { get; } = [];

    public bool Offline { get; set; }

    public const string Api = "https://api.github.com/repos/NorbCraft/PhotoX/releases/latest";

    public static string DownloadUrl(string version, string file) => $"https://github.com/NorbCraft/PhotoX/releases/download/v{version}/{file}";

    /// <summary>A published release <paramref name="version"/> with an installer of <paramref name="installer"/> bytes and its checksum.</summary>
    public FakeGitHub Release(string version, byte[] installer, string? checksum = null, bool prerelease = false, bool withChecksum = true, string? digest = null)
    {
        var name = $"PhotoX-{version}-win-Setup.exe";
        var hash = checksum ?? Convert.ToHexString(SHA256.HashData(installer)).ToLowerInvariant();
        var assets = new List<string>
        {
            $$"""{ "name": "{{name}}", "browser_download_url": "{{DownloadUrl(version, name)}}", "size": {{installer.Length}}{{(digest is null ? "" : $", \"digest\": \"{digest}\"")}} }""",
        };
        if (withChecksum) assets.Add($$"""{ "name": "{{name}}.sha256", "browser_download_url": "{{DownloadUrl(version, name + ".sha256")}}", "size": 100 }""");
        Routes[Api] = () => Json($$"""
            { "tag_name": "v{{version}}", "html_url": "https://github.com/NorbCraft/PhotoX/releases/tag/v{{version}}",
              "draft": false, "prerelease": {{(prerelease ? "true" : "false")}}, "assets": [ {{string.Join(",", assets)}} ] }
            """);
        Routes[DownloadUrl(version, name)] = () => new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(installer) };
        Routes[DownloadUrl(version, name + ".sha256")] = () => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent($"{hash}  {name}\n") };
        return this;
    }

    public static HttpResponseMessage Json(string json) => new(HttpStatusCode.OK) { Content = new StringContent(json, Encoding.UTF8, "application/json") };

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var url = request.RequestUri!.ToString();
        lock (Requested) Requested.Add(url);
        if (Offline) throw new HttpRequestException("No such host is known.");
        return Task.FromResult(Routes.TryGetValue(url, out var answer) ? answer() : new HttpResponseMessage(HttpStatusCode.NotFound));
    }
}

/// <summary>prompt_08 §6: Check for Updates against GitHub Releases, and the verified installer download (TS-23).</summary>
public class UpdateClientTests
{
    private static readonly Version Installed = new(0, 11, 0);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static byte[] Installer(int size = 300_000)
    {
        var bytes = new byte[size];
        new Random(8).NextBytes(bytes);
        return bytes;
    }

    private static UpdateClient Client(FakeGitHub github) => new(new HttpClient(github).WithUserAgent(Installed));

    [Fact]
    public async Task No_published_release_and_the_same_version_are_reported_as_such()
    {
        var github = new FakeGitHub();
        Assert.Equal(UpdateCheckStatus.NoRelease, (await Client(github).CheckAsync(Installed, Ct)).Status);

        github.Release("0.11.0", Installer());
        Assert.Equal(UpdateCheckStatus.UpToDate, (await Client(github).CheckAsync(Installed, Ct)).Status);
        github.Release("0.10.2", Installer());
        Assert.Equal(UpdateCheckStatus.UpToDate, (await Client(github).CheckAsync(Installed, Ct)).Status);
        Assert.All(github.Requested, u => Assert.Equal(FakeGitHub.Api, u));                         // only the public API, nothing downloaded
    }

    [Fact]
    public async Task A_newer_stable_release_is_offered_with_its_installer_and_checksum()
    {
        var github = new FakeGitHub().Release("0.12.0", Installer());

        var check = await Client(github).CheckAsync(Installed, Ct);

        Assert.Equal(UpdateCheckStatus.Available, check.Status);
        Assert.Equal(new Version(0, 12, 0), check.Release!.Version);
        Assert.Equal("PhotoX-0.12.0-win-Setup.exe", check.Release.Installer!.Name);
        Assert.Equal("PhotoX-0.12.0-win-Setup.exe.sha256", check.Release.Checksum!.Name);
        Assert.Equal("https://github.com/NorbCraft/PhotoX/releases/tag/v0.12.0", check.Release.PageUrl);
    }

    [Theory]
    [InlineData("v0.12.0-rc1")]
    [InlineData("nightly")]
    [InlineData("v1.2")]
    public void Only_plain_version_tags_count_as_stable(string tag) => Assert.Null(UpdateClient.ParseTag(tag));

    [Fact]
    public async Task A_prerelease_is_never_offered()
    {
        var github = new FakeGitHub().Release("0.12.0", Installer(), prerelease: true);
        Assert.Equal(UpdateCheckStatus.NoRelease, (await Client(github).CheckAsync(Installed, Ct)).Status);
    }

    [Fact]
    public async Task Without_a_connection_the_check_says_so()
    {
        var github = new FakeGitHub { Offline = true };
        Assert.Equal(UpdateCheckStatus.Offline, (await Client(github).CheckAsync(Installed, Ct)).Status);
    }

    [Fact]
    public async Task The_installer_is_kept_only_when_it_matches_its_published_checksum()
    {
        using var dir = new TempDir();
        var bytes = Installer();
        var github = new FakeGitHub().Release("0.12.0", bytes);
        var release = (await Client(github).CheckAsync(Installed, Ct)).Release!;
        var progress = new List<double>();

        var path = await Client(github).DownloadInstallerAsync(release, dir.Path, new SyncProgress(progress), Ct);

        Assert.Equal(Path.Combine(dir.Path, "PhotoX-0.12.0-win-Setup.exe"), path);
        Assert.Equal(bytes, File.ReadAllBytes(path));
        Assert.Equal(["PhotoX-0.12.0-win-Setup.exe"], Directory.GetFiles(dir.Path).Select(Path.GetFileName));
        Assert.Equal(1, progress[^1], 3);
    }

    [Fact]
    public async Task A_download_that_differs_from_its_checksum_is_deleted()
    {
        using var dir = new TempDir();
        var github = new FakeGitHub().Release("0.12.0", Installer(), checksum: new string('a', 64));
        var release = (await Client(github).CheckAsync(Installed, Ct)).Release!;

        var ex = await Assert.ThrowsAsync<UpdateDownloadException>(() => Client(github).DownloadInstallerAsync(release, dir.Path, null, Ct));

        Assert.True(ex.Integrity);
        Assert.Empty(Directory.GetFiles(dir.Path));                                                  // nothing left to install
    }

    [Fact]
    public async Task An_incomplete_download_is_deleted()
    {
        using var dir = new TempDir();
        var bytes = Installer();
        var github = new FakeGitHub().Release("0.12.0", bytes);
        var name = "PhotoX-0.12.0-win-Setup.exe";
        github.Routes[FakeGitHub.DownloadUrl("0.12.0", name)] = () => new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(bytes[..1000]) };
        var release = (await Client(github).CheckAsync(Installed, Ct)).Release!;

        var ex = await Assert.ThrowsAsync<UpdateDownloadException>(() => Client(github).DownloadInstallerAsync(release, dir.Path, null, Ct));

        Assert.True(ex.Integrity);
        Assert.Empty(Directory.GetFiles(dir.Path));
    }

    [Fact]
    public async Task A_release_without_a_checksum_or_from_elsewhere_is_not_downloaded()
    {
        using var dir = new TempDir();
        var github = new FakeGitHub().Release("0.12.0", Installer(), withChecksum: false);
        var release = (await Client(github).CheckAsync(Installed, Ct)).Release!;
        await Assert.ThrowsAsync<UpdateDownloadException>(() => Client(github).DownloadInstallerAsync(release, dir.Path, null, Ct));

        github.Release("0.12.0", Installer());
        release = (await Client(github).CheckAsync(Installed, Ct)).Release!;
        var foreign = release with { Installer = release.Installer! with { DownloadUrl = "https://example.com/NorbCraft/PhotoX/releases/download/v0.12.0/PhotoX-0.12.0-win-Setup.exe" } };
        await Assert.ThrowsAsync<UpdateDownloadException>(() => Client(github).DownloadInstallerAsync(foreign, dir.Path, null, Ct));
        Assert.Empty(Directory.GetFiles(dir.Path));
        Assert.DoesNotContain(github.Requested, u => u.Contains("example.com", StringComparison.Ordinal));
    }

    [Fact]
    public async Task GitHubs_own_digest_must_agree_with_the_published_checksum()
    {
        using var dir = new TempDir();
        var github = new FakeGitHub().Release("0.12.0", Installer(), digest: "sha256:" + new string('b', 64));
        var release = (await Client(github).CheckAsync(Installed, Ct)).Release!;

        Assert.True((await Assert.ThrowsAsync<UpdateDownloadException>(() => Client(github).DownloadInstallerAsync(release, dir.Path, null, Ct))).Integrity);
    }

    [Theory]
    [InlineData("ABCDEF0123456789abcdef0123456789ABCDEF0123456789abcdef0123456789  PhotoX-1.0.0-win-Setup.exe", true)]
    [InlineData("abcdef0123456789abcdef0123456789abcdef0123456789abcdef0123456789 *PhotoX-1.0.0-win-Setup.exe", true)]
    [InlineData("abcdef0123456789abcdef0123456789abcdef0123456789abcdef0123456789", true)]
    [InlineData("abcdef0123456789abcdef0123456789abcdef0123456789abcdef0123456789  other.exe", false)]
    [InlineData("not a checksum", false)]
    public void Checksum_lines_are_read_for_the_installer_only(string text, bool found) =>
        Assert.Equal(found, UpdateClient.ParseChecksum(text, "PhotoX-1.0.0-win-Setup.exe") is not null);

    /// <summary>Reports at once (Progress&lt;T&gt; posts to a synchronisation context).</summary>
    private sealed class SyncProgress(List<double> values) : IProgress<double>
    {
        public void Report(double value) => values.Add(value);
    }
}
