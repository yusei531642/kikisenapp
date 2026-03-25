using Whisper.net;

namespace KikisenApp.Core;

public sealed class WhisperSpeechRecognizer : IAsyncDisposable
{
    private readonly WhisperModelInstaller _modelInstaller;
    private readonly SemaphoreSlim _lock = new(1, 1);

    private WhisperFactory? _factory;
    private WhisperProcessor? _processor;
    private string? _loadedModelId;

    public WhisperSpeechRecognizer(WhisperModelInstaller modelInstaller)
    {
        _modelInstaller = modelInstaller;
    }

    public async Task EnsureReadyAsync(
        WhisperModelDefinition model,
        IProgress<SetupProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (_processor is not null && string.Equals(_loadedModelId, model.Id, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        await _lock.WaitAsync(cancellationToken);
        try
        {
            if (_processor is not null && string.Equals(_loadedModelId, model.Id, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            await DisposeProcessorAsync();

            var modelPath = await _modelInstaller.EnsureInstalledAsync(model, progress, cancellationToken);
            _factory = WhisperFactory.FromPath(modelPath);
            _processor = _factory.CreateBuilder()
                .WithLanguage("ja")
                .WithNoContext()
                .WithSingleSegment()
                .WithThreads(Math.Max(2, Environment.ProcessorCount / 2))
                .Build();
            _loadedModelId = model.Id;
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<string> TranscribeAsync(
        WhisperModelDefinition model,
        Stream waveStream,
        CancellationToken cancellationToken = default)
    {
        await EnsureReadyAsync(model, cancellationToken: cancellationToken);

        await _lock.WaitAsync(cancellationToken);
        try
        {
            waveStream.Position = 0;

            var segments = new List<string>();
            await foreach (var segment in _processor!.ProcessAsync(waveStream, cancellationToken))
            {
                if (!string.IsNullOrWhiteSpace(segment.Text))
                {
                    segments.Add(segment.Text.Trim());
                }
            }

            return string.Join(string.Empty, segments).Trim();
        }
        finally
        {
            _lock.Release();
        }
    }

    private async Task DisposeProcessorAsync()
    {
        if (_processor is not null)
        {
            await _processor.DisposeAsync();
            _processor = null;
        }

        _factory?.Dispose();
        _factory = null;
        _loadedModelId = null;
    }

    public async ValueTask DisposeAsync()
    {
        await DisposeProcessorAsync();
        _lock.Dispose();
    }
}
