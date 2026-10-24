using BellTower.Core;
using BellTower.IO;

namespace BellTower.Sessions;

// User control over native chat-name updates. Codex has a native write (runtime thread-name service); Claude's native
// /resume title write is unfinished work deferred to sync-native-session-titles, so the sync switches refuse it
// instead of pretending. "Use Bell Tower name" is shared: both clients can return from a native user title.
public static class NativeNameControl
{
    public static void SetEnabled(DataPaths paths, SessionState session, bool enabled)
    {
        if (session.Provider == "claude")
        {
            throw new NotSupportedException(NativeNamePolicy.ClaudeWriteStatus);
        }
        if (session.Provider != "codex")
        {
            throw new ArgumentException("Unsupported provider.");
        }
        using var gate = new DataLock(paths, "native-name-" + ContentHash.Compute(session.Key));
        new IdentityStore(paths).Update(session.Key, session.Directory, value =>
        {
            value.NativeSyncEnabled = enabled;
            value.NativeSyncTakeOwnership = enabled;
            value.NativeSyncStatus = enabled ? "pending" : "disabled";
            if (enabled)
            { NativeNamePolicy.UseBellTowerName(value); }
            return true;
        });
    }

    // Both clients: drop the native user title in favor of the automatic or Bell Tower name. For Codex this also takes
    // ownership so the Bell Tower name replaces the provider's name on the next synchronization.
    public static void UseBellTowerName(DataPaths paths, SessionState session)
    {
        if (session.Provider is not ("codex" or "claude"))
        { throw new ArgumentException("Unsupported provider."); }
        if (session.Provider == "codex")
        {
            SetEnabled(paths, session, true);
            return;
        }
        using var gate = new DataLock(paths, "native-name-" + ContentHash.Compute(session.Key));
        new IdentityStore(paths).Update(session.Key, session.Directory, NativeNamePolicy.UseBellTowerName);
    }
}
