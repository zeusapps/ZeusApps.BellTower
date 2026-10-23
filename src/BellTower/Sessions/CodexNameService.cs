using System.Net.WebSockets;
using System.Text.Json;
using BellTower.Core;
using BellTower.IO;
using BellTower.Providers;

namespace BellTower.Sessions;

public sealed class CodexNameService(DataPaths paths, Func<ICodexNameClient>? createClient = null)
{
    public void Sync(SessionState session, GitContext? context = null)
    {
        if (session.Provider != "codex" || session.Ended || session.AttachmentHealth == "unavailable")
        {
            return;
        }
        // The mutex stays on this synchronous caller thread while network awaits
        // resume on worker threads. Never await across a DataLock's lifetime.
        using var gate = new DataLock(paths, "native-name-" + ContentHash.Compute(session.Key));
        var identities = new IdentityStore(paths);
        var identity = identities.Get(session.Key, session.Directory);
        if (!identity.NativeSyncEnabled)
        {
            return;
        }
        try
        {
            var runtimes = new CodexRuntimeStore(paths);
            var runtime = runtimes.Get(session.AttachmentId);
            var attachment = new AttachmentStore(paths).Get(session.AttachmentId);
            if (runtime is null || attachment is not { EndedAt: null } || attachment.Host.Shell != runtime.Shell)
            {
                SetStatus(identities, session, "unavailable: no verified dedicated runtime");
                return;
            }
            var desired = SessionPresentation.CodexName(identity.Name, context ?? new GitReader().Read(session.Directory));
            var token = File.ReadAllText(runtimes.TokenPath(runtime.Attachment));
            using var client = createClient?.Invoke() ?? new CodexConnection();
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(3));
            client.ConnectAsync(runtime.Endpoint, token, timeout.Token).GetAwaiter().GetResult();
            var current = client.ReadNameAsync(session.SessionId, timeout.Token).GetAwaiter().GetResult();
            if (!identity.NativeSyncTakeOwnership &&
                (identity.NativeLastName is null ? !string.IsNullOrWhiteSpace(current) : current != identity.NativeLastName))
            {
                identities.Update(session.Key, session.Directory, value =>
                {
                    value.NativeSyncEnabled = false;
                    value.NativeSyncStatus = "paused: native chat name was set outside Bell Tower";
                    return true;
                });
                return;
            }
            if (current != desired)
            {
                client.SetNameAsync(session.SessionId, desired, timeout.Token).GetAwaiter().GetResult();
            }
            identities.Update(session.Key, session.Directory, value =>
            {
                // Preserve a concurrent user request to disable synchronization.
                if (!value.NativeSyncEnabled)
                {
                    return false;
                }
                var changed = value.NativeLastName != desired || value.NativeSyncTakeOwnership || value.NativeSyncStatus != "synchronized";
                value.NativeLastName = desired;
                value.NativeSyncTakeOwnership = false;
                value.NativeSyncStatus = "synchronized";
                return changed;
            });
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or WebSocketException or
            OperationCanceledException or JsonException or InvalidOperationException or ArgumentException)
        {
            SetStatus(identities, session, "unavailable: native metadata connection failed");
            new LocalLog(paths).Write("native-name-unavailable");
        }
    }

    public void SetEnabled(SessionState session, bool enabled)
    {
        if (session.Provider != "codex")
        {
            throw new ArgumentException("Native name synchronization is available for Codex sessions only.");
        }
        using var gate = new DataLock(paths, "native-name-" + ContentHash.Compute(session.Key));
        new IdentityStore(paths).Update(session.Key, session.Directory, value =>
        {
            value.NativeSyncEnabled = enabled;
            value.NativeSyncTakeOwnership = enabled;
            value.NativeSyncStatus = enabled ? "pending" : "disabled";
            return true;
        });
    }

    private static void SetStatus(IdentityStore identities, SessionState session, string status) =>
        identities.Update(session.Key, session.Directory, value =>
        {
            if (value.NativeSyncStatus == status || !value.NativeSyncEnabled)
            {
                return false;
            }
            value.NativeSyncStatus = status;
            return true;
        });
}
