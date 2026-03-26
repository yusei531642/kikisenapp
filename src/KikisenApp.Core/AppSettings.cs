namespace KikisenApp.Core;

public sealed class AppSettings
{
    public string EngineBaseUrl { get; set; } = "http://127.0.0.1:50021";

    public int SelectedSpeakerId { get; set; } = 3;

    public string? SelectedOutputDeviceName { get; set; }

    public bool MonitorSpeechLocally { get; set; } = true;

    public bool KeepMainWindowMaximized { get; set; }

    public bool AutoCheckForAppUpdates { get; set; } = true;

    public WhisperSettings Whisper { get; set; } = new();

    public VoiceTuningSettings VoiceTuning { get; set; } = new();

    public string? InstalledEnginePath { get; set; }

    public string? InstalledEngineVersion { get; set; }
}

public sealed class WhisperSettings
{
    public string SelectedModelId { get; set; } = "tiny";

    public string? SelectedInputDeviceName { get; set; }

    public int SegmentDurationMilliseconds { get; set; } = 2200;

    public float SilenceThreshold { get; set; } = 0.015f;

    public int FlushAfterSilenceMilliseconds { get; set; } = 1800;
}

public sealed class VoiceTuningSettings
{
    public decimal SpeedScale { get; set; } = 1.00m;

    public decimal PitchScale { get; set; } = 0.00m;

    public decimal IntonationScale { get; set; } = 1.10m;

    public decimal VolumeScale { get; set; } = 1.35m;

    public bool NormalizeText { get; set; } = true;
}
