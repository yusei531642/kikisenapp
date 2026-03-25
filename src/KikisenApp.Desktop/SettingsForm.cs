using System.ComponentModel;
using System.Diagnostics;
using KikisenApp.Core;
using NAudio.Wave;

namespace KikisenApp.Desktop;

public sealed class SettingsForm : Form
{
    private readonly AppPaths _paths;
    private readonly AppSettingsStore _settingsStore;
    private readonly AppSettings _settings;
    private readonly HttpClient _engineHttpClient;
    private readonly VbCableInstaller _vbCableInstaller;
    private readonly VoicevoxEngineProcessManager _processManager;
    private readonly WhisperController _whisperController;

    private List<SpeakerItem> _speakerItems = [];
    private List<WhisperModelItem> _whisperModelItems = [];

    private readonly ComboBox _speakerComboBox = new();
    private readonly ComboBox _outputDeviceComboBox = new();
    private readonly NumericUpDown _speedScaleUpDown = new();
    private readonly NumericUpDown _pitchScaleUpDown = new();
    private readonly NumericUpDown _intonationScaleUpDown = new();
    private readonly NumericUpDown _volumeScaleUpDown = new();
    private readonly CheckBox _normalizeTextCheckBox = new();
    private readonly Button _reloadDevicesButton = new();
    private readonly Button _saveSettingsButton = new();
    private readonly Label _voiceStatusLabel = new();
    private readonly TextBox _setupLogTextBox = new();
    private readonly ProgressBar _setupProgressBar = new();
    private readonly Label _setupStatusLabel = new();
    private readonly Button _startVoicevoxButton = new();
    private readonly Button _installVbCableButton = new();
    private readonly Button _openVbCableButton = new();
    private readonly ComboBox _whisperModelComboBox = new();
    private readonly ComboBox _whisperInputDeviceComboBox = new();
    private readonly Label _whisperModelDescriptionLabel = new();
    private readonly Label _whisperStatusLabel = new();
    private readonly Button _reloadWhisperDevicesButton = new();
    private readonly Button _saveWhisperSettingsButton = new();
    private readonly Button _downloadWhisperModelButton = new();
    private readonly Button _toggleWhisperButton = new();
    private readonly NumericUpDown _whisperSegmentUpDown = new();
    private readonly NumericUpDown _whisperFlushUpDown = new();
    private readonly NumericUpDown _whisperSilenceUpDown = new();
    private readonly TextBox _guideTextBox = new();

    public SettingsForm(
        AppPaths paths,
        AppSettingsStore settingsStore,
        AppSettings settings,
        HttpClient engineHttpClient,
        VbCableInstaller vbCableInstaller,
        VoicevoxEngineProcessManager processManager,
        WhisperController whisperController)
    {
        _paths = paths;
        _settingsStore = settingsStore;
        _settings = settings;
        _engineHttpClient = engineHttpClient;
        _vbCableInstaller = vbCableInstaller;
        _processManager = processManager;
        _whisperController = whisperController;

        Text = "設定";
        Icon = System.Drawing.Icon.ExtractAssociatedIcon(Application.ExecutablePath);
        StartPosition = FormStartPosition.CenterParent;
        Size = new Size(760, 560);
        MinimumSize = new Size(640, 460);
        FormBorderStyle = FormBorderStyle.Sizable;
        MaximizeBox = true;
        MinimizeBox = false;
        Font = new Font("Meiryo UI", 9F, FontStyle.Regular, GraphicsUnit.Point);

        BuildUi();
    }

    protected override async void OnLoad(EventArgs e)
    {
        base.OnLoad(e);

        _whisperController.StatusChanged += OnWhisperStatusChanged;
        _whisperController.RunningStateChanged += OnWhisperRunningStateChanged;

        ApplySettingsToUi();
        ApplyWhisperSettingsToUi();
        ReloadAudioDevices();
        ReloadWhisperInputDevices();
        LoadWhisperModels();
        UpdateWhisperUiState();
        await TryAutoStartVoicevoxAsync();
        await LoadSpeakersAsync();
        UpdateSetupGuide();
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        _whisperController.StatusChanged -= OnWhisperStatusChanged;
        _whisperController.RunningStateChanged -= OnWhisperRunningStateChanged;
        base.OnFormClosed(e);
    }

    private void BuildUi()
    {
        var tabs = new TabControl
        {
            Dock = DockStyle.Fill,
            Padding = new Point(14, 6)
        };

        tabs.TabPages.Add(BuildVoiceTab());
        tabs.TabPages.Add(BuildSetupTab());
        tabs.TabPages.Add(BuildWhisperTab());
        tabs.TabPages.Add(BuildGuideTab());

        Controls.Add(tabs);
    }

    private TabPage BuildVoiceTab()
    {
        var page = new TabPage("音声設定")
        {
            Padding = new Padding(4)
        };

        var table = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 6,
            Padding = new Padding(12)
        };
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        table.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
        table.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
        table.RowStyles.Add(new RowStyle(SizeType.Absolute, 52));
        table.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
        table.RowStyles.Add(new RowStyle(SizeType.Absolute, 52));
        table.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

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

        _saveSettingsButton.Text = "保存";
        _saveSettingsButton.AutoSize = true;
        _saveSettingsButton.Click += async (_, _) => await SaveSettingsFromUiAsync("設定を保存しました。");

        _voiceStatusLabel.Dock = DockStyle.Fill;
        _voiceStatusLabel.AutoEllipsis = true;
        _voiceStatusLabel.Text = "ここで話者や再生先を変えられます。";

        table.Controls.Add(CreateLabel("話者"), 0, 0);
        table.Controls.Add(_speakerComboBox, 1, 0);

        table.Controls.Add(CreateLabel("再生先デバイス"), 0, 1);
        table.Controls.Add(CreateRowPanel(_outputDeviceComboBox, _reloadDevicesButton), 1, 1);

        table.Controls.Add(CreateLabel("話し方"), 0, 2);
        table.Controls.Add(CreateVoiceTuningPanel(), 1, 2);

        table.Controls.Add(CreateLabel("文字の整形"), 0, 3);
        table.Controls.Add(_normalizeTextCheckBox, 1, 3);

        var bottomPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = true
        };
        bottomPanel.Controls.Add(_saveSettingsButton);
        bottomPanel.Controls.Add(_voiceStatusLabel);

        table.Controls.Add(bottomPanel, 0, 4);
        table.SetColumnSpan(bottomPanel, 2);

        page.Controls.Add(table);
        return page;
    }

    private TabPage BuildSetupTab()
    {
        var page = new TabPage("セットアップ")
        {
            Padding = new Padding(4)
        };

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 5,
            Padding = new Padding(12)
        };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        _setupStatusLabel.Text = "必要なものをここで準備できます。";
        _setupStatusLabel.Dock = DockStyle.Fill;

        _setupProgressBar.Dock = DockStyle.Fill;

        _startVoicevoxButton.Text = "VOICEVOX を起動";
        _startVoicevoxButton.AutoSize = true;
        _startVoicevoxButton.Click += async (_, _) => await StartVoicevoxAsync();

        _installVbCableButton.Text = "VB-CABLE を自動セットアップ";
        _installVbCableButton.AutoSize = true;
        _installVbCableButton.Click += async (_, _) => await InstallVbCableAsync();

        _openVbCableButton.Text = "VB-CABLE 公式ページを開く";
        _openVbCableButton.AutoSize = true;
        _openVbCableButton.Click += (_, _) => OpenUrl(ExternalLinks.VbCablePage);

        var infoLabel = new Label
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            Text = "VB-CABLE は管理者権限の許可が必要です。導入後は再起動すると安定します。"
        };

        _setupLogTextBox.Multiline = true;
        _setupLogTextBox.ScrollBars = ScrollBars.Vertical;
        _setupLogTextBox.ReadOnly = true;
        _setupLogTextBox.Dock = DockStyle.Fill;
        _setupLogTextBox.Font = new Font("Consolas", 9F, FontStyle.Regular, GraphicsUnit.Point);

        var buttonPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = true
        };
        buttonPanel.Controls.Add(_startVoicevoxButton);
        buttonPanel.Controls.Add(_installVbCableButton);
        buttonPanel.Controls.Add(_openVbCableButton);

        layout.Controls.Add(_setupStatusLabel, 0, 0);
        layout.Controls.Add(_setupProgressBar, 0, 1);
        layout.Controls.Add(buttonPanel, 0, 2);
        layout.Controls.Add(infoLabel, 0, 3);
        layout.Controls.Add(_setupLogTextBox, 0, 4);

        page.Controls.Add(layout);
        return page;
    }

    private TabPage BuildWhisperTab()
    {
        var page = new TabPage("Whisper")
        {
            Padding = new Padding(4)
        };

        var table = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 8,
            Padding = new Padding(12)
        };
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 160));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        table.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
        table.RowStyles.Add(new RowStyle(SizeType.Absolute, 56));
        table.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
        table.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
        table.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
        table.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
        table.RowStyles.Add(new RowStyle(SizeType.Absolute, 56));
        table.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        _whisperModelComboBox.DropDownStyle = ComboBoxStyle.DropDownList;
        _whisperModelComboBox.Dock = DockStyle.Fill;
        _whisperModelComboBox.SelectedIndexChanged += (_, _) => UpdateSelectedWhisperModelDescription();

        _whisperModelDescriptionLabel.Dock = DockStyle.Fill;
        _whisperModelDescriptionLabel.AutoEllipsis = true;
        _whisperModelDescriptionLabel.Text = "モデルを選ぶと重さと説明が見えます。";

        _whisperInputDeviceComboBox.DropDownStyle = ComboBoxStyle.DropDownList;
        _whisperInputDeviceComboBox.Dock = DockStyle.Fill;

        _reloadWhisperDevicesButton.Text = "再読込";
        _reloadWhisperDevicesButton.AutoSize = true;
        _reloadWhisperDevicesButton.Click += (_, _) => ReloadWhisperInputDevices();

        _downloadWhisperModelButton.Text = "モデルをダウンロード";
        _downloadWhisperModelButton.AutoSize = true;
        _downloadWhisperModelButton.Click += async (_, _) => await DownloadWhisperModelAsync();

        _toggleWhisperButton.Text = "Whisper を開始";
        _toggleWhisperButton.AutoSize = true;
        _toggleWhisperButton.Click += async (_, _) => await ToggleWhisperAsync();

        _saveWhisperSettingsButton.Text = "Whisper 設定を保存";
        _saveWhisperSettingsButton.AutoSize = true;
        _saveWhisperSettingsButton.Click += async (_, _) => await SaveWhisperSettingsFromUiAsync("Whisper の設定を保存しました。");

        _whisperStatusLabel.Dock = DockStyle.Fill;
        _whisperStatusLabel.AutoEllipsis = true;
        _whisperStatusLabel.Text = "モデルを選んでダウンロードしたあと、開始を押すと聞き取りできます。";

        ConfigureWholeNumber(_whisperSegmentUpDown, 1000, 8000, 2200, 100);
        ConfigureWholeNumber(_whisperFlushUpDown, 1000, 8000, 1800, 100);
        ConfigureFraction(_whisperSilenceUpDown, 0.001m, 0.100m, 0.015m, 0.001m, 3);

        table.Controls.Add(CreateLabel("Whisper モデル"), 0, 0);
        table.Controls.Add(_whisperModelComboBox, 1, 0);
        table.Controls.Add(CreateLabel("モデルの説明"), 0, 1);
        table.Controls.Add(_whisperModelDescriptionLabel, 1, 1);
        table.Controls.Add(CreateLabel("聞き取るデバイス"), 0, 2);
        table.Controls.Add(CreateRowPanel(_whisperInputDeviceComboBox, _reloadWhisperDevicesButton), 1, 2);
        table.Controls.Add(CreateLabel("区切り時間"), 0, 3);
        table.Controls.Add(CreateWhisperSegmentPanel(), 1, 3);
        table.Controls.Add(CreateLabel("無音の扱い"), 0, 4);
        table.Controls.Add(CreateWhisperSilencePanel(), 1, 4);

        var actionPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = true
        };
        actionPanel.Controls.Add(_downloadWhisperModelButton);
        actionPanel.Controls.Add(_toggleWhisperButton);
        actionPanel.Controls.Add(_saveWhisperSettingsButton);

        table.Controls.Add(actionPanel, 0, 5);
        table.SetColumnSpan(actionPanel, 2);
        table.Controls.Add(_whisperStatusLabel, 0, 6);
        table.SetColumnSpan(_whisperStatusLabel, 2);

        page.Controls.Add(table);
        return page;
    }

    private TabPage BuildGuideTab()
    {
        var page = new TabPage("使い方")
        {
            Padding = new Padding(4)
        };

        _guideTextBox.Multiline = true;
        _guideTextBox.ReadOnly = true;
        _guideTextBox.ScrollBars = ScrollBars.Vertical;
        _guideTextBox.Dock = DockStyle.Fill;
        _guideTextBox.Font = new Font("Meiryo UI", 9F, FontStyle.Regular, GraphicsUnit.Point);

        page.Controls.Add(_guideTextBox);
        return page;
    }

    private async Task LoadSpeakersAsync()
    {
        _speakerComboBox.Items.Clear();

        try
        {
            var client = CreateApiClient();
            if (!await client.IsEngineAvailableAsync())
            {
                _voiceStatusLabel.Text = "VOICEVOX ENGINE が未起動です。セットアップで起動してください。";
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

            _voiceStatusLabel.Text = "話者一覧を読み込みました。";
        }
        catch (Exception ex)
        {
            _voiceStatusLabel.Text = $"話者一覧の取得に失敗しました: {ex.Message}";
        }
    }

    private void ReloadAudioDevices()
    {
        _outputDeviceComboBox.Items.Clear();

        var devices = EnumerateAudioDevices();
        _outputDeviceComboBox.Items.AddRange(devices.Cast<object>().ToArray());

        var selected = devices.FirstOrDefault(x => string.Equals(x.Name, _settings.SelectedOutputDeviceName, StringComparison.OrdinalIgnoreCase))
            ?? devices.FirstOrDefault(x => x.Name.Contains("CABLE Input", StringComparison.OrdinalIgnoreCase))
            ?? devices.FirstOrDefault();

        if (selected is not null)
        {
            _outputDeviceComboBox.SelectedItem = selected;
        }
    }

    private void ReloadWhisperInputDevices()
    {
        _whisperInputDeviceComboBox.Items.Clear();

        var devices = _whisperController
            .GetInputDevices()
            .Select(x => new WhisperInputDeviceItem(x.DeviceNumber, x.Name))
            .ToList();

        _whisperInputDeviceComboBox.Items.AddRange(devices.Cast<object>().ToArray());

        var selected = devices.FirstOrDefault(x => string.Equals(x.Name, _settings.Whisper.SelectedInputDeviceName, StringComparison.OrdinalIgnoreCase))
            ?? devices.FirstOrDefault();

        if (selected is not null)
        {
            _whisperInputDeviceComboBox.SelectedItem = selected;
        }
    }

    private void LoadWhisperModels()
    {
        _whisperModelItems = _whisperController
            .GetModels()
            .Select(x => new WhisperModelItem(x))
            .ToList();

        _whisperModelComboBox.Items.Clear();
        _whisperModelComboBox.Items.AddRange(_whisperModelItems.Cast<object>().ToArray());

        var selected = _whisperModelItems.FirstOrDefault(x => string.Equals(x.Definition.Id, _settings.Whisper.SelectedModelId, StringComparison.OrdinalIgnoreCase))
            ?? _whisperModelItems.FirstOrDefault();

        if (selected is not null)
        {
            _whisperModelComboBox.SelectedItem = selected;
        }

        UpdateSelectedWhisperModelDescription();
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
            _voiceStatusLabel.Text = successMessage;
        }
    }

    private async Task SaveWhisperSettingsFromUiAsync(string? successMessage)
    {
        _settings.Whisper.SelectedModelId = (_whisperModelComboBox.SelectedItem as WhisperModelItem)?.Definition.Id ?? _settings.Whisper.SelectedModelId;
        _settings.Whisper.SelectedInputDeviceName = (_whisperInputDeviceComboBox.SelectedItem as WhisperInputDeviceItem)?.Name;
        _settings.Whisper.SegmentDurationMilliseconds = (int)_whisperSegmentUpDown.Value;
        _settings.Whisper.FlushAfterSilenceMilliseconds = (int)_whisperFlushUpDown.Value;
        _settings.Whisper.SilenceThreshold = (float)_whisperSilenceUpDown.Value;

        await _settingsStore.SaveAsync(_settings);
        UpdateSelectedWhisperModelDescription();

        if (!string.IsNullOrWhiteSpace(successMessage))
        {
            _whisperStatusLabel.Text = successMessage;
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

    private async Task InstallVbCableAsync()
    {
        ToggleSetupButtons(false);
        AppendSetupLog("VB-CABLE のセットアップを開始します。");

        try
        {
            _setupProgressBar.Style = ProgressBarStyle.Marquee;
            var progress = new Progress<SetupProgress>(info =>
            {
                _setupStatusLabel.Text = info.Message;
                UpdateProgressBar(info.ReceivedBytes, info.TotalBytes);
                AppendSetupLog($"{DateTime.Now:HH:mm:ss} {info.Message}");
            });

            var result = await _vbCableInstaller.DownloadAndLaunchInstallerAsync(progress);
            AppendSetupLog($"VB-CABLE セットアップを起動しました: {result.InstallerPath}");
            AppendSetupLog($"ダウンロード元: {result.DownloadUrl}");

            ReloadAudioDevices();
            var cableDevice = SelectCableInputIfAvailable();
            if (cableDevice is not null)
            {
                _settings.SelectedOutputDeviceName = cableDevice.Name;
                await _settingsStore.SaveAsync(_settings);
            }

            _setupStatusLabel.Text = cableDevice is null
                ? "VB-CABLE のセットアップを起動しました。完了後は Windows を再起動してください。"
                : "VB-CABLE を検出しました。Discord 側の入力を CABLE Output にしてください。";

            UpdateSetupGuide();
        }
        catch (Win32Exception ex) when (ex.NativeErrorCode == 1223)
        {
            _setupStatusLabel.Text = "VB-CABLE の管理者権限がキャンセルされました。";
            AppendSetupLog("VB-CABLE の UAC 承認がキャンセルされました。");
        }
        catch (Exception ex)
        {
            _setupStatusLabel.Text = "VB-CABLE のセットアップに失敗しました。";
            AppendSetupLog($"エラー: {ex.Message}");
            MessageBox.Show(this, ex.Message, "VB-CABLE セットアップ失敗", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            ToggleSetupButtons(true);
        }
    }

    private async Task DownloadWhisperModelAsync()
    {
        var model = (_whisperModelComboBox.SelectedItem as WhisperModelItem)?.Definition;
        if (model is null)
        {
            MessageBox.Show(this, "Whisper モデルを選んでください。", "Whisper", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        ToggleWhisperButtons(false);

        try
        {
            await SaveWhisperSettingsFromUiAsync(null);
            var progress = new Progress<SetupProgress>(info => _whisperStatusLabel.Text = info.Message);
            await _whisperController.DownloadModelAsync(model.Id, progress);
            LoadWhisperModels();
            UpdateSelectedWhisperModelDescription();
        }
        catch (Exception ex)
        {
            _whisperStatusLabel.Text = "Whisper モデルの準備に失敗しました。";
            MessageBox.Show(this, ex.Message, "Whisper ダウンロード失敗", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            ToggleWhisperButtons(true);
            UpdateWhisperUiState();
        }
    }

    private async Task ToggleWhisperAsync()
    {
        ToggleWhisperButtons(false);

        try
        {
            await SaveWhisperSettingsFromUiAsync(null);

            if (_whisperController.IsRunning)
            {
                await _whisperController.StopAsync();
            }
            else
            {
                await _whisperController.StartAsync();
            }
        }
        catch (Exception ex)
        {
            _whisperStatusLabel.Text = "Whisper の開始または停止に失敗しました。";
            MessageBox.Show(this, ex.Message, "Whisper エラー", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            ToggleWhisperButtons(true);
            UpdateWhisperUiState();
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

    private void ApplyWhisperSettingsToUi()
    {
        _whisperSegmentUpDown.Value = Math.Clamp(_settings.Whisper.SegmentDurationMilliseconds, (int)_whisperSegmentUpDown.Minimum, (int)_whisperSegmentUpDown.Maximum);
        _whisperFlushUpDown.Value = Math.Clamp(_settings.Whisper.FlushAfterSilenceMilliseconds, (int)_whisperFlushUpDown.Minimum, (int)_whisperFlushUpDown.Maximum);

        var threshold = (decimal)_settings.Whisper.SilenceThreshold;
        threshold = Math.Clamp(threshold, _whisperSilenceUpDown.Minimum, _whisperSilenceUpDown.Maximum);
        _whisperSilenceUpDown.Value = threshold;
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
            ? "1. まだ VOICEVOX ENGINE が見つかっていません。まずは setup.exe から入れてください。"
            : "1. VOICEVOX ENGINE は導入済みです。必要ならセットアップタブから起動してください。";

        var whisperState = _whisperController.IsModelInstalled(_settings.Whisper.SelectedModelId)
            ? "Whisper モデルは準備済みです。Whisper タブで開始すると聞き取り後に自動で読み上げます。"
            : "Whisper タブでモデルを選び、重さを見てからダウンロードしてください。";

        _guideTextBox.Text = string.Join(
            Environment.NewLine + Environment.NewLine,
            [
                "使い方",
                voicevoxState,
                "2. `VB-CABLE を自動セットアップ` を押して、管理者権限を許可します。",
                "3. 必要なら Windows を再起動します。",
                "4. Discord の入力デバイスを `CABLE Output (VB-Audio Virtual Cable)` にします。",
                "5. このソフトの再生先を `CABLE Input (VB-Audio Virtual Cable)` にします。",
                "6. Whisper タブで入力デバイスを選び、必要なモデルをダウンロードします。",
                "7. `Whisper を開始` を押すと聞き取りを始め、文の終わりごとに自動で読み上げます。",
                $"8. {whisperState}"
            ]);
    }

    private void UpdateSelectedWhisperModelDescription()
    {
        var model = (_whisperModelComboBox.SelectedItem as WhisperModelItem)?.Definition;
        if (model is null)
        {
            _whisperModelDescriptionLabel.Text = "モデルを選んでください。";
            return;
        }

        var installed = _whisperController.IsModelInstalled(model.Id) ? "ダウンロード済み" : "未ダウンロード";
        _whisperModelDescriptionLabel.Text = $"{model.DisplayName} / {model.SizeLabel} / {installed} / {model.Description}";
        UpdateSetupGuide();
    }

    private void ToggleSetupButtons(bool enabled)
    {
        _startVoicevoxButton.Enabled = enabled;
        _installVbCableButton.Enabled = enabled;
        _openVbCableButton.Enabled = enabled;
    }

    private void ToggleWhisperButtons(bool enabled)
    {
        _downloadWhisperModelButton.Enabled = enabled;
        _toggleWhisperButton.Enabled = enabled;
        _saveWhisperSettingsButton.Enabled = enabled;
        _reloadWhisperDevicesButton.Enabled = enabled;
    }

    private void UpdateWhisperUiState()
    {
        _toggleWhisperButton.Text = _whisperController.IsRunning
            ? "Whisper を停止"
            : "Whisper を開始";
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

    private AudioDeviceItem? SelectCableInputIfAvailable()
    {
        var cableDevice = _outputDeviceComboBox.Items
            .Cast<AudioDeviceItem>()
            .FirstOrDefault(x => x.Name.Contains("CABLE Input", StringComparison.OrdinalIgnoreCase));

        if (cableDevice is not null)
        {
            _outputDeviceComboBox.SelectedItem = cableDevice;
        }

        return cableDevice;
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

    private async Task TryAutoStartVoicevoxAsync()
    {
        try
        {
            var runExecutablePath = FindRunExecutablePath();
            if (runExecutablePath is null)
            {
                return;
            }

            var startedNow = await _processManager.StartAsync(
                runExecutablePath,
                _settings.EngineBaseUrl,
                CreateApiClient);

            if (startedNow)
            {
                _setupStatusLabel.Text = "VOICEVOX ENGINE を自動で起動しました。";
                AppendSetupLog(_setupStatusLabel.Text);
            }
        }
        catch
        {
        }
    }

    private void OnWhisperStatusChanged(string message)
    {
        if (IsDisposed)
        {
            return;
        }

        if (InvokeRequired)
        {
            BeginInvoke(() => OnWhisperStatusChanged(message));
            return;
        }

        _whisperStatusLabel.Text = message;
        UpdateSetupGuide();
    }

    private void OnWhisperRunningStateChanged(bool isRunning)
    {
        if (IsDisposed)
        {
            return;
        }

        if (InvokeRequired)
        {
            BeginInvoke(() => OnWhisperRunningStateChanged(isRunning));
            return;
        }

        _whisperStatusLabel.Text = isRunning
            ? "Whisper が動いています。文末ごとに自動で読み上げます。"
            : "Whisper は停止中です。";
        UpdateWhisperUiState();
        UpdateSetupGuide();
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

    private static void ConfigureWholeNumber(NumericUpDown numericUpDown, int minimum, int maximum, int value, int increment)
    {
        numericUpDown.Minimum = minimum;
        numericUpDown.Maximum = maximum;
        numericUpDown.Value = value;
        numericUpDown.Increment = increment;
        numericUpDown.DecimalPlaces = 0;
        numericUpDown.Dock = DockStyle.Left;
        numericUpDown.Width = 100;
    }

    private static void ConfigureFraction(NumericUpDown numericUpDown, decimal minimum, decimal maximum, decimal value, decimal increment, int decimalPlaces)
    {
        numericUpDown.Minimum = minimum;
        numericUpDown.Maximum = maximum;
        numericUpDown.Value = value;
        numericUpDown.Increment = increment;
        numericUpDown.DecimalPlaces = decimalPlaces;
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
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = true
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

    private Control CreateWhisperSegmentPanel()
    {
        var panel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = true
        };

        panel.Controls.Add(new Label { Text = "何ミリ秒ごとに区切るか", AutoSize = true, Padding = new Padding(0, 8, 0, 0) });
        panel.Controls.Add(_whisperSegmentUpDown);
        panel.Controls.Add(new Label { Text = "ms", AutoSize = true, Padding = new Padding(4, 8, 0, 0) });

        return panel;
    }

    private Control CreateWhisperSilencePanel()
    {
        var panel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = true
        };

        panel.Controls.Add(new Label { Text = "無音とみなす大きさ", AutoSize = true, Padding = new Padding(0, 8, 0, 0) });
        panel.Controls.Add(_whisperSilenceUpDown);
        panel.Controls.Add(new Label { Text = "  無音が続いたら読み上げるまで", AutoSize = true, Padding = new Padding(12, 8, 0, 0) });
        panel.Controls.Add(_whisperFlushUpDown);
        panel.Controls.Add(new Label { Text = "ms", AutoSize = true, Padding = new Padding(4, 8, 0, 0) });

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

    private sealed record WhisperInputDeviceItem(int DeviceNumber, string Name)
    {
        public override string ToString() => Name;
    }

    private sealed record WhisperModelItem(WhisperModelDefinition Definition)
    {
        public override string ToString() => $"{Definition.DisplayName} / {Definition.SizeLabel}";
    }
}
