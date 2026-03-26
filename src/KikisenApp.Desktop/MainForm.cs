using KikisenApp.Core;
using NAudio.Wave;
using System.Diagnostics;
using System.Reflection;
using System.Threading.Channels;

namespace KikisenApp.Desktop;

public sealed class MainForm : Form
{
    private readonly AppPaths _paths = AppPaths.CreateDefault();
    private readonly AppSettingsStore _settingsStore;
    private readonly HttpClient _engineHttpClient;
    private readonly HttpClient _setupHttpClient;
    private readonly VbCableInstaller _vbCableInstaller;
    private readonly AppUpdateService _appUpdateService;
    private readonly SpeechPlaybackService _playbackService = new();
    private readonly Channel<string> _whisperSpeechQueue = Channel.CreateUnbounded<string>();

    private AppSettings _settings = new();
    private WhisperController? _whisperController;
    private CancellationTokenSource? _whisperQueueCancellationTokenSource;
    private Task? _whisperQueueTask;
    private bool _isCheckingForAppUpdates;
    private bool _isForcingMaximized;

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
        _appUpdateService = new AppUpdateService(_setupHttpClient, _paths);

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
        EnsureWhisperController();
        StartWhisperQueueProcessor();
        ApplyWindowSettings();
        await RefreshVoicevoxStatusAsync();
        _ = CheckForAppUpdatesOnStartupAsync();
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        _playbackService.Dispose();
        _whisperSpeechQueue.Writer.TryComplete();
        _whisperQueueCancellationTokenSource?.Cancel();
        _whisperQueueTask?.Wait(TimeSpan.FromSeconds(3));
        _whisperController?.DisposeAsync().AsTask().GetAwaiter().GetResult();
        _whisperQueueCancellationTokenSource?.Dispose();
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
        EnsureWhisperController();

        using var form = new SettingsForm(
            _settingsStore,
            _settings,
            _engineHttpClient,
            _vbCableInstaller,
            _whisperController!,
            _appUpdateService);

        form.ShowDialog(this);
        ApplyWindowSettings();
    }

    private async Task SpeakAsync()
    {
        try
        {
            await SpeakTextAsync(_speechTextBox.Text, updateTextBox: false);
        }
        catch (Exception ex)
        {
            _statusLabel.Text = "送信に失敗しました。";
            MessageBox.Show(this, ex.Message, "送信失敗", MessageBoxButtons.OK, MessageBoxIcon.Error);
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

    private VoicevoxApiClient CreateApiClient()
    {
        return new VoicevoxApiClient(_engineHttpClient);
    }

    private async Task SpeakTextAsync(string rawText, bool updateTextBox)
    {
        var text = _settings.VoiceTuning.NormalizeText
            ? VoiceTextFormatter.NormalizeForSpeech(rawText)
            : rawText.Trim();

        if (string.IsNullOrWhiteSpace(text))
        {
            throw new InvalidOperationException("読み上げる文字がありません。");
        }

        if (updateTextBox)
        {
            SetSpeechTextSafe(text);
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
            _playbackService.Play(synthesized.WaveBytes, outputDevice.DeviceNumber, _settings.MonitorSpeechLocally);
            _statusLabel.Text = _settings.MonitorSpeechLocally
                ? $"送信しました: {outputDevice.Name} / 自分のスピーカーでも再生中"
                : $"送信しました: {outputDevice.Name}";
        }
        finally
        {
            _speakButton.Enabled = true;
        }
    }

    private async Task<VoicevoxApiClient> EnsureEngineReadyAsync()
    {
        var client = CreateApiClient();
        if (await client.IsEngineAvailableAsync())
        {
            return client;
        }

        throw new InvalidOperationException($"VOICEVOX に接続できません。VOICEVOX を自分で起動して、設定画面の API URL を確認してください。現在: {_settings.EngineBaseUrl}");
    }

    private async Task RefreshVoicevoxStatusAsync()
    {
        try
        {
            var client = CreateApiClient();
            if (await client.IsEngineAvailableAsync())
            {
                _statusLabel.Text = "VOICEVOX につながっています。文字を入れて送信できます。";
                return;
            }
        }
        catch
        {
        }

        _statusLabel.Text = $"VOICEVOX につながっていません。設定で API URL を確認してください。現在: {_settings.EngineBaseUrl}";
    }

    private void SetStatusSafe(string text)
    {
        if (IsDisposed)
        {
            return;
        }

        if (InvokeRequired)
        {
            BeginInvoke(() => _statusLabel.Text = text);
            return;
        }

        _statusLabel.Text = text;
    }

    private void SetSpeechTextSafe(string text)
    {
        if (IsDisposed)
        {
            return;
        }

        if (InvokeRequired)
        {
            BeginInvoke(() => _speechTextBox.Text = text);
            return;
        }

        _speechTextBox.Text = text;
    }

    private void EnsureWhisperController()
    {
        if (_whisperController is not null)
        {
            return;
        }

        _whisperController = new WhisperController(_settings, _paths);
        _whisperController.StatusChanged += SetStatusSafe;
        _whisperController.SentenceReady += sentence => _whisperSpeechQueue.Writer.TryWrite(sentence);
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);

        if (!_settings.KeepMainWindowMaximized || WindowState != FormWindowState.Normal || _isForcingMaximized)
        {
            return;
        }

        _isForcingMaximized = true;
        BeginInvoke(() =>
        {
            try
            {
                if (!IsDisposed && _settings.KeepMainWindowMaximized && WindowState == FormWindowState.Normal)
                {
                    WindowState = FormWindowState.Maximized;
                }
            }
            finally
            {
                _isForcingMaximized = false;
            }
        });
    }

    private void ApplyWindowSettings()
    {
        if (_settings.KeepMainWindowMaximized && WindowState != FormWindowState.Minimized)
        {
            WindowState = FormWindowState.Maximized;
        }
    }

    private async Task CheckForAppUpdatesOnStartupAsync()
    {
        if (!_settings.AutoCheckForAppUpdates || _isCheckingForAppUpdates)
        {
            return;
        }

        _isCheckingForAppUpdates = true;

        try
        {
            var update = await _appUpdateService.CheckForUpdatesAsync(GetCurrentVersions());
            if (!update.UpdateAvailable)
            {
                return;
            }

            if (IsDisposed)
            {
                return;
            }

            BeginInvoke(async () =>
            {
                var result = MessageBox.Show(
                    this,
                    $"最新版 {update.LatestVersion} が見つかりました。今すぐ更新しますか？",
                    "KikisenApp 更新",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Information);

                if (result != DialogResult.Yes)
                {
                    SetStatusSafe($"最新版 {update.LatestVersion} が公開されています。設定の About からも確認できます。");
                    return;
                }

                await InstallAppUpdateAsync(update);
            });
        }
        catch (Exception ex)
        {
            SetStatusSafe($"更新確認は失敗しましたが、アプリはそのまま使えます: {ex.Message}");
        }
        finally
        {
            _isCheckingForAppUpdates = false;
        }
    }

    private async Task InstallAppUpdateAsync(AppUpdateCheckResult update)
    {
        try
        {
            if (update.InstallerAsset is null)
            {
                OpenUrl(update.ReleasePageUrl);
                return;
            }

            var progress = new Progress<SetupProgress>(info => SetStatusSafe(info.Message));
            var installerPath = await _appUpdateService.DownloadInstallerAsync(update, progress);

            Process.Start(new ProcessStartInfo
            {
                FileName = installerPath,
                WorkingDirectory = Path.GetDirectoryName(installerPath) ?? AppContext.BaseDirectory,
                UseShellExecute = true
            });

            SetStatusSafe("最新版のセットアップを起動しました。");
            BeginInvoke(Close);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "更新失敗", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void StartWhisperQueueProcessor()
    {
        if (_whisperQueueTask is not null)
        {
            return;
        }

        _whisperQueueCancellationTokenSource = new CancellationTokenSource();
        _whisperQueueTask = Task.Run(() => ProcessWhisperSpeechQueueAsync(_whisperQueueCancellationTokenSource.Token));
    }

    private async Task ProcessWhisperSpeechQueueAsync(CancellationToken cancellationToken)
    {
        await foreach (var sentence in _whisperSpeechQueue.Reader.ReadAllAsync(cancellationToken))
        {
            try
            {
                await SpeakTextAsync(sentence, updateTextBox: true);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                SetStatusSafe($"Whisper の読み上げに失敗しました: {ex.Message}");
            }
        }
    }

    private sealed record AudioDeviceItem(int DeviceNumber, string Name);

    private static string GetCurrentVersion()
    {
        return Application.ProductVersion;
    }

    private static IReadOnlyList<string?> GetCurrentVersions()
    {
        var versions = new List<string?>();

        versions.Add(Application.ProductVersion);

        var fileVersionInfo = FileVersionInfo.GetVersionInfo(Application.ExecutablePath);
        versions.Add(fileVersionInfo.ProductVersion);
        versions.Add(fileVersionInfo.FileVersion);

        var entryAssembly = Assembly.GetEntryAssembly();
        versions.Add(entryAssembly?.GetName().Version?.ToString());

        var informationalVersion = entryAssembly?
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion;

        if (!string.IsNullOrWhiteSpace(informationalVersion))
        {
            versions.Add(informationalVersion.Split('+')[0]);
        }

        return versions
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static void OpenUrl(string url)
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = url,
            UseShellExecute = true
        });
    }
}
