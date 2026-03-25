using System.Threading.Channels;
using KikisenApp.Core;
using NAudio.Wave;

namespace KikisenApp.Desktop;

public sealed class RealtimeSpeechBridge : IAsyncDisposable
{
    private readonly WhisperSpeechRecognizer _recognizer;
    private readonly WhisperModelDefinition _model;
    private readonly WhisperSettings _settings;
    private readonly int _deviceNumber;
    private readonly object _bufferLock = new();

    private Channel<byte[]>? _segments;
    private WaveInEvent? _waveIn;
    private MemoryStream? _pcmBuffer;
    private CancellationTokenSource? _cancellationTokenSource;
    private Task? _segmentTask;
    private Task? _processTask;
    private string _lastRecognizedText = string.Empty;

    public RealtimeSpeechBridge(
        WhisperSpeechRecognizer recognizer,
        WhisperModelDefinition model,
        WhisperSettings settings,
        int deviceNumber)
    {
        _recognizer = recognizer;
        _model = model;
        _settings = settings;
        _deviceNumber = deviceNumber;
    }

    public event Action<string>? Recognized;

    public event Action? SilenceDetected;

    public event Action<string>? StatusChanged;

    public bool IsRunning => _waveIn is not null;

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (IsRunning)
        {
            return;
        }

        await _recognizer.EnsureReadyAsync(_model, cancellationToken: cancellationToken);

        _cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _segments = Channel.CreateUnbounded<byte[]>();
        _pcmBuffer = new MemoryStream();
        _lastRecognizedText = string.Empty;

        _waveIn = new WaveInEvent
        {
            DeviceNumber = _deviceNumber,
            BufferMilliseconds = 200,
            NumberOfBuffers = 3,
            WaveFormat = new WaveFormat(16000, 16, 1)
        };
        _waveIn.DataAvailable += OnDataAvailable;

        _segmentTask = RunSegmentLoopAsync(_cancellationTokenSource.Token);
        _processTask = RunProcessLoopAsync(_cancellationTokenSource.Token);

        _waveIn.StartRecording();
        StatusChanged?.Invoke("Whisper の聞き取りを開始しました。");
    }

    public async Task StopAsync()
    {
        if (!IsRunning)
        {
            return;
        }

        _cancellationTokenSource?.Cancel();

        if (_waveIn is not null)
        {
            _waveIn.DataAvailable -= OnDataAvailable;
            _waveIn.StopRecording();
            _waveIn.Dispose();
            _waveIn = null;
        }

        if (_segmentTask is not null)
        {
            await SafeAwaitAsync(_segmentTask);
            _segmentTask = null;
        }

        _segments?.Writer.TryComplete();

        if (_processTask is not null)
        {
            await SafeAwaitAsync(_processTask);
            _processTask = null;
        }

        _pcmBuffer?.Dispose();
        _pcmBuffer = null;
        _segments = null;
        _cancellationTokenSource?.Dispose();
        _cancellationTokenSource = null;

        StatusChanged?.Invoke("Whisper の聞き取りを停止しました。");
    }

    private void OnDataAvailable(object? sender, WaveInEventArgs e)
    {
        if (_pcmBuffer is null)
        {
            return;
        }

        lock (_bufferLock)
        {
            _pcmBuffer.Write(e.Buffer, 0, e.BytesRecorded);
        }
    }

    private async Task RunSegmentLoopAsync(CancellationToken cancellationToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(Math.Max(1000, _settings.SegmentDurationMilliseconds)));

        while (await timer.WaitForNextTickAsync(cancellationToken))
        {
            byte[] pcmData;

            lock (_bufferLock)
            {
                if (_pcmBuffer is null || _pcmBuffer.Length == 0)
                {
                    SilenceDetected?.Invoke();
                    continue;
                }

                pcmData = _pcmBuffer.ToArray();
                _pcmBuffer.SetLength(0);
            }

            if (pcmData.Length == 0 || !HasSpeech(pcmData, _settings.SilenceThreshold))
            {
                SilenceDetected?.Invoke();
                continue;
            }

            if (_segments is null)
            {
                break;
            }

            await _segments.Writer.WriteAsync(CreateWaveBytes(pcmData), cancellationToken);
        }
    }

    private async Task RunProcessLoopAsync(CancellationToken cancellationToken)
    {
        if (_segments is null)
        {
            return;
        }

        await foreach (var waveBytes in _segments.Reader.ReadAllAsync(cancellationToken))
        {
            try
            {
                await using var stream = new MemoryStream(waveBytes, writable: false);
                StatusChanged?.Invoke("Whisper で聞き取り中です。");

                var text = await _recognizer.TranscribeAsync(_model, stream, cancellationToken);
                if (string.IsNullOrWhiteSpace(text))
                {
                    continue;
                }

                text = text.Trim();
                if (string.Equals(text, _lastRecognizedText, StringComparison.Ordinal))
                {
                    continue;
                }

                _lastRecognizedText = text;
                Recognized?.Invoke(text);
                StatusChanged?.Invoke("Whisper で聞き取った内容を整理しています。");
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                StatusChanged?.Invoke($"Whisper の聞き取りに失敗しました: {ex.Message}");
            }
        }
    }

    private static bool HasSpeech(byte[] pcmData, float threshold)
    {
        if (pcmData.Length < 2)
        {
            return false;
        }

        double total = 0;
        var sampleCount = pcmData.Length / 2;

        for (var index = 0; index < pcmData.Length - 1; index += 2)
        {
            var sample = BitConverter.ToInt16(pcmData, index);
            total += Math.Abs(sample) / 32768d;
        }

        return total / sampleCount >= threshold;
    }

    private static byte[] CreateWaveBytes(byte[] pcmData)
    {
        using var output = new MemoryStream();
        using (var writer = new WaveFileWriter(output, new WaveFormat(16000, 16, 1)))
        {
            writer.Write(pcmData, 0, pcmData.Length);
        }

        return output.ToArray();
    }

    private static async Task SafeAwaitAsync(Task task)
    {
        try
        {
            await task;
        }
        catch (OperationCanceledException)
        {
        }
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync();
    }
}
