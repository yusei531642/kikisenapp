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
    private readonly VoicevoxEngineInstaller _voicevoxInstaller;
    private readonly VbCableInstaller _vbCableInstaller;
    private readonly VoicevoxEngineProcessManager _processManager;

    private List<SpeakerItem> _speakerItems = [];

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
    private readonly Button _installVoicevoxButton = new();
    private readonly Button _startVoicevoxButton = new();
    private readonly Button _installVbCableButton = new();
    private readonly Button _openVbCableButton = new();
    private readonly TextBox _guideTextBox = new();

    public SettingsForm(
        AppPaths paths,
        AppSettingsStore settingsStore,
        AppSettings settings,
        HttpClient engineHttpClient,
        VoicevoxEngineInstaller voicevoxInstaller,
        VbCableInstaller vbCableInstaller,
        VoicevoxEngineProcessManager processManager)
    {
        _paths = paths;
        _settingsStore = settingsStore;
        _settings = settings;
        _engineHttpClient = engineHttpClient;
        _voicevoxInstaller = voicevoxInstaller;
        _vbCableInstaller = vbCableInstaller;
        _processManager = processManager;

        Text = "設定";
        Icon = System.Drawing.Icon.ExtractAssociatedIcon(Application.ExecutablePath);
        StartPosition = FormStartPosition.CenterParent;
        Size = new Size(760, 540);
        MinimumSize = new Size(620, 420);
        FormBorderStyle = FormBorderStyle.Sizable;
        MaximizeBox = true;
        MinimizeBox = false;
        Font = new Font("Meiryo UI", 9F, FontStyle.Regular, GraphicsUnit.Point);

        BuildUi();
    }

    protected override async void OnLoad(EventArgs e)
    {
        base.OnLoad(e);

        ApplySettingsToUi();
        ReloadAudioDevices();
        await TryAutoStartVoicevoxAsync();
        await LoadSpeakersAsync();
        UpdateSetupGuide();
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

        _installVoicevoxButton.Text = "VOICEVOX を自動セットアップ";
        _installVoicevoxButton.AutoSize = true;
        _installVoicevoxButton.Click += async (_, _) => await InstallVoicevoxAsync();

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
        buttonPanel.Controls.Add(_installVoicevoxButton);
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

            var result = await _voicevoxInstaller.EnsureInstalledAsync(progress);
            _settings.InstalledEnginePath = Path.GetDirectoryName(result.RunExecutablePath);
            _settings.InstalledEngineVersion = result.Version;
            await _settingsStore.SaveAsync(_settings);

            var startedNow = await _processManager.StartAsync(
                result.RunExecutablePath,
                _settings.EngineBaseUrl,
                CreateApiClient);

            var variantName = FormatVariantName(result.Variant);
            _setupStatusLabel.Text = startedNow
                ? $"VOICEVOX ENGINE {variantName} {result.Version} を起動しました。"
                : $"VOICEVOX ENGINE {variantName} {result.Version} はすでに起動しています。";
            AppendSetupLog($"VOICEVOX ENGINE {variantName} を {result.RunExecutablePath} に準備しました。");
            AppendSetupLog(_setupStatusLabel.Text);
            await LoadSpeakersAsync();
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

    private void ApplySettingsToUi()
    {
        _speedScaleUpDown.Value = _settings.VoiceTuning.SpeedScale;
        _pitchScaleUpDown.Value = _settings.VoiceTuning.PitchScale;
        _intonationScaleUpDown.Value = _settings.VoiceTuning.IntonationScale;
        _volumeScaleUpDown.Value = _settings.VoiceTuning.VolumeScale;
        _normalizeTextCheckBox.Checked = _settings.VoiceTuning.NormalizeText;
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
                "使い方",
                voicevoxState,
                "2. `VB-CABLE を自動セットアップ` を押して、管理者権限を許可します。",
                "3. 必要なら Windows を再起動します。",
                "4. Discord の入力デバイスを `CABLE Output (VB-Audio Virtual Cable)` にします。",
                "5. このソフトの再生先を `CABLE Input (VB-Audio Virtual Cable)` にします。",
                "6. メイン画面で文章を入れて `送信` を押すと読み上げます。"
            ]);
    }

    private void ToggleSetupButtons(bool enabled)
    {
        _installVoicevoxButton.Enabled = enabled;
        _startVoicevoxButton.Enabled = enabled;
        _installVbCableButton.Enabled = enabled;
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
