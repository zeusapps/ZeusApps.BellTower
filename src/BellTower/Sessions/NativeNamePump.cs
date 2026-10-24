using BellTower.Core;
using BellTower.IO;

namespace BellTower.Sessions;

// Drives the shipped Codex native-name synchronization. The service is long-lived and change-driven through its
// NativeNameSchedule, so an idle session costs a local file read and nothing else. Claude has no native write in
// this release (deferred to sync-native-session-titles), so Claude sessions are not visited.
public sealed class NativeNamePump : IDisposable
{
    private readonly DataPaths paths;
    private readonly CheckoutCache checkout;
    private readonly CodexNameService codex;
    private readonly NativeNameSchedule codexSchedule;
    private int running;
    private volatile bool disposed;
    private DateTimeOffset next;

    public NativeNamePump(DataPaths paths, CheckoutCache? checkout = null, CodexNameService? codex = null, NativeNameSchedule? codexSchedule = null)
    {
        this.paths = paths;
        this.checkout = checkout ?? new();
        this.codexSchedule = codexSchedule ?? new(CodexNameService.VerifyEvery);
        this.codex = codex ?? new CodexNameService(paths, schedule: this.codexSchedule);
    }

    public void Update(IReadOnlyList<SessionState> sessions)
    {
        if (disposed || DateTimeOffset.UtcNow < next || Interlocked.CompareExchange(ref running, 1, 0) != 0)
        {
            return;
        }
        next = DateTimeOffset.UtcNow.AddSeconds(2);
        _ = Task.Run(async () => await RunAsync(sessions).ConfigureAwait(false));
    }

    public async Task RunAsync(IReadOnlyList<SessionState> sessions)
    {
        try
        {
            var live = sessions.Where(value => value.Provider == "codex" && !value.Ended && value.AttachmentHealth != "unavailable").ToArray();
            codexSchedule.Retain(live.Select(value => value.Key));
            foreach (var session in live)
            {
                if (disposed)
                {
                    break;
                }
                var identity = new IdentityStore(paths).Get(session.Key, session.Directory);
                codex.Sync(session, await checkout.ReadAsync(identity.Checkout(session.Directory)).ConfigureAwait(false));
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or System.Text.Json.JsonException)
        {
            new LocalLog(paths).Write("native-name-refresh-failed");
        }
        finally
        {
            Interlocked.Exchange(ref running, 0);
        }
    }

    public void Dispose() => disposed = true;
}
