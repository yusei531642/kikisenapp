using System.Text.Json.Serialization;

namespace KikisenApp.Core;

public sealed class GitHubReleaseResponse
{
    [JsonPropertyName("tag_name")]
    public string TagName { get; set; } = string.Empty;

    [JsonPropertyName("html_url")]
    public string HtmlUrl { get; set; } = string.Empty;

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
    VoicevoxEngineVariant Variant,
    string ArchiveBaseName,
    IReadOnlyList<GitHubReleaseAsset> Parts)
{
    public long TotalSize => Parts.Sum(x => x.Size);
}

public static class VoicevoxReleaseAssetSelector
{
    public static VoicevoxReleaseAsset SelectWindowsGpuAsset(
        GitHubReleaseResponse release,
        VoicevoxEngineVariant preferredVariant)
    {
        ArgumentNullException.ThrowIfNull(release);

        var variants = preferredVariant == VoicevoxEngineVariant.Nvidia
            ? new[] { VoicevoxEngineVariant.Nvidia, VoicevoxEngineVariant.DirectMl }
            : new[] { VoicevoxEngineVariant.DirectMl, VoicevoxEngineVariant.Nvidia };

        foreach (var variant in variants)
        {
            var prefix = variant switch
            {
                VoicevoxEngineVariant.Nvidia => "voicevox_engine-windows-nvidia-",
                _ => "voicevox_engine-windows-directml-"
            };

            var firstPart = release.Assets.FirstOrDefault(x =>
                x.Name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) &&
                x.Name.EndsWith(".7z.001", StringComparison.OrdinalIgnoreCase));

            if (firstPart is null)
            {
                continue;
            }

            var archiveBaseName = firstPart.Name[..^4];
            var parts = release.Assets
                .Where(x =>
                    x.Name.StartsWith($"{archiveBaseName}.", StringComparison.OrdinalIgnoreCase) &&
                    int.TryParse(Path.GetExtension(x.Name).TrimStart('.'), out _))
                .OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (parts.Count == 0)
            {
                continue;
            }

            return new VoicevoxReleaseAsset(
                Version: release.TagName,
                Variant: variant,
                ArchiveBaseName: archiveBaseName,
                Parts: parts);
        }

        throw new InvalidOperationException("VOICEVOX ENGINE の Windows GPU 版アセットが見つかりませんでした。");
    }
}

public static class VoicevoxVersionComparer
{
    public static bool IsNewerVersion(string latestVersion, string? installedVersion)
    {
        var normalizedLatest = AppVersionResolver.Normalize(latestVersion);
        var normalizedInstalled = AppVersionResolver.Normalize(installedVersion);

        if (string.IsNullOrWhiteSpace(normalizedLatest))
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(normalizedInstalled))
        {
            return true;
        }

        if (Version.TryParse(normalizedLatest, out var latest) &&
            Version.TryParse(normalizedInstalled, out var installed))
        {
            return latest > installed;
        }

        return !string.Equals(normalizedLatest, normalizedInstalled, StringComparison.OrdinalIgnoreCase);
    }

    private static string Normalize(string? version)
    {
        return AppVersionResolver.Normalize(version);
    }
}
