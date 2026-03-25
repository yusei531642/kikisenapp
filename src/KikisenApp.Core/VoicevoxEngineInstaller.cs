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

        var preferredVariant = VoicevoxEngineVariantDetector.DetectPreferredVariant();
        return VoicevoxReleaseAssetSelector.SelectWindowsGpuAsset(release, preferredVariant);
    }

    public async Task<VoicevoxInstallationResult> EnsureInstalledAsync(
        IProgress<SetupProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        _paths.EnsureDirectories();
        var latestAsset = await GetLatestReleaseAssetAsync(cancellationToken);
        var installDirectory = Path.Combine(
            _paths.EngineDirectory,
            $"{latestAsset.Version}-{latestAsset.Variant.ToString().ToLowerInvariant()}");

        var existingRunExe = FindRunExecutable(installDirectory);
        if (existingRunExe is not null)
        {
            progress?.Report(new SetupProgress("install", $"VOICEVOX ENGINE {FormatVariantName(latestAsset.Variant)} はすでに導入されています。"));
            return new VoicevoxInstallationResult(latestAsset.Version, latestAsset.Variant, existingRunExe, false);
        }

        Directory.CreateDirectory(installDirectory);

        var archivePaths = latestAsset.Parts
            .Select(x => Path.Combine(_paths.DownloadsDirectory, x.Name))
            .ToList();

        progress?.Report(new SetupProgress("download", $"VOICEVOX ENGINE {FormatVariantName(latestAsset.Variant)} をダウンロードしています。", 0, latestAsset.TotalSize));
        await DownloadPartsAsync(latestAsset, archivePaths, progress, cancellationToken);

        progress?.Report(new SetupProgress("extract", $"VOICEVOX ENGINE {FormatVariantName(latestAsset.Variant)} を展開しています。"));
        ExtractArchive(archivePaths, installDirectory);

        var runExe = FindRunExecutable(installDirectory)
            ?? throw new InvalidOperationException("VOICEVOX ENGINE の run.exe が見つかりませんでした。");

        progress?.Report(new SetupProgress("complete", $"VOICEVOX ENGINE {FormatVariantName(latestAsset.Variant)} のセットアップが完了しました。"));
        return new VoicevoxInstallationResult(latestAsset.Version, latestAsset.Variant, runExe, true);
    }

    private async Task DownloadPartsAsync(
        VoicevoxReleaseAsset asset,
        IReadOnlyList<string> destinationPaths,
        IProgress<SetupProgress>? progress,
        CancellationToken cancellationToken)
    {
        long received = 0;

        for (var index = 0; index < asset.Parts.Count; index++)
        {
            var part = asset.Parts[index];
            var destinationPath = destinationPaths[index];

            if (File.Exists(destinationPath))
            {
                var fileInfo = new FileInfo(destinationPath);
                if (fileInfo.Length == part.Size)
                {
                    received += fileInfo.Length;
                    progress?.Report(new SetupProgress("download", $"VOICEVOX ENGINE {FormatVariantName(asset.Variant)} をダウンロードしています。", received, asset.TotalSize));
                    continue;
                }
            }

            using var response = await _httpClient.GetAsync(part.BrowserDownloadUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            response.EnsureSuccessStatusCode();

            await using var responseStream = await response.Content.ReadAsStreamAsync(cancellationToken);
            await using var fileStream = File.Create(destinationPath);

            var buffer = new byte[1024 * 1024];

            while (true)
            {
                var read = await responseStream.ReadAsync(buffer, cancellationToken);
                if (read == 0)
                {
                    break;
                }

                await fileStream.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
                received += read;
                progress?.Report(new SetupProgress("download", $"VOICEVOX ENGINE {FormatVariantName(asset.Variant)} をダウンロードしています。", received, asset.TotalSize));
            }
        }
    }

    private static void ExtractArchive(IReadOnlyList<string> archivePaths, string destinationDirectory)
    {
        var partFiles = archivePaths.Select(path => new FileInfo(path)).ToList();
        using var archive = SevenZipArchive.OpenArchive(partFiles, new());

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

    private static string FormatVariantName(VoicevoxEngineVariant variant)
    {
        return variant switch
        {
            VoicevoxEngineVariant.Nvidia => "GPU版 (NVIDIA)",
            _ => "GPU版 (DirectML)"
        };
    }
}

public sealed record VoicevoxInstallationResult(
    string Version,
    VoicevoxEngineVariant Variant,
    string RunExecutablePath,
    bool InstalledNow);
