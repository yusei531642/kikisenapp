using NAudio.Wave;

namespace KikisenApp.Desktop;

public sealed class SpeechPlaybackService : IDisposable
{
    private readonly List<PlaybackHandle> _activePlaybacks = [];
    private readonly object _sync = new();

    public void Play(byte[] waveBytes, int outputDeviceNumber, bool monitorLocally)
    {
        ArgumentNullException.ThrowIfNull(waveBytes);

        Stop();

        var deviceNumbers = new List<int> { outputDeviceNumber };
        if (monitorLocally && outputDeviceNumber != -1)
        {
            deviceNumbers.Add(-1);
        }

        lock (_sync)
        {
            foreach (var deviceNumber in deviceNumbers.Distinct())
            {
                _activePlaybacks.Add(CreatePlaybackHandle(waveBytes, deviceNumber));
            }

            foreach (var playback in _activePlaybacks)
            {
                playback.Player.Play();
            }
        }
    }

    public void Stop()
    {
        List<PlaybackHandle> playbacksToDispose;

        lock (_sync)
        {
            playbacksToDispose = [.. _activePlaybacks];
            _activePlaybacks.Clear();
        }

        foreach (var playback in playbacksToDispose)
        {
            playback.Dispose();
        }
    }

    private PlaybackHandle CreatePlaybackHandle(byte[] waveBytes, int deviceNumber)
    {
        var memoryStream = new MemoryStream(waveBytes, writable: false);
        var waveStream = new WaveFileReader(memoryStream);
        var player = new WaveOutEvent
        {
            DeviceNumber = deviceNumber
        };

        player.Init(waveStream);
        player.PlaybackStopped += OnPlaybackStopped;

        return new PlaybackHandle(player, waveStream, memoryStream, deviceNumber);
    }

    private void OnPlaybackStopped(object? sender, StoppedEventArgs e)
    {
        if (sender is not WaveOutEvent player)
        {
            return;
        }

        PlaybackHandle? playback = null;

        lock (_sync)
        {
            playback = _activePlaybacks.FirstOrDefault(x => ReferenceEquals(x.Player, player));
            if (playback is not null)
            {
                _activePlaybacks.Remove(playback);
            }
        }

        playback?.Dispose();
    }

    public void Dispose()
    {
        Stop();
    }

    private sealed class PlaybackHandle : IDisposable
    {
        private bool _disposed;

        public PlaybackHandle(WaveOutEvent player, WaveFileReader waveStream, MemoryStream memoryStream, int deviceNumber)
        {
            Player = player;
            WaveStream = waveStream;
            MemoryStream = memoryStream;
            DeviceNumber = deviceNumber;
        }

        public WaveOutEvent Player { get; }

        public WaveFileReader WaveStream { get; }

        public MemoryStream MemoryStream { get; }

        public int DeviceNumber { get; }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            Player.Dispose();
            WaveStream.Dispose();
            MemoryStream.Dispose();
        }
    }
}
