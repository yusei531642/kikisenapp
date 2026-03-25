using System.Text.Json;
using System.Text.Json.Serialization;

namespace KikisenApp.Core;

public sealed class AppSettingsStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly AppPaths _paths;

    public AppSettingsStore(AppPaths paths)
    {
        _paths = paths;
    }

    public async Task<AppSettings> LoadAsync(CancellationToken cancellationToken = default)
    {
        _paths.EnsureDirectories();

        if (!File.Exists(_paths.SettingsPath))
        {
            var defaultSettings = new AppSettings();
            await SaveAsync(defaultSettings, cancellationToken);
            return defaultSettings;
        }

        await using var stream = File.OpenRead(_paths.SettingsPath);
        var settings = await JsonSerializer.DeserializeAsync<AppSettings>(stream, SerializerOptions, cancellationToken);
        return settings ?? new AppSettings();
    }

    public async Task SaveAsync(AppSettings settings, CancellationToken cancellationToken = default)
    {
        _paths.EnsureDirectories();
        await using var stream = File.Create(_paths.SettingsPath);
        await JsonSerializer.SerializeAsync(stream, settings, SerializerOptions, cancellationToken);
    }
}
