using BellTower.Core;

namespace BellTower.Tray;

public sealed class SilentAudioSink : IAudioSink
{
    public void Play(string? phrase, AttentionReason reason, Func<bool> cancelled)
    {
    }

    public void Dispose()
    {
    }
}
