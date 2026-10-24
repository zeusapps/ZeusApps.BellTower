namespace BellTower.Core;

public static class NativeNamePolicy
{
    // A name Bell Tower wrote, or has recorded as about to write, is never a user override.
    public static bool Ours(SessionIdentity identity, string? name) =>
        name is not null && (name == identity.NativeLastName || name == identity.NativePendingName);

    public static bool Conflict(SessionIdentity identity, string? current) => !identity.NativeSyncTakeOwnership &&
        (identity.NativeLastName is null && identity.NativePendingName is null ? !string.IsNullOrWhiteSpace(current) : !Ours(identity, current));

    // Recorded before the provider write so every other reader already recognizes the name as ours.
    public static bool RecordIntent(SessionIdentity identity, string desired)
    {
        if (identity.NativePendingName == desired)
        { return false; }
        identity.NativePendingName = desired;
        return true;
    }

    public static bool Confirm(SessionIdentity identity, string desired)
    {
        var changed = identity.NativeLastName != desired || identity.NativePendingName is not null ||
            identity.NativeSyncTakeOwnership || identity.NativeSyncStatus != "synchronized";
        identity.NativeLastName = desired;
        identity.NativePendingName = null;
        identity.NativeSyncTakeOwnership = false;
        identity.NativeSyncStatus = "synchronized";
        return changed;
    }

    // Shipped behavior, unchanged: a name set outside Bell Tower pauses writes and is never overwritten. It is not
    // promoted to NativeTitle, because whether a Codex thread name was typed by the user (and not generated) is
    // not verified from a primary source; the name stays the provider's own until the user takes ownership.
    public static void Pause(SessionIdentity identity, string? current)
    {
        identity.NativeSyncEnabled = false;
        identity.NativeSyncStatus = "paused: native chat name was set outside Bell Tower";
        identity.NativePendingName = null;
    }

    public const string ClaudeWriteStatus = "unfinished: Claude native /resume title write is deferred to the sync-native-session-titles change (not a provider restriction)";

    // Native-name status as presented by every surface. Claude has no native write yet, so the stored sync status
    // (a Codex notion) is never shown for it.
    public static string StatusFor(string provider, SessionIdentity identity) => provider == "claude" ? ClaudeWriteStatus : identity.NativeSyncStatus;

    // "Use Bell Tower name": returns from a native user title to the automatic or Bell Tower name. The title and its
    // offset are remembered so the same title is not applied again; only a newer, different title takes effect.
    public static bool UseBellTowerName(SessionIdentity identity)
    {
        if (identity.NativeTitle is null)
        { return false; }
        identity.NativeTitleReleased = identity.NativeTitle;
        identity.NativeTitle = null;
        identity.NativeTitleSource = null;
        return true;
    }
}
