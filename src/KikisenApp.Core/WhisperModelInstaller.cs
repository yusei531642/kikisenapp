using Whisper.net.Ggml;

namespace KikisenApp.Core;

public sealed class WhisperModelInstaller
{
    private readonly AppPaths _paths;

    public WhisperModelInstaller(AppPaths paths)
    {
        _paths = paths;
    }

    public string GetModelPath(WhisperModelDefinition model)
    {
        return Path.Combine(_paths.WhisperDirectory, model.FileName);
    }

    public bool IsInstalled(WhisperModelDefinition model)
    {
        var path = GetModelPath(model);
        return File.Exists(path) && new FileInfo(path).Length > 0;
    }

    public async Task<string> EnsureInstalledAsync(
        WhisperModelDefinition model,
        IProgress<SetupProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        _paths.EnsureDirectories();

        var modelPath = GetModelPath(model);
        if (IsInstalled(model))
        {
            progress?.Report(new SetupProgress("install", $"Whisper モデル {model.DisplayName} はすでに準備されています。"));
            return modelPath;
        }

        progress?.Report(new SetupProgress("download", $"Whisper モデル {model.DisplayName} をダウンロードしています。"));

        await using var modelStream = await WhisperGgmlDownloader.Default
            .GetGgmlModelAsync(model.ModelType, QuantizationType.NoQuantization, cancellationToken);

        await using var fileStream = File.Create(modelPath);
        await modelStream.CopyToAsync(fileStream, cancellationToken);

        progress?.Report(new SetupProgress("complete", $"Whisper モデル {model.DisplayName} の準備が完了しました。"));
        return modelPath;
    }
}
