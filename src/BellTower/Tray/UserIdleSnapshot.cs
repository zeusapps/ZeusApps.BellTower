namespace BellTower.Tray;

public sealed record UserIdleSnapshot(TimeSpan? IdleFor, DateTimeOffset ObservedAt, string Reason)
{
    // Keep the full uint32 tick half-range unambiguous for elapsed-time arithmetic while allowing
    // idle observations comfortably beyond the largest configured 24-hour threshold.
    public static readonly TimeSpan MaximumIdleFor = TimeSpan.FromMilliseconds(int.MaxValue);

    public static UserIdleSnapshot Unknown(string reason, DateTimeOffset now) => new(null, now, reason);

    public bool IsObservationFresh(DateTimeOffset now, TimeSpan maximumAge) =>
        ObservedAt <= now && now - ObservedAt <= maximumAge;

    public bool IsFresh(DateTimeOffset now, TimeSpan maximumAge) =>
        IsObservationFresh(now, maximumAge) && IdleFor is { } idle &&
        idle >= TimeSpan.Zero && idle <= MaximumIdleFor;
}
