namespace BellTower.Core;

public enum MicrophoneUsageState
{
    Active,
    Inactive,
    Unknown
}

public sealed record MicrophoneActivitySnapshot(MicrophoneUsageState State, DateTimeOffset? ObservedAt, string Reason)
{
    public static MicrophoneActivitySnapshot Unknown(string reason) => new(MicrophoneUsageState.Unknown, null, reason);

    public bool IsFresh(DateTimeOffset now, TimeSpan maximumAge) =>
        ObservedAt is { } observed && observed <= now && now - observed <= maximumAge;
}
