using System.Diagnostics;

namespace KikisenApp.Core;

public sealed class VoicevoxEngineProcessManager
{
    private Process? _process;

    public async Task<bool> StartAsync(
        string runExecutablePath,
        string baseUrl,
        Func<VoicevoxApiClient> clientFactory,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(runExecutablePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(baseUrl);

        var client = clientFactory();
        if (await client.IsEngineAvailableAsync(cancellationToken))
        {
            return false;
        }

        if (!File.Exists(runExecutablePath))
        {
            throw new FileNotFoundException("VOICEVOX ENGINE の run.exe が見つかりません。", runExecutablePath);
        }

        var uri = new Uri(baseUrl);
        var arguments = $"--host {uri.Host} --port {uri.Port}";

        _process = Process.Start(new ProcessStartInfo
        {
            FileName = runExecutablePath,
            Arguments = arguments,
            WorkingDirectory = Path.GetDirectoryName(runExecutablePath) ?? AppContext.BaseDirectory,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        });

        for (var i = 0; i < 60; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);

            client = clientFactory();
            if (await client.IsEngineAvailableAsync(cancellationToken))
            {
                return true;
            }
        }

        throw new TimeoutException("VOICEVOX ENGINE の起動待ちがタイムアウトしました。");
    }

    public void Stop()
    {
        try
        {
            if (_process is { HasExited: false })
            {
                _process.Kill(entireProcessTree: true);
            }
        }
        catch
        {
        }
        finally
        {
            _process?.Dispose();
            _process = null;
        }
    }
}
