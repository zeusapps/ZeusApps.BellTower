using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text.Json;
using BellTower.Core;
using BellTower.IO;
using BellTower.Providers;

namespace BellTower.Sessions;

public sealed class CodexPermissionPump(DataPaths paths, Func<ICodexAttentionClient>? createClient = null) : IDisposable
{
    private sealed record Verification(string Attachment, string? Episode, bool Pending, DateTimeOffset Since, DateTimeOffset Observed);
    private readonly ConcurrentDictionary<string, Verification> verified = new();
    private readonly ConcurrentDictionary<string, (string Attachment, string? Episode)> checking = new();
    private readonly ConcurrentDictionary<string, (string Attachment, string? Episode)> attempted = new();
    private int running;
    private volatile bool disposed;
    private DateTimeOffset next;

    public void Update(IReadOnlyList<SessionState> sessions)
    {
        if (disposed || DateTimeOffset.UtcNow < next || Interlocked.CompareExchange(ref running, 1, 0) != 0)
        {
            return;
        }
        next = DateTimeOffset.UtcNow.AddSeconds(1);
        BeginChecks(sessions);
        _ = Task.Run(async () =>
        {
            try
            {
                await RefreshAsync(sessions).ConfigureAwait(false);
            }
            finally
            {
                Interlocked.Exchange(ref running, 0);
            }
        });
    }

    public async Task RefreshAsync(IReadOnlyList<SessionState> sessions)
    {
        var pending = BeginChecks(sessions);
        using var concurrency = new SemaphoreSlim(4);
        await Task.WhenAll(pending.Select(async session =>
        {
            await concurrency.WaitAsync().ConfigureAwait(false);
            try
            {
                await VerifyAsync(session).ConfigureAwait(false);
            }
            finally
            {
                concurrency.Release();
            }
        })).ConfigureAwait(false);
    }

    private SessionState[] BeginChecks(IReadOnlyList<SessionState> sessions)
    {
        var pending = sessions.Where(session => session.Provider == "codex" && !session.Ended &&
            session.Waits.Any(wait => wait.Reason == AttentionReason.Permission)).ToArray();
        foreach (var session in pending)
        {
            var scope = (session.AttachmentId, session.Episode);
            if (!attempted.TryGetValue(session.Key, out var previous) || previous != scope)
            {
                attempted[session.Key] = scope;
                checking[session.Key] = scope;
            }
        }
        foreach (var key in attempted.Keys.Except(pending.Select(session => session.Key)))
        {
            verified.TryRemove(key, out _);
            checking.TryRemove(key, out _);
            attempted.TryRemove(key, out _);
        }
        return pending;
    }

    private async Task VerifyAsync(SessionState session)
    {
        try
        {
            if (disposed)
            {
                return;
            }
            var runtimes = new CodexRuntimeStore(paths);
            var runtime = runtimes.Get(session.AttachmentId);
            var attachment = new AttachmentStore(paths).Get(session.AttachmentId);
            if (runtime is null || attachment is not { EndedAt: null } || attachment.Host.Shell != runtime.Shell)
            {
                verified.TryRemove(session.Key, out _);
                return;
            }
            using var client = createClient?.Invoke() ?? new CodexConnection();
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(3));
            await client.ConnectAsync(runtime.Endpoint, File.ReadAllText(runtimes.TokenPath(runtime.Attachment)), timeout.Token).ConfigureAwait(false);
            var waiting = await client.ReadPermissionPendingAsync(session.SessionId, timeout.Token).ConfigureAwait(false);
            if (waiting is null)
            {
                verified.TryRemove(session.Key, out _);
                return;
            }
            var now = DateTimeOffset.UtcNow;
            var since = verified.TryGetValue(session.Key, out var previous) && previous.Pending && waiting.Value &&
                previous.Attachment == session.AttachmentId && previous.Episode == session.Episode &&
                now - previous.Observed <= TimeSpan.FromSeconds(5) ? previous.Since : now;
            verified[session.Key] = new(session.AttachmentId, session.Episode, waiting.Value, since, now);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or WebSocketException or
                OperationCanceledException or JsonException or InvalidOperationException or ArgumentException)
        {
            verified.TryRemove(session.Key, out _);
            new LocalLog(paths).Write("permission-verification-unavailable");
        }
        finally
        {
            checking.TryRemove(session.Key, out _);
        }
    }

    public void Apply(IReadOnlyList<SessionState> sessions, DateTimeOffset now)
    {
        foreach (var session in sessions)
        {
            session.NativePermissionPending = null;
            session.NativePermissionSince = null;
            if (verified.TryGetValue(session.Key, out var value) && value.Attachment == session.AttachmentId &&
                value.Episode == session.Episode && now - value.Observed <= TimeSpan.FromSeconds(5))
            {
                session.NativePermissionPending = value.Pending;
                session.NativePermissionSince = value.Pending ? value.Since : null;
            }
            else if (checking.TryGetValue(session.Key, out var pending) && pending.Attachment == session.AttachmentId &&
                pending.Episode == session.Episode)
            {
                // Do not flash an already-due chip during the first bounded status check.
                session.NativePermissionPending = false;
            }
        }
    }

    public void Dispose() => disposed = true;
}
