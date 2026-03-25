using System.Text.Json.Serialization;

namespace KikisenApp.Core;

public sealed class GitHubReleaseResponse
{
    [JsonPropertyName("tag_name")]
    public string TagName { get; set; } = string.Empty;

    [JsonPropertyName("assets")]
    public List<GitHubReleaseAsset> Assets { get; set; } = [];
}

public sealed class GitHubReleaseAsset
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("browser_download_url")]
    public string BrowserDownloadUrl { get; set; } = string.Empty;

    [JsonPropertyName("size")]
    public long Size { get; set; }
}

public sealed record VoicevoxReleaseAsset(
    string Version,
    string AssetName,
    string DownloadUrl,
    long Size);

public static class VoicevoxReleaseAssetSelector
{
    public static VoicevoxReleaseAsset SelectWindowsCpuAsset(GitHubReleaseResponse release)
    {
        ArgumentNullException.ThrowIfNull(release);

        var asset = release.Assets.FirstOrDefault(x =>
            x.Name.StartsWith("voicevox_engine-windows-cpu-", StringComparison.OrdinalIgnoreCase) &&
            x.Name.EndsWith(".7z.001", StringComparison.OrdinalIgnoreCase));

        if (asset is null)
        {
            throw new InvalidOperationException("VOICEVOX ENGINE の Windows CPU 版アセットが見つかりませんでした。");
        }

        return new VoicevoxReleaseAsset(
            Version: release.TagName,
            AssetName: asset.Name,
            DownloadUrl: asset.BrowserDownloadUrl,
            Size: asset.Size);
    }
}
