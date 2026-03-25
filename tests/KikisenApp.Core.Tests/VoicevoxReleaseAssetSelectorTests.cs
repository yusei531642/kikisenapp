using KikisenApp.Core;

namespace KikisenApp.Core.Tests;

public sealed class VoicevoxReleaseAssetSelectorTests
{
    [Fact]
    public void SelectWindowsGpuAsset_PicksNvidiaArchiveAndAllParts()
    {
        var release = new GitHubReleaseResponse
        {
            TagName = "0.25.1",
            Assets =
            [
                new GitHubReleaseAsset
                {
                    Name = "voicevox_engine-windows-directml-0.25.1.7z.001",
                    BrowserDownloadUrl = "https://example.com/directml",
                    Size = 100
                },
                new GitHubReleaseAsset
                {
                    Name = "voicevox_engine-windows-nvidia-0.25.1.7z.001",
                    BrowserDownloadUrl = "https://example.com/nvidia-1",
                    Size = 200
                },
                new GitHubReleaseAsset
                {
                    Name = "voicevox_engine-windows-nvidia-0.25.1.7z.002",
                    BrowserDownloadUrl = "https://example.com/nvidia-2",
                    Size = 300
                }
            ]
        };

        var asset = VoicevoxReleaseAssetSelector.SelectWindowsGpuAsset(release, VoicevoxEngineVariant.Nvidia);

        Assert.Equal("0.25.1", asset.Version);
        Assert.Equal(VoicevoxEngineVariant.Nvidia, asset.Variant);
        Assert.Equal("voicevox_engine-windows-nvidia-0.25.1.7z", asset.ArchiveBaseName);
        Assert.Equal(2, asset.Parts.Count);
        Assert.Equal(500, asset.TotalSize);
    }

    [Fact]
    public void SelectWindowsGpuAsset_FallsBackToDirectMl()
    {
        var release = new GitHubReleaseResponse
        {
            TagName = "0.25.1",
            Assets =
            [
                new GitHubReleaseAsset
                {
                    Name = "voicevox_engine-windows-directml-0.25.1.7z.001",
                    BrowserDownloadUrl = "https://example.com/directml",
                    Size = 100
                }
            ]
        };

        var asset = VoicevoxReleaseAssetSelector.SelectWindowsGpuAsset(release, VoicevoxEngineVariant.Nvidia);

        Assert.Equal(VoicevoxEngineVariant.DirectMl, asset.Variant);
        Assert.Single(asset.Parts);
    }
}
