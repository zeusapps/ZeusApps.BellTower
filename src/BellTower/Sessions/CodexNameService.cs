using System.Net.WebSockets;
using System.Text.Json;
using BellTower.Core;
using BellTower.IO;
using BellTower.Providers;

namespace BellTower.Sessions;

public sealed class CodexNameService(DataPaths paths, Func<ICodexNameClient>? createClient = null, NativeNameSchedule? schedule = null)
{
    public static readonly TimeSpan VerifyEvery = TimeSpan.FromSeconds(10);

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
        string? scheduledName = null;
        try
        {
            var desired = SessionPresentation.CodexName(identity.Name, context ?? new GitReader().Read(identity.Checkout(session.Directory)));
            if (schedule is not null && !schedule.Due(session.Key, desired, identity.NativeSyncTakeOwnership || identity.NativeSyncStatus == "pending"))
            {
                return;
            }
            scheduledName = desired;
            var runtimes = new CodexRuntimeStore(paths);
            var runtime = runtimes.Get(session.AttachmentId);
            var attachment = new AttachmentStore(paths).Get(session.AttachmentId);
            if (runtime is null || attachment is not { EndedAt: null } || attachment.Host.Shell != runtime.Shell)
            {
                SetStatus(identities, session, "unavailable: no verified dedicated runtime");
                schedule?.Failed(session.Key, desired);
                return;
            }
            var token = File.ReadAllText(runtimes.TokenPath(runtime.Attachment));
            using var client = createClient?.Invoke() ?? new CodexConnection();
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(3));
            client.ConnectAsync(runtime.Endpoint, token, timeout.Token).GetAwaiter().GetResult();
            var current = client.ReadNameAsync(session.SessionId, timeout.Token).GetAwaiter().GetResult();
            if (NativeNamePolicy.Conflict(identity, current))
            {
                identities.Update(session.Key, session.Directory, value =>
                {
                    NativeNamePolicy.Pause(value, current);
                    return true;
                });
                schedule?.Succeeded(session.Key, desired);
                return;
            }
            if (current != desired)
            {
                // Record the intended name before the write so a crash or a concurrent reader between the
                // write and the bookkeeping never reads Bell Tower's own name as a user rename.
                identities.Update(session.Key, session.Directory, value => value.NativeSyncEnabled && NativeNamePolicy.RecordIntent(value, desired));
                client.SetNameAsync(session.SessionId, desired, timeout.Token).GetAwaiter().GetResult();
            }
            // Preserve a concurrent user request to disable synchronization.
            identities.Update(session.Key, session.Directory, value => value.NativeSyncEnabled && NativeNamePolicy.Confirm(value, desired));
            schedule?.Succeeded(session.Key, desired);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or WebSocketException or
            OperationCanceledException or JsonException or InvalidOperationException or ArgumentException)
        {
            schedule?.Failed(session.Key, scheduledName ?? "");
            SetStatus(identities, session, "unavailable: native metadata connection failed");
            new LocalLog(paths).Write("native-name-unavailable");
        }
    }

    public void SetEnabled(SessionState session, bool enabled) => NativeNameControl.SetEnabled(paths, session, enabled);

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
