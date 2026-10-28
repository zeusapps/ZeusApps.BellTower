using BellTower.Core;

namespace BellTower.Tray;

public interface IMicrophoneActivitySource
{
    MicrophoneActivitySnapshot GetSnapshot();
}

// Windows does not expose a documented system-wide current-use query for all capture modes.
// Until an authoritative source is available, unknown remains the safe production result.
public sealed class UnavailableMicrophoneActivitySource : IMicrophoneActivitySource
{
    public MicrophoneActivitySnapshot GetSnapshot() => MicrophoneActivitySnapshot.Unknown(
        "Windows does not expose a supported system-wide microphone-use status for all capture modes.");
}
