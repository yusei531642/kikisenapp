using System.Net.Http.Headers;
using System.Net.Http.Json;
using SharpCompress.Archives;
using SharpCompress.Archives.SevenZip;
using SharpCompress.Common;

namespace KikisenApp.Core;

public sealed class VoicevoxEngineInstaller
{
    private const string GitHubLatestReleaseEndpoint = "https://api.github.com/repos/VOICEVOX/voicevox_engine/releases/latest";

    private readonly HttpClient _httpClient;
    private readonly AppPaths _paths;

    public VoicevoxEngineInstaller(HttpClient httpClient, AppPaths paths)
    {
        _httpClient = httpClient;
        _paths = paths;

        if (_httpClient.DefaultRequestHeaders.UserAgent.Count == 0)
        {
            _httpClient.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("KikisenApp", "1.0"));
        }
    }

    public async Task<VoicevoxReleaseAsset> GetLatestReleaseAssetAsync(CancellationToken cancellationToken = default)
    {
        var release = await _httpClient.GetFromJsonAsync<GitHubReleaseResponse>(GitHubLatestReleaseEndpoint, cancellationToken)
            ?? throw new InvalidOperationException("VOICEVOX ENGINE の最新情報を取得できませんでした。");

        return VoicevoxReleaseAssetSelector.SelectWindowsCpuAsset(release);
    }

    public async Task<VoicevoxInstallationResult> EnsureInstalledAsync(
        IProgress<SetupProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        _paths.EnsureDirectories();
        var latestAsset = await GetLatestReleaseAssetAsync(cancellationToken);
        var installDirectory = Path.Combine(_paths.EngineDirectory, latestAsset.Version);

        var existingRunExe = FindRunExecutable(installDirectory);
        if (existingRunExe is not null)
        {
            progress?.Report(new SetupProgress("install", "VOICEVOX ENGINE はすでに導入されています。"));
            return new VoicevoxInstallationResult(latestAsset.Version, existingRunExe, false);
        }

        Directory.CreateDirectory(installDirectory);

        var archiveName = latestAsset.AssetName.Replace(".001", string.Empty, StringComparison.OrdinalIgnoreCase);
        var archivePath = Path.Combine(_paths.DownloadsDirectory, archiveName);

        progress?.Report(new SetupProgress("download", "VOICEVOX ENGINE をダウンロードしています。", 0, latestAsset.Size));
        await DownloadFileAsync(latestAsset.DownloadUrl, archivePath, latestAsset.Size, progress, cancellationToken);

        progress?.Report(new SetupProgress("extract", "VOICEVOX ENGINE を展開しています。"));
        ExtractArchive(archivePath, installDirectory);

        var runExe = FindRunExecutable(installDirectory)
            ?? throw new InvalidOperationException("VOICEVOX ENGINE の run.exe が見つかりませんでした。");

        progress?.Report(new SetupProgress("complete", "VOICEVOX ENGINE のセットアップが完了しました。"));
        return new VoicevoxInstallationResult(latestAsset.Version, runExe, true);
    }

    private async Task DownloadFileAsync(
        string url,
        string destinationPath,
        long? totalBytes,
        IProgress<SetupProgress>? progress,
        CancellationToken cancellationToken)
    {
        if (File.Exists(destinationPath))
        {
            var fileInfo = new FileInfo(destinationPath);
            if (totalBytes.HasValue && fileInfo.Length == totalBytes.Value)
            {
                progress?.Report(new SetupProgress("download", "すでにダウンロード済みのファイルを使います。", fileInfo.Length, totalBytes));
                return;
            }
        }

        using var response = await _httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();

        await using var responseStream = await response.Content.ReadAsStreamAsync(cancellationToken);
        await using var fileStream = File.Create(destinationPath);

        var buffer = new byte[1024 * 1024];
        long received = 0;

        while (true)
        {
            var read = await responseStream.ReadAsync(buffer, cancellationToken);
            if (read == 0)
            {
                break;
            }

            await fileStream.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
            received += read;
            progress?.Report(new SetupProgress("download", "VOICEVOX ENGINE をダウンロードしています。", received, totalBytes));
        }
    }

    private static void ExtractArchive(string archivePath, string destinationDirectory)
    {
        using var archive = SevenZipArchive.OpenArchive(archivePath, new());

        foreach (var entry in archive.Entries.Where(x => !x.IsDirectory))
        {
            entry.WriteToDirectory(destinationDirectory, new ExtractionOptions
            {
                ExtractFullPath = true,
                Overwrite = true
            });
        }
    }

    private static string? FindRunExecutable(string directory)
    {
        if (!Directory.Exists(directory))
        {
            return null;
        }

        return Directory
            .EnumerateFiles(directory, "run.exe", SearchOption.AllDirectories)
            .FirstOrDefault();
    }
}

public sealed record VoicevoxInstallationResult(
    string Version,
    string RunExecutablePath,
    bool InstalledNow);
