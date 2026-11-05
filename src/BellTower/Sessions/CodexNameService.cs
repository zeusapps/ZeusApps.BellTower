using System.Net.WebSockets;
using System.Text.Json;
using BellTower.Core;
using BellTower.IO;
using BellTower.Providers;

namespace BellTower.Sessions;

/// <summary>
/// Codex native-name synchronization over the one runtime opener. <paramref name="connect"/> replaces the production connector
/// (tests); it receives the selected access and returns an already connected client.
/// </summary>
public sealed class CodexNameService(DataPaths paths, Func<CodexAccess, CancellationToken, Task<ICodexNameClient>>? connect = null,
    NativeNameSchedule? schedule = null, CodexRuntimeOpener? opener = null)
{
    private readonly CodexRuntimeOpener opener = opener ?? new(paths);

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
        CodexAccess? access = null;
        try
        {
            var desired = SessionPresentation.CodexName(identity.Name, context ?? new GitReader().Read(identity.Checkout(session.Directory)));
            if (schedule is not null && !schedule.Due(session.Key, desired, identity.NativeSyncTakeOwnership || identity.NativeSyncStatus == "pending"))
            {
                return;
            }
            scheduledName = desired;
            access = opener.Select(session);
            if (access is null)
            {
                SetStatus(identities, session, CodexAccessStatus.NoRuntime);
                schedule?.Failed(session.Key, desired);
                return;
            }
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(3));
            using var client = OpenAsync(access, timeout.Token).GetAwaiter().GetResult();
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
            var status = access is null ? CodexAccessStatus.ConnectionFailed : CodexAccessStatus.For(access, exception);
            if (status is null)
            {
                // A backoff in progress: nothing was attempted, so neither the status nor the schedule changes.
                return;
            }
            schedule?.Failed(session.Key, scheduledName ?? "");
            SetStatus(identities, session, status);
            new LocalLog(paths).Write("native-name-unavailable");
        }
    }

    private async Task<ICodexNameClient> OpenAsync(CodexAccess access, CancellationToken cancellation) =>
        connect is null ? await opener.ConnectAsync(access, cancellation).ConfigureAwait(false) : await connect(access, cancellation).ConfigureAwait(false);

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
