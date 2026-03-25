using KikisenApp.Core;
using NAudio.Wave;

namespace KikisenApp.Desktop;

public sealed class MainForm : Form
{
    private readonly AppPaths _paths = AppPaths.CreateDefault();
    private readonly AppSettingsStore _settingsStore;
    private readonly HttpClient _engineHttpClient;
    private readonly HttpClient _setupHttpClient;
    private readonly VbCableInstaller _vbCableInstaller;
    private readonly VoicevoxEngineProcessManager _processManager = new();

    private AppSettings _settings = new();
    private IWavePlayer? _waveOut;
    private WaveStream? _currentWaveStream;
    private MemoryStream? _currentAudioStream;

    private readonly TextBox _speechTextBox = new();
    private readonly Button _speakButton = new();
    private readonly Button _settingsButton = new();
    private readonly Label _statusLabel = new();

    public MainForm()
    {
        _paths.EnsureDirectories();
        _settingsStore = new AppSettingsStore(_paths);

        _engineHttpClient = new HttpClient
        {
            BaseAddress = new Uri("http://127.0.0.1:50021/")
        };

        _setupHttpClient = new HttpClient();
        _vbCableInstaller = new VbCableInstaller(_setupHttpClient, _paths);

        Text = "KikisenApp";
        Icon = System.Drawing.Icon.ExtractAssociatedIcon(Application.ExecutablePath);
        StartPosition = FormStartPosition.CenterScreen;
        Size = new Size(560, 300);
        MinimumSize = new Size(420, 220);
        FormBorderStyle = FormBorderStyle.Sizable;
        MaximizeBox = true;
        MinimizeBox = true;
        Font = new Font("Meiryo UI", 9F, FontStyle.Regular, GraphicsUnit.Point);

        BuildUi();
    }

    protected override async void OnLoad(EventArgs e)
    {
        base.OnLoad(e);

        _settings = await _settingsStore.LoadAsync();
        _engineHttpClient.BaseAddress = new Uri($"{_settings.EngineBaseUrl.TrimEnd('/')}/");
        _statusLabel.Text = "VOICEVOX を確認しています。";

        await TryAutoStartInstalledEngineAsync();
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        StopPlayback();
        _processManager.Stop();
        _engineHttpClient.Dispose();
        _setupHttpClient.Dispose();
        base.OnFormClosing(e);
    }

    private void BuildUi()
    {
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            Padding = new Padding(12)
        };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));

        var titleLabel = new Label
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            Text = "入力して送信するだけの画面です。",
            TextAlign = ContentAlignment.MiddleLeft
        };

        _speechTextBox.Multiline = true;
        _speechTextBox.Dock = DockStyle.Fill;
        _speechTextBox.ScrollBars = ScrollBars.Vertical;
        _speechTextBox.Font = new Font("Meiryo UI", 10F, FontStyle.Regular, GraphicsUnit.Point);
        _speechTextBox.Text = "こんにちは。";

        _settingsButton.Text = "設定";
        _settingsButton.AutoSize = true;
        _settingsButton.Click += (_, _) => OpenSettingsWindow();

        _speakButton.Text = "送信";
        _speakButton.AutoSize = true;
        _speakButton.Click += async (_, _) => await SpeakAsync();

        _statusLabel.Dock = DockStyle.Fill;
        _statusLabel.TextAlign = ContentAlignment.MiddleLeft;
        _statusLabel.AutoEllipsis = true;

        var bottomPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 3
        };
        bottomPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        bottomPanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        bottomPanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        bottomPanel.Controls.Add(_statusLabel, 0, 0);
        bottomPanel.Controls.Add(_settingsButton, 1, 0);
        bottomPanel.Controls.Add(_speakButton, 2, 0);

        layout.Controls.Add(titleLabel, 0, 0);
        layout.Controls.Add(_speechTextBox, 0, 1);
        layout.Controls.Add(bottomPanel, 0, 2);

        Controls.Add(layout);
        AcceptButton = _speakButton;
    }

    private void OpenSettingsWindow()
    {
        using var form = new SettingsForm(
            _paths,
            _settingsStore,
            _settings,
            _engineHttpClient,
            _vbCableInstaller,
            _processManager);

        form.ShowDialog(this);
    }

    private async Task SpeakAsync()
    {
        var rawText = _speechTextBox.Text;
        var text = _settings.VoiceTuning.NormalizeText
            ? VoiceTextFormatter.NormalizeForSpeech(rawText)
            : rawText.Trim();

        if (string.IsNullOrWhiteSpace(text))
        {
            MessageBox.Show(this, "読み上げる文字を入力してください。", "未入力", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        _speakButton.Enabled = false;
        _statusLabel.Text = "送信中です。";

        try
        {
            var client = await EnsureEngineReadyAsync();

            var speakerId = await ResolveSpeakerIdAsync(client);
            var outputDevice = FindPreferredOutputDevice();
            if (outputDevice is null)
            {
                throw new InvalidOperationException("再生先デバイスが見つかりません。設定画面で選んでください。");
            }

            var synthesized = await client.SynthesizeAsync(text, speakerId, _settings.VoiceTuning);
            PlayWave(synthesized.WaveBytes, outputDevice.DeviceNumber);
            _statusLabel.Text = $"送信しました: {outputDevice.Name}";
        }
        catch (Exception ex)
        {
            _statusLabel.Text = "送信に失敗しました。";
            MessageBox.Show(this, ex.Message, "送信失敗", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            _speakButton.Enabled = true;
        }
    }

    private async Task<int> ResolveSpeakerIdAsync(VoicevoxApiClient client)
    {
        var speakers = await client.GetSpeakersAsync();
        var styles = speakers.SelectMany(x => x.Styles).ToList();

        var selected = styles.FirstOrDefault(x => x.Id == _settings.SelectedSpeakerId) ?? styles.FirstOrDefault();
        if (selected is null)
        {
            throw new InvalidOperationException("使える話者が見つかりません。設定画面で確認してください。");
        }

        if (selected.Id != _settings.SelectedSpeakerId)
        {
            _settings.SelectedSpeakerId = selected.Id;
            await _settingsStore.SaveAsync(_settings);
        }

        return selected.Id;
    }

    private AudioDeviceItem? FindPreferredOutputDevice()
    {
        var devices = EnumerateAudioDevices();
        return devices.FirstOrDefault(x => string.Equals(x.Name, _settings.SelectedOutputDeviceName, StringComparison.OrdinalIgnoreCase))
            ?? devices.FirstOrDefault(x => x.Name.Contains("CABLE Input", StringComparison.OrdinalIgnoreCase))
            ?? devices.FirstOrDefault();
    }

    private List<AudioDeviceItem> EnumerateAudioDevices()
    {
        var devices = new List<AudioDeviceItem>();
        for (var i = 0; i < WaveOut.DeviceCount; i++)
        {
            var capabilities = WaveOut.GetCapabilities(i);
            devices.Add(new AudioDeviceItem(i, capabilities.ProductName));
        }

        return devices;
    }

    private void PlayWave(byte[] waveBytes, int deviceNumber)
    {
        StopPlayback();

        _currentAudioStream = new MemoryStream(waveBytes);
        _currentWaveStream = new WaveFileReader(_currentAudioStream);
        _waveOut = new WaveOutEvent
        {
            DeviceNumber = deviceNumber
        };
        _waveOut.PlaybackStopped += (_, _) => _statusLabel.Text = "再生が終わりました。";
        _waveOut.Init(_currentWaveStream);
        _waveOut.Play();
    }

    private void StopPlayback()
    {
        _waveOut?.Stop();
        _waveOut?.Dispose();
        _waveOut = null;

        _currentWaveStream?.Dispose();
        _currentWaveStream = null;

        _currentAudioStream?.Dispose();
        _currentAudioStream = null;
    }

    private VoicevoxApiClient CreateApiClient()
    {
        return new VoicevoxApiClient(_engineHttpClient);
    }

    private async Task<VoicevoxApiClient> EnsureEngineReadyAsync()
    {
        var client = CreateApiClient();
        if (await client.IsEngineAvailableAsync())
        {
            return client;
        }

        var runExecutablePath = FindInstalledRunExecutablePath();
        if (runExecutablePath is null)
        {
            throw new InvalidOperationException("VOICEVOX ENGINE が見つかりません。setup か設定画面からセットアップしてください。");
        }

        _statusLabel.Text = "VOICEVOX を起動しています。";
        var startedNow = await _processManager.StartAsync(
            runExecutablePath,
            _settings.EngineBaseUrl,
            CreateApiClient);

        await RememberInstalledEngineAsync(runExecutablePath);
        _statusLabel.Text = startedNow
            ? "VOICEVOX を起動しました。"
            : "VOICEVOX はすでに起動しています。";

        return CreateApiClient();
    }

    private async Task TryAutoStartInstalledEngineAsync()
    {
        try
        {
            var client = await EnsureEngineReadyAsync();
            if (await client.IsEngineAvailableAsync())
            {
                _statusLabel.Text = "文字を入れて送信できます。細かい設定は右下の設定から開けます。";
            }
        }
        catch
        {
            _statusLabel.Text = "文字を入れて送信できます。細かい設定は右下の設定から開けます。";
        }
    }

    private string? FindInstalledRunExecutablePath()
    {
        if (!string.IsNullOrWhiteSpace(_settings.InstalledEnginePath) && Directory.Exists(_settings.InstalledEnginePath))
        {
            var configuredPath = Directory
                .EnumerateFiles(_settings.InstalledEnginePath, "run.exe", SearchOption.AllDirectories)
                .FirstOrDefault();

            if (configuredPath is not null)
            {
                return configuredPath;
            }
        }

        return Directory.Exists(_paths.EngineDirectory)
            ? Directory.EnumerateFiles(_paths.EngineDirectory, "run.exe", SearchOption.AllDirectories).FirstOrDefault()
            : null;
    }

    private async Task RememberInstalledEngineAsync(string runExecutablePath)
    {
        var engineDirectory = Path.GetDirectoryName(runExecutablePath);
        if (string.IsNullOrWhiteSpace(engineDirectory))
        {
            return;
        }

        if (string.Equals(_settings.InstalledEnginePath, engineDirectory, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        _settings.InstalledEnginePath = engineDirectory;
        await _settingsStore.SaveAsync(_settings);
    }

    private sealed record AudioDeviceItem(int DeviceNumber, string Name);
}
