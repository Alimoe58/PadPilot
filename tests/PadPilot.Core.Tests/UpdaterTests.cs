using System.Text.Json;
using PadPilot.Core.Updates;

namespace PadPilot.Core.Tests;

public sealed class UpdaterTests : IDisposable
{
    private const string Sha = "f4fc92da20509e94b2a948ce5c905d0e672bae8b6d2a26f09ac59751f7265bf2";
    private readonly string _dir = Directory.CreateTempSubdirectory("padpilot-update-").FullName;

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    private static UpdateInfo? Parse(string assets, string tag = "v1.3.0") =>
        UpdateChecker.ParseRelease(JsonDocument.Parse($$"""
            { "tag_name": "{{tag}}", "html_url": "https://github.com/Alimoe58/PadPilot/releases/tag/{{tag}}", "assets": [ {{assets}} ] }
            """).RootElement, new Version(1, 2, 1));

    private static string Asset(string name = "PadPilot.exe", string url = "https://github.com/Alimoe58/PadPilot/releases/download/v1.3.0/PadPilot.exe",
        string digest = "sha256:" + Sha, long size = 66745733) =>
        $$"""{ "name": "{{name}}", "browser_download_url": "{{url}}", "digest": "{{digest}}", "size": {{size}} }""";

    [Fact]
    public void ReleaseWithChecksummedExeCanBeInstalled()
    {
        var info = Parse(Asset());
        Assert.NotNull(info);
        Assert.Equal(new Version(1, 3, 0), info.Version);
        Assert.Equal(new ReleaseFile("https://github.com/Alimoe58/PadPilot/releases/download/v1.3.0/PadPilot.exe", 66745733, Sha), info.Download);
    }

    [Fact]
    public void OlderReleaseIsNoUpdate() => Assert.Null(Parse(Asset(), tag: "v1.2.1"));

    [Theory]
    [InlineData("name", "Other.exe")]
    [InlineData("url", "https://example.com/PadPilot.exe")]
    [InlineData("url", "https://github.com/someone-else/PadPilot/releases/download/v1.3.0/PadPilot.exe")]
    [InlineData("digest", "")]
    [InlineData("digest", "md5:abc")]
    [InlineData("digest", "sha256:1234")]
    public void WithoutAProperExeTheUpdateIsOnlyOfferedAsALink(string field, string value)
    {
        string asset = field switch
        {
            "name" => Asset(name: value),
            "url" => Asset(url: value),
            _ => Asset(digest: value),
        };
        var info = Parse(asset);
        Assert.NotNull(info);             // still offered …
        Assert.Null(info.Download);       // … but only as a link to the release page
    }

    [Fact]
    public void SwapPutsTheNewFileInPlaceAndKeepsTheOldOne()
    {
        string exe = Path.Combine(_dir, "PadPilot.exe");
        File.WriteAllText(exe, "old");
        File.WriteAllText(exe + Updater.DownloadSuffix, "new");

        Updater.Swap(exe, exe + Updater.DownloadSuffix);

        Assert.Equal("new", File.ReadAllText(exe));
        Assert.Equal("old", File.ReadAllText(exe + Updater.OldSuffix));
        Assert.False(File.Exists(exe + Updater.DownloadSuffix));
    }

    [Fact]
    public void FailedSwapLeavesTheOldExe()
    {
        string exe = Path.Combine(_dir, "PadPilot.exe");
        File.WriteAllText(exe, "old");

        Assert.ThrowsAny<IOException>(() => Updater.Swap(exe, Path.Combine(_dir, "missing.download")));
        Assert.Equal("old", File.ReadAllText(exe));
    }

    [Fact]
    public async Task CleanUpRemovesLeftovers()
    {
        string exe = Path.Combine(_dir, "PadPilot.exe");
        File.WriteAllText(exe, "new");
        File.WriteAllText(exe + Updater.OldSuffix, "old");
        File.WriteAllText(exe + Updater.DownloadSuffix, "partial");

        Assert.True(await Updater.CleanUpAsync(exe, TimeSpan.FromSeconds(2)));
        Assert.Equal(["PadPilot.exe"], Directory.GetFiles(_dir).Select(Path.GetFileName));
    }

    [Fact]
    public void OnlyTheSingleFileBuildCanReplaceItself()
    {
        string exe = Path.Combine(_dir, "PadPilot.exe");
        File.WriteAllText(exe, "exe");
        Assert.True(Updater.CanReplace(exe));

        File.WriteAllText(Path.Combine(_dir, "PadPilot.dll"), "development build");
        Assert.False(Updater.CanReplace(exe));
    }

    [Fact]
    public async Task DownloadsFromOtherAddressesAreRefused()
    {
        var file = new ReleaseFile("https://example.com/PadPilot.exe", 10, Sha);
        await Assert.ThrowsAsync<UpdateFailedException>(() =>
            Updater.DownloadAsync(file, Path.Combine(_dir, "PadPilot.exe"), new Version(1, 2, 0)));
    }
}
