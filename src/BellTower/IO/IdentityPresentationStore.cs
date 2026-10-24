using BellTower.Core;

namespace BellTower.IO;

public sealed class IdentityPresentationStore(DataPaths paths)
{
    // The tray refreshes every ~2 s but rewrites unchanged content only on this heartbeat. Readers reject snapshots
    // older than Staleness, which is two full heartbeats: one missed write never makes a healthy tray look stale,
    // while a stopped or wedged tray is noticed within 30 s and readers use the process-free Git fallback.
    public static readonly TimeSpan Heartbeat = TimeSpan.FromSeconds(15);
    public static readonly TimeSpan Staleness = TimeSpan.FromSeconds(30);

    private string FilePath(string key) => Path.Combine(paths.Root, "presentations", ContentHash.Compute(key) + ".json");

    // Returns whether a snapshot was written. Identical content is not rewritten between heartbeats.
    public bool Save(SessionIdentity identity, GitContext git, DateTimeOffset? now = null)
    {
        var at = now ?? DateTimeOffset.UtcNow;
        var projection = new IdentityPresentation(identity.Key, at, identity.Name, identity.Subject,
            identity.Palette, identity.Checkout(identity.LaunchDirectory ?? ""), git,
            identity.ManualName is not null ? "manual" : identity.NativeTitle is not null ? "native-user" :
            identity.PinnedChange is not null ? "pinned-change" : identity.ActiveWork is not null ? identity.WorkSource ?? "work" :
            ChangeOrigin.Legacy(identity) is not null ? ChangeOrigin.LegacySelection : "fallback");
        var path = FilePath(identity.Key);
        try
        {
            if (AtomicFile.Read<IdentityPresentation>(path) is { Version: 1 } existing && existing.Key == identity.Key &&
                at - existing.At < Heartbeat && existing with { At = at } == projection)
            {
                return false;
            }
        }
        catch (Exception e) when (e is IOException or System.Text.Json.JsonException or InvalidDataException) { }
        AtomicFile.Write(path, Json.Encode(projection));
        return true;
    }

    public IdentityPresentation? Read(string key, DateTimeOffset? now = null)
    {
        try
        {
            var value = AtomicFile.Read<IdentityPresentation>(FilePath(key));
            return value is { Version: 1 } && value.Key == key && (now ?? DateTimeOffset.UtcNow) - value.At < Staleness ? value : null;
        }
        catch (Exception e) when (e is IOException or System.Text.Json.JsonException or InvalidDataException) { return null; }
    }
}
