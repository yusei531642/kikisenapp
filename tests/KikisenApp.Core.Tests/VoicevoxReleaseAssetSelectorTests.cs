using KikisenApp.Core;

namespace KikisenApp.Core.Tests;

public sealed class VoicevoxReleaseAssetSelectorTests
{
    [Fact]
    public void SelectWindowsCpuAsset_PicksWindowsCpuArchive()
    {
        var release = new GitHubReleaseResponse
        {
            TagName = "0.25.1",
            Assets =
            [
                new GitHubReleaseAsset
                {
                    Name = "voicevox_engine-linux-cpu-x64-0.25.1.7z.001",
                    BrowserDownloadUrl = "https://example.com/linux",
                    Size = 100
                },
                new GitHubReleaseAsset
                {
                    Name = "voicevox_engine-windows-cpu-0.25.1.7z.001",
                    BrowserDownloadUrl = "https://example.com/windows",
                    Size = 200
                }
            ]
        };

        var asset = VoicevoxReleaseAssetSelector.SelectWindowsCpuAsset(release);

        Assert.Equal("0.25.1", asset.Version);
        Assert.Equal("https://example.com/windows", asset.DownloadUrl);
        Assert.Equal(200, asset.Size);
    }
}
