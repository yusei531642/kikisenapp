using System.Text.Json.Serialization;

namespace KikisenApp.Core;

public sealed class VoicevoxSpeaker
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("styles")]
    public List<VoicevoxSpeakerStyle> Styles { get; set; } = [];
}

public sealed class VoicevoxSpeakerStyle
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;
}

public sealed class VoicevoxAudioQuery
{
    [JsonPropertyName("accent_phrases")]
    public List<object> AccentPhrases { get; set; } = [];

    [JsonPropertyName("speedScale")]
    public decimal SpeedScale { get; set; }

    [JsonPropertyName("pitchScale")]
    public decimal PitchScale { get; set; }

    [JsonPropertyName("intonationScale")]
    public decimal IntonationScale { get; set; }

    [JsonPropertyName("volumeScale")]
    public decimal VolumeScale { get; set; }

    [JsonPropertyName("prePhonemeLength")]
    public decimal PrePhonemeLength { get; set; }

    [JsonPropertyName("postPhonemeLength")]
    public decimal PostPhonemeLength { get; set; }

    [JsonPropertyName("outputSamplingRate")]
    public int OutputSamplingRate { get; set; }

    [JsonPropertyName("outputStereo")]
    public bool OutputStereo { get; set; }

    [JsonPropertyName("kana")]
    public string? Kana { get; set; }
}

public sealed record SynthesizedSpeech(
    byte[] WaveBytes,
    string SpokenText);
