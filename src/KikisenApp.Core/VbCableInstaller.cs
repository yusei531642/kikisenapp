using System.Diagnostics;
using System.IO.Compression;
using System.Text.RegularExpressions;

namespace KikisenApp.Core;

public sealed record VbCablePackageInfo(
    string PackagePageUrl,
    string DownloadUrl,
    string ArchiveFileName);

public static partial class VbCablePackageSelector
{
    public static VbCablePackageInfo SelectFromHtml(string html)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(html);

        var match = DownloadUrlRegex().Match(html);
        if (!match.Success)
        {
            throw new InvalidOperationException("VB-CABLE のダウンロード URL を公式ページから見つけられませんでした。");
        }

        var downloadUrl = match.Value;
        return new VbCablePackageInfo(
            PackagePageUrl: ExternalLinks.VbCablePage,
            DownloadUrl: downloadUrl,
            ArchiveFileName: Path.GetFileName(new Uri(downloadUrl).AbsolutePath));
    }

    [GeneratedRegex(@"https://download\.vb-audio\.com/Download_CABLE/VBCABLE_Driver_Pack\d+\.zip", RegexOptions.IgnoreCase)]
    private static partial Regex DownloadUrlRegex();
}

public sealed class VbCableInstaller
{
    private readonly HttpClient _httpClient;
    private readonly AppPaths _paths;

    public VbCableInstaller(HttpClient httpClient, AppPaths paths)
    {
        _httpClient = httpClient;
        _paths = paths;
    }

    public async Task<VbCablePackageInfo> GetLatestPackageInfoAsync(CancellationToken cancellationToken = default)
    {
        var html = await _httpClient.GetStringAsync(ExternalLinks.VbCablePage, cancellationToken);
        return VbCablePackageSelector.SelectFromHtml(html);
    }

    public async Task<VbCableInstallResult> DownloadAndLaunchInstallerAsync(
        IProgress<SetupProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        _paths.EnsureDirectories();

        var package = await GetLatestPackageInfoAsync(cancellationToken);
        var archivePath = Path.Combine(_paths.DownloadsDirectory, package.ArchiveFileName);
        var extractDirectory = Path.Combine(
            _paths.DownloadsDirectory,
            Path.GetFileNameWithoutExtension(package.ArchiveFileName));

        progress?.Report(new SetupProgress("download", "VB-CABLE をダウンロードしています。"));
        await DownloadFileAsync(package.DownloadUrl, archivePath, progress, cancellationToken);

        progress?.Report(new SetupProgress("extract", "VB-CABLE を展開しています。"));
        ExtractArchive(archivePath, extractDirectory);

        var installerPath = FindInstallerPath(extractDirectory);
        progress?.Report(new SetupProgress("install", "VB-CABLE セットアップを管理者権限で起動します。"));
        var exitCode = await LaunchInstallerAsync(installerPath, cancellationToken);

        return new VbCableInstallResult(
            DownloadUrl: package.DownloadUrl,
            ExtractDirectory: extractDirectory,
            InstallerPath: installerPath,
            InstallerExitCode: exitCode,
            RestartRequired: true);
    }

    private async Task DownloadFileAsync(
        string url,
        string destinationPath,
        IProgress<SetupProgress>? progress,
        CancellationToken cancellationToken)
    {
        if (File.Exists(destinationPath) && new FileInfo(destinationPath).Length > 0)
        {
            progress?.Report(new SetupProgress("download", "すでにダウンロード済みの VB-CABLE を使います。"));
            return;
        }

        using var response = await _httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();

        await using var responseStream = await response.Content.ReadAsStreamAsync(cancellationToken);
        await using var fileStream = File.Create(destinationPath);
        await responseStream.CopyToAsync(fileStream, cancellationToken);
    }

    private static void ExtractArchive(string archivePath, string destinationDirectory)
    {
        if (Directory.Exists(destinationDirectory) &&
            File.Exists(Path.Combine(destinationDirectory, "VBCABLE_Setup_x64.exe")))
        {
            return;
        }

        Directory.CreateDirectory(destinationDirectory);
        ZipFile.ExtractToDirectory(archivePath, destinationDirectory, overwriteFiles: true);
    }

    private static string FindInstallerPath(string extractDirectory)
    {
        var installerName = Environment.Is64BitOperatingSystem ? "VBCABLE_Setup_x64.exe" : "VBCABLE_Setup.exe";
        var installerPath = Directory.EnumerateFiles(extractDirectory, installerName, SearchOption.AllDirectories).FirstOrDefault();

        return installerPath
            ?? throw new FileNotFoundException("VB-CABLE のセットアップ EXE が見つかりませんでした。", installerName);
    }

    private static async Task<int> LaunchInstallerAsync(string installerPath, CancellationToken cancellationToken)
    {
        var process = Process.Start(new ProcessStartInfo
        {
            FileName = installerPath,
            WorkingDirectory = Path.GetDirectoryName(installerPath) ?? AppContext.BaseDirectory,
            UseShellExecute = true,
            Verb = "runas"
        }) ?? throw new InvalidOperationException("VB-CABLE のセットアップを起動できませんでした。");

        await process.WaitForExitAsync(cancellationToken);
        return process.ExitCode;
    }
}

public sealed record VbCableInstallResult(
    string DownloadUrl,
    string ExtractDirectory,
    string InstallerPath,
    int InstallerExitCode,
    bool RestartRequired);
