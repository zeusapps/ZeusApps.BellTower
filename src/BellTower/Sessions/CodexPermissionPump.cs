using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text.Json;
using BellTower.Core;
using BellTower.IO;
using BellTower.Providers;

namespace BellTower.Sessions;

public sealed class CodexPermissionPump(DataPaths paths, Func<ICodexAttentionClient>? createClient = null) : IDisposable
{
    private sealed record Verification(string Attachment, string? Episode, string Scope, long Revision, bool Pending, DateTimeOffset Since, DateTimeOffset Observed, bool? UserInput = null);
    private sealed record ScopeKey(string Attachment, string? Episode, string Scope, long Revision);
    private sealed record Check(SessionState Session, ScopeKey Scope, WaitState[] PermissionWaits, string[] PermissionScopeIds, string ObservationId,
        string SessionId, string Directory, string? TurnId, bool HasQuestion);
    private readonly ConcurrentDictionary<string, Verification> verified = new();
    private readonly ConcurrentDictionary<string, ScopeKey> checking = new();
    private readonly ConcurrentDictionary<string, ScopeKey> attempted = new();
    private int running;
    private volatile bool disposed;
    private DateTimeOffset next;
    private DateTimeOffset lastEvidence;
    private readonly DateTimeOffset startedAt = DateTimeOffset.UtcNow;

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
        await Task.WhenAll(pending.Select(async check =>
        {
            await concurrency.WaitAsync().ConfigureAwait(false);
            try
            {
                await VerifyAsync(check).ConfigureAwait(false);
            }
            finally
            {
                concurrency.Release();
            }
        })).ConfigureAwait(false);
    }

    private Check[] BeginChecks(IReadOnlyList<SessionState> sessions)
    {
        var pending = sessions.Where(session => session.Provider == "codex" && !session.Ended &&
            session.Waits.Any(wait => wait.Reason is AttentionReason.Permission or AttentionReason.Question))
            .Select(session =>
            {
                var permissionWaits = session.Waits.Where(wait => wait.Reason == AttentionReason.Permission).ToArray();
                var permissionScopeIds = permissionWaits.Select(wait => wait.ConfirmationScopeId).Order(StringComparer.Ordinal).ToArray();
                return new Check(session, new(session.AttachmentId, session.Episode, string.Join('|', permissionScopeIds), session.ApprovalRevision),
                    permissionWaits, permissionScopeIds, Guid.NewGuid().ToString("N"),
                    session.SessionId, session.Directory, session.TurnId,
                    session.Waits.Any(wait => wait.Reason == AttentionReason.Question));
            }).ToArray();
        foreach (var session in pending)
        {
            if (!attempted.TryGetValue(session.Session.Key, out var previous) || previous != session.Scope)
            {
                attempted[session.Session.Key] = session.Scope;
                checking[session.Session.Key] = session.Scope;
            }
        }
        foreach (var key in attempted.Keys.Except(pending.Select(session => session.Session.Key)))
        {
            verified.TryRemove(key, out _);
            checking.TryRemove(key, out _);
            attempted.TryRemove(key, out _);
        }
        return pending;
    }

    private async Task VerifyAsync(Check check)
    {
        var session = check.Session;
        try
        {
            if (disposed)
            {
                return;
            }
            var runtimes = new CodexRuntimeStore(paths);
            var runtime = runtimes.Get(check.Scope.Attachment);
            var attachment = new AttachmentStore(paths).Get(check.Scope.Attachment);
            if (runtime is null || attachment is not { EndedAt: null } || attachment.Host.Shell != runtime.Shell)
            {
                verified.TryRemove(session.Key, out _);
                return;
            }
            using var client = createClient?.Invoke() ?? new CodexConnection();
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(3));
            await client.ConnectAsync(runtime.Endpoint, File.ReadAllText(runtimes.TokenPath(runtime.Attachment)), timeout.Token).ConfigureAwait(false);
            var waiting = await client.ReadPermissionPendingAsync(check.SessionId, timeout.Token).ConfigureAwait(false);
            if (waiting is null)
            {
                verified.TryRemove(session.Key, out _);
                return;
            }
            var now = DateTimeOffset.UtcNow;
            if (now - lastEvidence > TimeSpan.FromMinutes(1))
            {
                lastEvidence = now;
                try
                {
                    ConfirmationEvidence.Record(paths, "codex", ConfirmationEvidence.CodexSignal, null, now);
                }
                catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
                {
                    // Diagnostics evidence is best effort and never affects verification.
                }
            }
            var since = verified.TryGetValue(session.Key, out var previous) && previous.Pending && waiting.Value &&
                previous.Attachment == check.Scope.Attachment && previous.Episode == check.Scope.Episode && previous.Scope == check.Scope.Scope && previous.Revision == check.Scope.Revision &&
                now - previous.Observed <= TimeSpan.FromSeconds(5) ? previous.Since : now;
            // The question dialog status is confirmation evidence only; it never gates or delays a question.
            var userInput = check.HasQuestion
                ? await client.ReadUserInputPendingAsync(check.SessionId, timeout.Token).ConfigureAwait(false) : null;
            verified[session.Key] = new(check.Scope.Attachment, check.Scope.Episode, check.Scope.Scope, check.Scope.Revision, waiting.Value, since, now, userInput);
            if (waiting.Value)
            {
                SubmitConfirmation(check, now);
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or WebSocketException or
                OperationCanceledException or JsonException or InvalidOperationException or ArgumentException)
        {
            verified.TryRemove(session.Key, out _);
            new LocalLog(paths).Write("permission-verification-unavailable");
        }
        finally
        {
            ((ICollection<KeyValuePair<string, ScopeKey>>)checking).Remove(new(session.Key, check.Scope));
        }
    }

    // Runtime confirmation takes the same stored transition as a Claude prompt: the pump publishes a runtime-approval observation
    // that the shared reducer applies, so the episode and receipt handling is identical. The observation keeps its own time (the
    // causal journal order is unchanged); a wait that predates this tray run carries RestoredSince so it is confirmed back-dated
    // and restores silently. The transient flags stay only for freshness and negative-status suppression.
    private void SubmitConfirmation(Check check, DateTimeOffset now)
    {
        var session = check.Session;
        var permissions = check.PermissionWaits;
        // While a confirmed permission wait is outstanding, a continuing positive status is that wait's evidence: it cannot be
        // attributed to newer requests, so nothing is published and no deterministic ID is consumed for them.
        if (permissions.Any(wait => wait.Confirmation == HumanConfirmation.Confirmed))
        {
            return;
        }
        var provisional = permissions.Where(wait => wait.Confirmation != HumanConfirmation.Confirmed).ToList();
        if (provisional.Count == 0)
        {
            return;
        }
        var oldest = provisional.Min(wait => wait.Since);
        try
        {
            new EventStore(paths).Publish(new Observation
            {
                Id = check.ObservationId,
                Provider = session.Provider,
                SessionId = check.SessionId,
                AttachmentId = check.Scope.Attachment,
                Kind = EventKind.Permission,
                At = now,
                RestoredSince = oldest < startedAt ? startedAt : null,
                RuntimeApprovalScope = [.. check.PermissionScopeIds],
                RuntimeApprovalRevision = check.Scope.Revision,
                Directory = check.Directory,
                TurnId = check.TurnId,
                HumanPrompt = true,
                RuntimeApproval = true,
                AdapterRevision = Observation.HumanWaitRevision
            });
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            new LocalLog(paths).Write("permission-confirmation-unavailable");
        }
    }

    public void Apply(IReadOnlyList<SessionState> sessions, DateTimeOffset now)
    {
        foreach (var session in sessions)
        {
            session.ConfirmationSignalUnavailable = false;
            session.NativePermissionPending = null;
            session.NativePermissionSince = null;
            session.NativeUserInputPending = null;
            var scope = Scope(session.Waits.Where(wait => wait.Reason == AttentionReason.Permission));
            if (verified.TryGetValue(session.Key, out var value) && value.Attachment == session.AttachmentId &&
                value.Episode == session.Episode && value.Scope == scope && value.Revision == session.ApprovalRevision && now - value.Observed <= TimeSpan.FromSeconds(5))
            {
                session.NativePermissionPending = value.Pending;
                session.NativePermissionSince = value.Pending ? value.Since : null;
                session.NativeUserInputPending = value.UserInput;
            }
            else if (checking.TryGetValue(session.Key, out var pending) && pending.Attachment == session.AttachmentId &&
                pending.Episode == session.Episode && pending.Scope == scope && pending.Revision == session.ApprovalRevision)
            {
                // Do not flash an already-due chip during the first bounded status check.
                session.NativePermissionPending = false;
            }
            else if (session.Provider == "codex")
            {
                // No verified owning runtime, or its status read failed or went stale: no supported signal for this session now.
                session.ConfirmationSignalUnavailable = true;
            }
        }
    }

    private static string Scope(IEnumerable<WaitState> waits) =>
        string.Join('|', waits.Select(wait => wait.ConfirmationScopeId).Order(StringComparer.Ordinal));

    public void Dispose() => disposed = true;
}
