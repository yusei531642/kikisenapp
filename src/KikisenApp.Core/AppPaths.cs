namespace KikisenApp.Core;

public sealed record AppPaths(
    string RootDirectory,
    string DownloadsDirectory,
    string EngineDirectory,
    string WhisperDirectory,
    string AudioDirectory,
    string LogsDirectory,
    string SettingsPath)
{
    public static AppPaths CreateDefault()
    {
        var rootDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "KikisenApp");

        return new AppPaths(
            RootDirectory: rootDirectory,
            DownloadsDirectory: Path.Combine(rootDirectory, "downloads"),
            EngineDirectory: Path.Combine(rootDirectory, "voicevox-engine"),
            WhisperDirectory: Path.Combine(rootDirectory, "whisper"),
            AudioDirectory: Path.Combine(rootDirectory, "audio"),
            LogsDirectory: Path.Combine(rootDirectory, "logs"),
            SettingsPath: Path.Combine(rootDirectory, "settings.json"));
    }

    public void EnsureDirectories()
    {
        Directory.CreateDirectory(RootDirectory);
        Directory.CreateDirectory(DownloadsDirectory);
        Directory.CreateDirectory(EngineDirectory);
        Directory.CreateDirectory(WhisperDirectory);
        Directory.CreateDirectory(AudioDirectory);
        Directory.CreateDirectory(LogsDirectory);
    }
}
