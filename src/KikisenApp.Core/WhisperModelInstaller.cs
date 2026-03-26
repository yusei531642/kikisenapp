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
            var existingSize = new FileInfo(modelPath).Length;
            progress?.Report(new SetupProgress("install", $"Whisper モデル {model.DisplayName} はすでに準備されています。", existingSize, existingSize));
            return modelPath;
        }

        progress?.Report(new SetupProgress("download", $"Whisper モデル {model.DisplayName} をダウンロードしています。", 0, model.ApproximateSizeBytes));

        await using var modelStream = await WhisperGgmlDownloader.Default
            .GetGgmlModelAsync(model.ModelType, QuantizationType.NoQuantization, cancellationToken);

        await using var fileStream = File.Create(modelPath);
        var buffer = new byte[1024 * 1024];
        long receivedBytes = 0;

        while (true)
        {
            var read = await modelStream.ReadAsync(buffer, cancellationToken);
            if (read == 0)
            {
                break;
            }

            await fileStream.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
            receivedBytes += read;
            progress?.Report(new SetupProgress("download", $"Whisper モデル {model.DisplayName} をダウンロードしています。", receivedBytes, model.ApproximateSizeBytes));
        }

        progress?.Report(new SetupProgress("complete", $"Whisper モデル {model.DisplayName} の準備が完了しました。", receivedBytes, receivedBytes));
        return modelPath;
    }
}
