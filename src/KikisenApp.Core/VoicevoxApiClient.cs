using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace KikisenApp.Core;

public sealed class VoicevoxApiClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _httpClient;

    public VoicevoxApiClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<bool> IsEngineAvailableAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            using var response = await _httpClient.GetAsync("version", cancellationToken);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    public async Task<IReadOnlyList<VoicevoxSpeaker>> GetSpeakersAsync(CancellationToken cancellationToken = default)
    {
        var speakers = await _httpClient.GetFromJsonAsync<List<VoicevoxSpeaker>>("speakers", JsonOptions, cancellationToken);
        return speakers ?? [];
    }

    public async Task<SynthesizedSpeech> SynthesizeAsync(
        string text,
        int speakerId,
        VoiceTuningSettings tuning,
        CancellationToken cancellationToken = default)
    {
        var encodedText = Uri.EscapeDataString(text);
        var audioQueryEndpoint = $"audio_query?text={encodedText}&speaker={speakerId}";
        using var audioQueryResponse = await _httpClient.PostAsync(audioQueryEndpoint, content: null, cancellationToken);
        audioQueryResponse.EnsureSuccessStatusCode();

        var query = await audioQueryResponse.Content.ReadFromJsonAsync<VoicevoxAudioQuery>(JsonOptions, cancellationToken);
        if (query is null)
        {
            throw new InvalidOperationException("VOICEVOX から音声クエリを受け取れませんでした。");
        }

        query.SpeedScale = tuning.SpeedScale;
        query.PitchScale = tuning.PitchScale;
        query.IntonationScale = tuning.IntonationScale;
        query.VolumeScale = tuning.VolumeScale;
        query.PrePhonemeLength = 0.08m;
        query.PostPhonemeLength = 0.10m;

        var payload = JsonSerializer.Serialize(query, JsonOptions);
        using var content = new StringContent(payload, Encoding.UTF8, "application/json");
        using var synthesisResponse = await _httpClient.PostAsync($"synthesis?speaker={speakerId}", content, cancellationToken);
        synthesisResponse.EnsureSuccessStatusCode();

        return new SynthesizedSpeech(
            WaveBytes: await synthesisResponse.Content.ReadAsByteArrayAsync(cancellationToken),
            SpokenText: text);
    }
}
