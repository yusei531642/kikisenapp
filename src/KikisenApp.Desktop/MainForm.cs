using System.Diagnostics;
using KikisenApp.Core;
using NAudio.Wave;

namespace KikisenApp.Desktop;

public sealed class MainForm : Form
{
    private readonly AppPaths _paths = AppPaths.CreateDefault();
    private readonly AppSettingsStore _settingsStore;
    private readonly HttpClient _engineHttpClient;
    private readonly HttpClient _githubHttpClient;
    private readonly VoicevoxEngineInstaller _installer;
    private readonly VoicevoxEngineProcessManager _processManager = new();

    private AppSettings _settings = new();
    private List<SpeakerItem> _speakerItems = [];
    private IWavePlayer? _waveOut;
    private WaveStream? _currentWaveStream;
    private MemoryStream? _currentAudioStream;

    private readonly TextBox _speechTextBox = new();
    private readonly ComboBox _speakerComboBox = new();
    private readonly ComboBox _outputDeviceComboBox = new();
    private readonly NumericUpDown _speedScaleUpDown = new();
    private readonly NumericUpDown _pitchScaleUpDown = new();
    private readonly NumericUpDown _intonationScaleUpDown = new();
    private readonly NumericUpDown _volumeScaleUpDown = new();
    private readonly CheckBox _normalizeTextCheckBox = new();
    private readonly Button _speakButton = new();
    private readonly Button _stopButton = new();
    private readonly Button _saveSettingsButton = new();
    private readonly Label _speechStatusLabel = new();
    private readonly TextBox _setupLogTextBox = new();
    private readonly ProgressBar _setupProgressBar = new();
    private readonly Label _setupStatusLabel = new();
    private readonly Button _installVoicevoxButton = new();
    private readonly Button _startVoicevoxButton = new();
    private readonly Button _openVbCableButton = new();
    private readonly Button _reloadDevicesButton = new();
    private readonly TextBox _guideTextBox = new();

    public MainForm()
    {
        _paths.EnsureDirectories();
        _settingsStore = new AppSettingsStore(_paths);

        _engineHttpClient = new HttpClient
        {
            BaseAddress = new Uri("http://127.0.0.1:50021/")
        };

        _githubHttpClient = new HttpClient();
        _installer = new VoicevoxEngineInstaller(_githubHttpClient, _paths);

        Text = "KikisenApp - Discord 読み上げ";
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(1040, 720);
        Font = new Font("Meiryo UI", 10F, FontStyle.Regular, GraphicsUnit.Point);

        BuildUi();
    }

    protected override async void OnLoad(EventArgs e)
    {
        base.OnLoad(e);

        _settings = await _settingsStore.LoadAsync();
        _engineHttpClient.BaseAddress = new Uri($"{_settings.EngineBaseUrl.TrimEnd('/')}/");

        ApplySettingsToUi();
        ReloadAudioDevices();
        await LoadSpeakersAsync();
        UpdateSetupGuide();
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        StopPlayback();
        _processManager.Stop();
        _engineHttpClient.Dispose();
        _githubHttpClient.Dispose();
        base.OnFormClosing(e);
    }

    private void BuildUi()
    {
        var tabs = new TabControl
        {
            Dock = DockStyle.Fill
        };

        tabs.TabPages.Add(BuildSpeechTab());
        tabs.TabPages.Add(BuildSetupTab());
        tabs.TabPages.Add(BuildGuideTab());

        Controls.Add(tabs);
    }

    private TabPage BuildSpeechTab()
    {
        var page = new TabPage("しゃべる");

        var table = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 7,
            Padding = new Padding(16)
        };
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 200));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        table.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
        table.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        table.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        table.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        table.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        table.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        table.RowStyles.Add(new RowStyle(SizeType.Absolute, 78));

        _speechTextBox.Multiline = true;
        _speechTextBox.ScrollBars = ScrollBars.Vertical;
        _speechTextBox.Dock = DockStyle.Fill;
        _speechTextBox.Font = new Font("Meiryo UI", 11F, FontStyle.Regular, GraphicsUnit.Point);
        _speechTextBox.Text = "こんにちは。Discord に流す読み上げテストです。";

        _speakerComboBox.DropDownStyle = ComboBoxStyle.DropDownList;
        _speakerComboBox.Dock = DockStyle.Fill;
        _outputDeviceComboBox.DropDownStyle = ComboBoxStyle.DropDownList;
        _outputDeviceComboBox.Dock = DockStyle.Fill;

        ConfigureNumeric(_speedScaleUpDown, 0.50m, 2.00m, 1.00m, 0.05m);
        ConfigureNumeric(_pitchScaleUpDown, -0.15m, 0.15m, 0.00m, 0.01m);
        ConfigureNumeric(_intonationScaleUpDown, 0.50m, 2.00m, 1.10m, 0.05m);
        ConfigureNumeric(_volumeScaleUpDown, 0.50m, 2.50m, 1.35m, 0.05m);

        _normalizeTextCheckBox.Text = "改行や空白を読み上げ向けに整える";
        _normalizeTextCheckBox.AutoSize = true;

        _reloadDevicesButton.Text = "再読込";
        _reloadDevicesButton.AutoSize = true;
        _reloadDevicesButton.Click += (_, _) => ReloadAudioDevices();

        _saveSettingsButton.Text = "設定保存";
        _saveSettingsButton.AutoSize = true;
        _saveSettingsButton.Click += async (_, _) => await SaveSettingsFromUiAsync("設定を保存しました。");

        _speakButton.Text = "読み上げ";
        _speakButton.AutoSize = true;
        _speakButton.Click += async (_, _) => await SpeakAsync();

        _stopButton.Text = "停止";
        _stopButton.AutoSize = true;
        _stopButton.Click += (_, _) => StopPlayback();

        _speechStatusLabel.Text = "ここに状態が表示されます。";
        _speechStatusLabel.Dock = DockStyle.Fill;

        table.Controls.Add(CreateLabel("読み上げる文章"), 0, 0);
        table.Controls.Add(CreateLabel(""), 1, 0);
        table.Controls.Add(_speechTextBox, 0, 1);
        table.SetColumnSpan(_speechTextBox, 2);

        table.Controls.Add(CreateLabel("話者"), 0, 2);
        table.Controls.Add(_speakerComboBox, 1, 2);

        table.Controls.Add(CreateLabel("再生先デバイス"), 0, 3);
        table.Controls.Add(CreateRowPanel(_outputDeviceComboBox, _reloadDevicesButton), 1, 3);

        table.Controls.Add(CreateLabel("話し方"), 0, 4);
        table.Controls.Add(CreateVoiceTuningPanel(), 1, 4);

        table.Controls.Add(CreateLabel("文字の整形"), 0, 5);
        table.Controls.Add(_normalizeTextCheckBox, 1, 5);

        var actionPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight
        };
        actionPanel.Controls.Add(_speakButton);
        actionPanel.Controls.Add(_stopButton);
        actionPanel.Controls.Add(_saveSettingsButton);
        actionPanel.Controls.Add(_speechStatusLabel);

        table.Controls.Add(actionPanel, 0, 6);
        table.SetColumnSpan(actionPanel, 2);

        page.Controls.Add(table);
        return page;
    }

    private TabPage BuildSetupTab()
    {
        var page = new TabPage("セットアップ");

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 5,
            Padding = new Padding(16)
        };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        _setupStatusLabel.Text = "初回は VOICEVOX を自動セットアップしてください。";
        _setupStatusLabel.Dock = DockStyle.Fill;

        _setupProgressBar.Dock = DockStyle.Fill;

        _installVoicevoxButton.Text = "VOICEVOX を自動セットアップ";
        _installVoicevoxButton.AutoSize = true;
        _installVoicevoxButton.Click += async (_, _) => await InstallVoicevoxAsync();

        _startVoicevoxButton.Text = "VOICEVOX を起動";
        _startVoicevoxButton.AutoSize = true;
        _startVoicevoxButton.Click += async (_, _) => await StartVoicevoxAsync();

        _openVbCableButton.Text = "VB-CABLE 公式ページを開く";
        _openVbCableButton.AutoSize = true;
        _openVbCableButton.Click += (_, _) => OpenUrl(ExternalLinks.VbCablePage);

        var infoLabel = new Label
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            Text = "VB-CABLE は配布条件の都合で自動同梱していません。公式ページから入れてください。"
        };

        _setupLogTextBox.Multiline = true;
        _setupLogTextBox.ScrollBars = ScrollBars.Vertical;
        _setupLogTextBox.ReadOnly = true;
        _setupLogTextBox.Dock = DockStyle.Fill;
        _setupLogTextBox.Font = new Font("Consolas", 10F, FontStyle.Regular, GraphicsUnit.Point);

        var buttonPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight
        };
        buttonPanel.Controls.Add(_installVoicevoxButton);
        buttonPanel.Controls.Add(_startVoicevoxButton);
        buttonPanel.Controls.Add(_openVbCableButton);

        layout.Controls.Add(_setupStatusLabel, 0, 0);
        layout.Controls.Add(_setupProgressBar, 0, 1);
        layout.Controls.Add(buttonPanel, 0, 2);
        layout.Controls.Add(infoLabel, 0, 3);
        layout.Controls.Add(_setupLogTextBox, 0, 4);

        page.Controls.Add(layout);
        return page;
    }

    private TabPage BuildGuideTab()
    {
        var page = new TabPage("使い方");

        _guideTextBox.Multiline = true;
        _guideTextBox.ReadOnly = true;
        _guideTextBox.ScrollBars = ScrollBars.Vertical;
        _guideTextBox.Dock = DockStyle.Fill;
        _guideTextBox.Font = new Font("Meiryo UI", 10F, FontStyle.Regular, GraphicsUnit.Point);

        page.Controls.Add(_guideTextBox);
        return page;
    }

    private async Task InstallVoicevoxAsync()
    {
        ToggleSetupButtons(false);
        AppendSetupLog("VOICEVOX ENGINE のセットアップを開始します。");

        try
        {
            _setupProgressBar.Style = ProgressBarStyle.Marquee;
            var progress = new Progress<SetupProgress>(info =>
            {
                _setupStatusLabel.Text = info.Message;
                UpdateProgressBar(info.ReceivedBytes, info.TotalBytes);
                AppendSetupLog($"{DateTime.Now:HH:mm:ss} {info.Message}");
            });

            var result = await _installer.EnsureInstalledAsync(progress);
            _settings.InstalledEnginePath = Path.GetDirectoryName(result.RunExecutablePath);
            _settings.InstalledEngineVersion = result.Version;
            await _settingsStore.SaveAsync(_settings);

            var variantName = FormatVariantName(result.Variant);
            _setupStatusLabel.Text = $"VOICEVOX ENGINE {variantName} {result.Version} の準備ができました。";
            AppendSetupLog($"VOICEVOX ENGINE {variantName} を {result.RunExecutablePath} に準備しました。");
            UpdateSetupGuide();
        }
        catch (Exception ex)
        {
            _setupStatusLabel.Text = "セットアップに失敗しました。";
            AppendSetupLog($"エラー: {ex.Message}");
            MessageBox.Show(this, ex.Message, "セットアップ失敗", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            ToggleSetupButtons(true);
        }
    }

    private async Task StartVoicevoxAsync()
    {
        ToggleSetupButtons(false);

        try
        {
            var runExecutablePath = FindRunExecutablePath();
            if (runExecutablePath is null)
            {
                throw new InvalidOperationException("先に VOICEVOX をセットアップしてください。");
            }

            var startedNow = await _processManager.StartAsync(
                runExecutablePath,
                _settings.EngineBaseUrl,
                CreateApiClient);

            _setupStatusLabel.Text = startedNow
                ? "VOICEVOX ENGINE を起動しました。"
                : "VOICEVOX ENGINE はすでに起動しています。";

            AppendSetupLog(_setupStatusLabel.Text);
            await LoadSpeakersAsync();
        }
        catch (Exception ex)
        {
            _setupStatusLabel.Text = "VOICEVOX の起動に失敗しました。";
            AppendSetupLog($"エラー: {ex.Message}");
            MessageBox.Show(this, ex.Message, "VOICEVOX 起動失敗", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            ToggleSetupButtons(true);
        }
    }

    private async Task SpeakAsync()
    {
        var rawText = _speechTextBox.Text;
        var text = _normalizeTextCheckBox.Checked
            ? VoiceTextFormatter.NormalizeForSpeech(rawText)
            : rawText.Trim();

        if (string.IsNullOrWhiteSpace(text))
        {
            MessageBox.Show(this, "読み上げる文字を入力してください。", "未入力", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        if (_speakerComboBox.SelectedItem is not SpeakerItem speaker)
        {
            MessageBox.Show(this, "話者を選んでください。", "話者未選択", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        if (_outputDeviceComboBox.SelectedItem is not AudioDeviceItem device)
        {
            MessageBox.Show(this, "再生先デバイスを選んでください。", "デバイス未選択", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        _speakButton.Enabled = false;
        _speechStatusLabel.Text = "VOICEVOX に音声生成を依頼しています。";

        try
        {
            await SaveSettingsFromUiAsync(null);

            var client = CreateApiClient();
            if (!await client.IsEngineAvailableAsync())
            {
                throw new InvalidOperationException("VOICEVOX ENGINE が起動していません。セットアップタブから起動してください。");
            }

            var synthesized = await client.SynthesizeAsync(text, speaker.StyleId, _settings.VoiceTuning);
            PlayWave(synthesized.WaveBytes, device.DeviceNumber);
            _speechStatusLabel.Text = $"再生中: {speaker.DisplayName} -> {device.Name}";
        }
        catch (Exception ex)
        {
            _speechStatusLabel.Text = "読み上げに失敗しました。";
            MessageBox.Show(this, ex.Message, "読み上げ失敗", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            _speakButton.Enabled = true;
        }
    }

    private async Task SaveSettingsFromUiAsync(string? successMessage)
    {
        _settings.SelectedSpeakerId = (_speakerComboBox.SelectedItem as SpeakerItem)?.StyleId ?? _settings.SelectedSpeakerId;
        _settings.SelectedOutputDeviceName = (_outputDeviceComboBox.SelectedItem as AudioDeviceItem)?.Name;
        _settings.VoiceTuning.SpeedScale = _speedScaleUpDown.Value;
        _settings.VoiceTuning.PitchScale = _pitchScaleUpDown.Value;
        _settings.VoiceTuning.IntonationScale = _intonationScaleUpDown.Value;
        _settings.VoiceTuning.VolumeScale = _volumeScaleUpDown.Value;
        _settings.VoiceTuning.NormalizeText = _normalizeTextCheckBox.Checked;

        await _settingsStore.SaveAsync(_settings);

        if (!string.IsNullOrWhiteSpace(successMessage))
        {
            _speechStatusLabel.Text = successMessage;
        }
    }

    private async Task LoadSpeakersAsync()
    {
        _speakerComboBox.Items.Clear();

        try
        {
            var client = CreateApiClient();
            if (!await client.IsEngineAvailableAsync())
            {
                _speechStatusLabel.Text = "VOICEVOX ENGINE が未起動です。セットアップタブから起動してください。";
                return;
            }

            var speakers = await client.GetSpeakersAsync();
            _speakerItems = speakers
                .SelectMany(speaker => speaker.Styles.Select(style => new SpeakerItem(style.Id, $"{speaker.Name} / {style.Name}")))
                .OrderBy(x => x.DisplayName, StringComparer.CurrentCulture)
                .ToList();

            _speakerComboBox.Items.AddRange(_speakerItems.Cast<object>().ToArray());

            var selectedSpeaker = _speakerItems.FirstOrDefault(x => x.StyleId == _settings.SelectedSpeakerId)
                ?? _speakerItems.FirstOrDefault();

            if (selectedSpeaker is not null)
            {
                _speakerComboBox.SelectedItem = selectedSpeaker;
            }

            _speechStatusLabel.Text = "VOICEVOX の話者一覧を読み込みました。";
        }
        catch (Exception ex)
        {
            _speechStatusLabel.Text = $"話者一覧の取得に失敗しました: {ex.Message}";
        }
    }

    private void ReloadAudioDevices()
    {
        _outputDeviceComboBox.Items.Clear();

        var items = new List<AudioDeviceItem>();
        for (var i = 0; i < WaveOut.DeviceCount; i++)
        {
            var capabilities = WaveOut.GetCapabilities(i);
            items.Add(new AudioDeviceItem(i, capabilities.ProductName));
        }

        _outputDeviceComboBox.Items.AddRange(items.Cast<object>().ToArray());

        var preferred = items.FirstOrDefault(x => string.Equals(x.Name, _settings.SelectedOutputDeviceName, StringComparison.OrdinalIgnoreCase))
            ?? items.FirstOrDefault(x => x.Name.Contains("CABLE Input", StringComparison.OrdinalIgnoreCase))
            ?? items.FirstOrDefault();

        if (preferred is not null)
        {
            _outputDeviceComboBox.SelectedItem = preferred;
        }
    }

    private void ApplySettingsToUi()
    {
        _speedScaleUpDown.Value = _settings.VoiceTuning.SpeedScale;
        _pitchScaleUpDown.Value = _settings.VoiceTuning.PitchScale;
        _intonationScaleUpDown.Value = _settings.VoiceTuning.IntonationScale;
        _volumeScaleUpDown.Value = _settings.VoiceTuning.VolumeScale;
        _normalizeTextCheckBox.Checked = _settings.VoiceTuning.NormalizeText;
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
        _waveOut.PlaybackStopped += (_, _) => _speechStatusLabel.Text = "再生が終わりました。";
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

    private string? FindRunExecutablePath()
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

    private void UpdateSetupGuide()
    {
        var voicevoxState = FindRunExecutablePath() is null
            ? "1. まだ VOICEVOX ENGINE は未導入です。セットアップタブから自動セットアップしてください。"
            : "1. VOICEVOX ENGINE は導入済みです。必要ならセットアップタブから起動してください。";

        _guideTextBox.Text = string.Join(
            Environment.NewLine + Environment.NewLine,
            [
                "Discord で使う手順",
                voicevoxState,
                "2. VB-CABLE を公式サイトからインストールします。インストール後は Windows の再起動が必要です。",
                "3. Discord の入力デバイスを `CABLE Output (VB-Audio Virtual Cable)` にします。",
                "4. このアプリの `しゃべる` タブで再生先デバイスを `CABLE Input (VB-Audio Virtual Cable)` にします。",
                "5. 文章を入れて `読み上げ` を押すと、Discord 側では仮想マイクとして聞こえます。",
                "聞こえやすくするコツ",
                "- 音量倍率は 1.35 を初期値にしています。小さければ 1.50 くらいまで上げてください。",
                "- Discord 側の入力感度が自動だと途切れることがあります。必要なら自動感度を切って少し低めにします。",
                "- ノイズ抑制が強いと機械音声が削られることがあるので、必要に応じて Discord の音声処理を弱めます。"
            ]);
    }

    private void ToggleSetupButtons(bool enabled)
    {
        _installVoicevoxButton.Enabled = enabled;
        _startVoicevoxButton.Enabled = enabled;
        _openVbCableButton.Enabled = enabled;
    }

    private void UpdateProgressBar(long? receivedBytes, long? totalBytes)
    {
        if (receivedBytes.HasValue && totalBytes.HasValue && totalBytes.Value > 0)
        {
            _setupProgressBar.Style = ProgressBarStyle.Continuous;
            var percent = (int)Math.Clamp(receivedBytes.Value * 100 / totalBytes.Value, 0, 100);
            _setupProgressBar.Value = percent;
            return;
        }

        _setupProgressBar.Style = ProgressBarStyle.Marquee;
    }

    private void AppendSetupLog(string message)
    {
        _setupLogTextBox.AppendText(message + Environment.NewLine);
    }

    private static void ConfigureNumeric(NumericUpDown numericUpDown, decimal minimum, decimal maximum, decimal value, decimal increment)
    {
        numericUpDown.Minimum = minimum;
        numericUpDown.Maximum = maximum;
        numericUpDown.Value = value;
        numericUpDown.Increment = increment;
        numericUpDown.DecimalPlaces = 2;
        numericUpDown.Dock = DockStyle.Left;
        numericUpDown.Width = 100;
    }

    private static Label CreateLabel(string text)
    {
        return new Label
        {
            Text = text,
            TextAlign = ContentAlignment.MiddleLeft,
            Dock = DockStyle.Fill,
            AutoSize = true
        };
    }

    private static Panel CreateRowPanel(Control fillControl, params Control[] trailingControls)
    {
        var panel = new Panel
        {
            Dock = DockStyle.Fill
        };

        fillControl.Anchor = AnchorStyles.Left | AnchorStyles.Right;
        fillControl.Left = 0;
        fillControl.Top = 4;
        fillControl.Width = Math.Max(200, panel.Width - 120);
        panel.Controls.Add(fillControl);

        var right = 0;
        foreach (var control in trailingControls.Reverse())
        {
            control.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            control.Top = 2;
            control.Left = Math.Max(0, panel.Width - control.Width - right);
            right += control.Width + 8;
            panel.Controls.Add(control);
        }

        panel.Resize += (_, _) =>
        {
            var trailingWidth = trailingControls.Sum(x => x.Width + 8);
            fillControl.Width = Math.Max(200, panel.ClientSize.Width - trailingWidth - 8);

            var offset = 0;
            foreach (var control in trailingControls.Reverse())
            {
                control.Left = panel.ClientSize.Width - control.Width - offset;
                offset += control.Width + 8;
            }
        };

        return panel;
    }

    private Control CreateVoiceTuningPanel()
    {
        var panel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight
        };

        panel.Controls.Add(new Label { Text = "速度", AutoSize = true, Padding = new Padding(0, 8, 0, 0) });
        panel.Controls.Add(_speedScaleUpDown);
        panel.Controls.Add(new Label { Text = "高さ", AutoSize = true, Padding = new Padding(12, 8, 0, 0) });
        panel.Controls.Add(_pitchScaleUpDown);
        panel.Controls.Add(new Label { Text = "抑揚", AutoSize = true, Padding = new Padding(12, 8, 0, 0) });
        panel.Controls.Add(_intonationScaleUpDown);
        panel.Controls.Add(new Label { Text = "音量", AutoSize = true, Padding = new Padding(12, 8, 0, 0) });
        panel.Controls.Add(_volumeScaleUpDown);

        return panel;
    }

    private static void OpenUrl(string url)
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = url,
            UseShellExecute = true
        });
    }

    private static string FormatVariantName(VoicevoxEngineVariant variant)
    {
        return variant switch
        {
            VoicevoxEngineVariant.Nvidia => "(NVIDIA GPU版)",
            _ => "(DirectML GPU版)"
        };
    }

    private sealed record SpeakerItem(int StyleId, string DisplayName)
    {
        public override string ToString() => DisplayName;
    }

    private sealed record AudioDeviceItem(int DeviceNumber, string Name)
    {
        public override string ToString() => Name;
    }
}
