using BellTower.Core;

namespace BellTower.Tray;

public interface IAudioSink : IDisposable
{
    void Play(string? phrase, AttentionReason reason, Func<bool> cancelled);
}
