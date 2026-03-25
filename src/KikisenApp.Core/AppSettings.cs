namespace KikisenApp.Core;

public sealed class AppSettings
{
    public string EngineBaseUrl { get; set; } = "http://127.0.0.1:50021";

    public int SelectedSpeakerId { get; set; } = 3;

    public string? SelectedOutputDeviceName { get; set; }

    public VoiceTuningSettings VoiceTuning { get; set; } = new();

    public string? InstalledEnginePath { get; set; }

    public string? InstalledEngineVersion { get; set; }
}

public sealed class VoiceTuningSettings
{
    public decimal SpeedScale { get; set; } = 1.00m;

    public decimal PitchScale { get; set; } = 0.00m;

    public decimal IntonationScale { get; set; } = 1.10m;

    public decimal VolumeScale { get; set; } = 1.35m;

    public bool NormalizeText { get; set; } = true;
}
