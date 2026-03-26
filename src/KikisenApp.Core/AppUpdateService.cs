using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace KikisenApp.Core;

public sealed class AppUpdateService
{
    private readonly HttpClient _httpClient;
    private readonly AppPaths _paths;

    public AppUpdateService(HttpClient httpClient, AppPaths paths)
    {
        _httpClient = httpClient;
        _paths = paths;

        if (_httpClient.DefaultRequestHeaders.UserAgent.Count == 0)
        {
            _httpClient.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("KikisenApp", "1.0"));
        }
    }

    public async Task<AppUpdateCheckResult> CheckForUpdatesAsync(
        IEnumerable<string?> currentVersions,
        CancellationToken cancellationToken = default)
    {
        var release = await _httpClient.GetFromJsonAsync<GitHubReleaseResponse>(
            ExternalLinks.AppLatestReleaseApi,
            cancellationToken)
            ?? throw new InvalidOperationException("最新版の情報を取得できませんでした。");

        var installerAsset = AppReleaseAssetSelector.SelectInstallerAsset(release);
        var installedVersions = currentVersions.ToList();

        return new AppUpdateCheckResult(
            CurrentVersion: installedVersions.FirstOrDefault(x => !string.IsNullOrWhiteSpace(x)) ?? string.Empty,
            InstalledVersions: installedVersions,
            LatestVersion: release.TagName,
            ReleasePageUrl: string.IsNullOrWhiteSpace(release.HtmlUrl) ? ExternalLinks.AppReleasesPage : release.HtmlUrl,
            InstallerAsset: installerAsset,
            UpdateAvailable: AppVersionResolver.HasUpdate(release.TagName, installedVersions.ToArray()));
    }

    public async Task<string> DownloadInstallerAsync(
        AppUpdateCheckResult update,
        IProgress<SetupProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(update);

        if (update.InstallerAsset is null)
        {
            throw new InvalidOperationException("更新用インストーラーが見つかりませんでした。");
        }

        _paths.EnsureDirectories();

        var safeVersion = NormalizeVersion(update.LatestVersion);
        var destinationDirectory = Path.Combine(_paths.UpdatesDirectory, safeVersion);
        Directory.CreateDirectory(destinationDirectory);

        var destinationPath = Path.Combine(destinationDirectory, update.InstallerAsset.Name);
        if (File.Exists(destinationPath))
        {
            var existingFile = new FileInfo(destinationPath);
            if (existingFile.Length == update.InstallerAsset.Size && existingFile.Length > 0)
            {
                progress?.Report(new SetupProgress("download", "すでにダウンロード済みの最新版を使います。", existingFile.Length, existingFile.Length));
                return destinationPath;
            }
        }

        using var response = await _httpClient.GetAsync(
            update.InstallerAsset.BrowserDownloadUrl,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);
        response.EnsureSuccessStatusCode();

        var totalBytes = response.Content.Headers.ContentLength ?? update.InstallerAsset.Size;
        await using var responseStream = await response.Content.ReadAsStreamAsync(cancellationToken);
        await using var destinationStream = File.Create(destinationPath);

        var buffer = new byte[1024 * 1024];
        long receivedBytes = 0;

        while (true)
        {
            var read = await responseStream.ReadAsync(buffer, cancellationToken);
            if (read == 0)
            {
                break;
            }

            await destinationStream.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
            receivedBytes += read;
            progress?.Report(new SetupProgress("download", "最新版をダウンロードしています。", receivedBytes, totalBytes));
        }

        progress?.Report(new SetupProgress("complete", "最新版のダウンロードが完了しました。", receivedBytes, totalBytes));
        return destinationPath;
    }

    private static string NormalizeVersion(string version)
    {
        var safeVersion = (version ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(safeVersion))
        {
            return "latest";
        }

        foreach (var invalidChar in Path.GetInvalidFileNameChars())
        {
            safeVersion = safeVersion.Replace(invalidChar, '_');
        }

        return safeVersion.Trim();
    }
}

public static class AppReleaseAssetSelector
{
    private static readonly string[] PreferredInstallerNames =
    [
        "KikisenApp-Setup.exe",
        "KikisenApp.Desktop.exe"
    ];

    public static GitHubReleaseAsset? SelectInstallerAsset(GitHubReleaseResponse release)
    {
        ArgumentNullException.ThrowIfNull(release);

        foreach (var name in PreferredInstallerNames)
        {
            var asset = release.Assets.FirstOrDefault(x => string.Equals(x.Name, name, StringComparison.OrdinalIgnoreCase));
            if (asset is not null)
            {
                return asset;
            }
        }

        return release.Assets.FirstOrDefault(x => x.Name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase));
    }
}

public sealed record AppUpdateCheckResult(
    string CurrentVersion,
    IReadOnlyList<string?> InstalledVersions,
    string LatestVersion,
    string ReleasePageUrl,
    GitHubReleaseAsset? InstallerAsset,
    bool UpdateAvailable);
