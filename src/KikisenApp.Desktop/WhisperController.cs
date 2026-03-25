using System.Text;
using KikisenApp.Core;
using NAudio.Wave;

namespace KikisenApp.Desktop;

public sealed class WhisperController : IAsyncDisposable
{
    private static readonly char[] SentenceEndings = ['。', '！', '!', '？', '?', '.'];

    private readonly AppSettings _settings;
    private readonly WhisperModelInstaller _modelInstaller;
    private readonly WhisperSpeechRecognizer _recognizer;
    private readonly StringBuilder _pendingText = new();

    private RealtimeSpeechBridge? _bridge;
    private DateTimeOffset? _lastRecognizedAt;

    public WhisperController(AppSettings settings, AppPaths paths)
    {
        _settings = settings;
        _modelInstaller = new WhisperModelInstaller(paths);
        _recognizer = new WhisperSpeechRecognizer(_modelInstaller);
    }

    public event Action<string>? StatusChanged;

    public event Action<bool>? RunningStateChanged;

    public event Action<string>? SentenceReady;

    public bool IsRunning => _bridge?.IsRunning == true;

    public IReadOnlyList<WhisperModelDefinition> GetModels()
    {
        return WhisperModelCatalog.GetAll();
    }

    public bool IsModelInstalled(string modelId)
    {
        return _modelInstaller.IsInstalled(WhisperModelCatalog.GetById(modelId));
    }

    public WhisperModelDefinition GetSelectedModel()
    {
        return WhisperModelCatalog.GetById(_settings.Whisper.SelectedModelId);
    }

    public IReadOnlyList<AudioInputDeviceItem> GetInputDevices()
    {
        var devices = new List<AudioInputDeviceItem>();
        for (var index = 0; index < WaveIn.DeviceCount; index++)
        {
            var capabilities = WaveIn.GetCapabilities(index);
            devices.Add(new AudioInputDeviceItem(index, capabilities.ProductName));
        }

        return devices;
    }

    public async Task DownloadModelAsync(
        string modelId,
        IProgress<SetupProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var model = WhisperModelCatalog.GetById(modelId);
        await _recognizer.EnsureReadyAsync(model, progress, cancellationToken);
        StatusChanged?.Invoke($"Whisper モデル {model.DisplayName} の準備が完了しました。");
    }

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (IsRunning)
        {
            return;
        }

        var model = GetSelectedModel();
        await _recognizer.EnsureReadyAsync(model, cancellationToken: cancellationToken);

        var inputDevice = ResolveInputDevice();
        if (inputDevice is null)
        {
            throw new InvalidOperationException("Whisper で使う入力デバイスが見つかりません。Whisper タブで選んでください。");
        }

        _bridge = new RealtimeSpeechBridge(_recognizer, model, _settings.Whisper, inputDevice.DeviceNumber);
        _bridge.Recognized += OnRecognized;
        _bridge.SilenceDetected += OnSilenceDetected;
        _bridge.StatusChanged += message => StatusChanged?.Invoke(message);

        await _bridge.StartAsync(cancellationToken);
        StatusChanged?.Invoke($"Whisper を開始しました: {inputDevice.Name}");
        RunningStateChanged?.Invoke(true);
    }

    public async Task StopAsync()
    {
        if (_bridge is null)
        {
            return;
        }

        await FlushPendingSentenceAsync(force: true);

        _bridge.Recognized -= OnRecognized;
        _bridge.SilenceDetected -= OnSilenceDetected;
        await _bridge.DisposeAsync();
        _bridge = null;

        StatusChanged?.Invoke("Whisper を停止しました。");
        RunningStateChanged?.Invoke(false);
    }

    private AudioInputDeviceItem? ResolveInputDevice()
    {
        var devices = GetInputDevices();
        return devices.FirstOrDefault(x => string.Equals(x.Name, _settings.Whisper.SelectedInputDeviceName, StringComparison.OrdinalIgnoreCase))
            ?? devices.FirstOrDefault();
    }

    private void OnRecognized(string text)
    {
        _lastRecognizedAt = DateTimeOffset.Now;

        if (_pendingText.Length > 0)
        {
            _pendingText.Append(' ');
        }

        _pendingText.Append(text.Trim());
        FlushCompleteSentences();
    }

    private void OnSilenceDetected()
    {
        _ = FlushPendingSentenceAsync(force: false);
    }

    private async Task FlushPendingSentenceAsync(bool force)
    {
        if (_pendingText.Length == 0)
        {
            return;
        }

        if (!force)
        {
            if (_lastRecognizedAt is null)
            {
                return;
            }

            var elapsed = DateTimeOffset.Now - _lastRecognizedAt.Value;
            if (elapsed.TotalMilliseconds < Math.Max(1000, _settings.Whisper.FlushAfterSilenceMilliseconds))
            {
                return;
            }
        }

        var sentence = _pendingText.ToString().Trim();
        _pendingText.Clear();

        if (!string.IsNullOrWhiteSpace(sentence))
        {
            SentenceReady?.Invoke(sentence);
            StatusChanged?.Invoke($"Whisper の結果を読み上げます: {sentence}");
        }

        await Task.CompletedTask;
    }

    private void FlushCompleteSentences()
    {
        var text = _pendingText.ToString();
        var lastBoundary = text.LastIndexOfAny(SentenceEndings);
        if (lastBoundary < 0)
        {
            return;
        }

        var completed = text[..(lastBoundary + 1)].Trim();
        var remaining = text[(lastBoundary + 1)..].TrimStart();

        _pendingText.Clear();
        if (!string.IsNullOrWhiteSpace(remaining))
        {
            _pendingText.Append(remaining);
        }

        foreach (var sentence in SplitCompletedSentences(completed))
        {
            SentenceReady?.Invoke(sentence);
            StatusChanged?.Invoke($"Whisper の結果を読み上げます: {sentence}");
        }
    }

    private static IEnumerable<string> SplitCompletedSentences(string text)
    {
        var buffer = new StringBuilder();
        foreach (var character in text)
        {
            buffer.Append(character);
            if (!SentenceEndings.Contains(character))
            {
                continue;
            }

            var sentence = buffer.ToString().Trim();
            buffer.Clear();

            if (!string.IsNullOrWhiteSpace(sentence))
            {
                yield return sentence;
            }
        }

        var trailing = buffer.ToString().Trim();
        if (!string.IsNullOrWhiteSpace(trailing))
        {
            yield return trailing;
        }
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync();
        await _recognizer.DisposeAsync();
    }

    public sealed record AudioInputDeviceItem(int DeviceNumber, string Name)
    {
        public override string ToString() => Name;
    }
}
