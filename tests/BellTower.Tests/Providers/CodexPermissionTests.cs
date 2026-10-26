using System.Text.Json;
using BellTower.Core;
using BellTower.IO;
using BellTower.Providers;
using BellTower.Sessions;
using BellTower.Tests.Testing;
using Xunit;

namespace BellTower.Tests.Providers;

public sealed class CodexPermissionTests
{
    private sealed class Status
    {
        public bool? Pending { get; set; }
        public bool? UserInput { get; set; }
        public bool Fail { get; set; }
        public string? Thread { get; set; }
        public Task? ConnectWait { get; set; }
        public TaskCompletionSource? Read { get; set; }
    }

    private sealed class Client(Status status) : ICodexAttentionClient
    {
        public Task ConnectAsync(string endpoint, string token, CancellationToken cancellation) =>
            status.Fail ? throw new IOException("fixture unavailable") : status.ConnectWait ?? Task.CompletedTask;
        public Task<bool?> ReadPermissionPendingAsync(string thread, CancellationToken cancellation)
        {
            status.Thread = thread;
            status.Read?.TrySetResult();
            return Task.FromResult(status.Pending);
        }
        public Task<bool?> ReadUserInputPendingAsync(string thread, CancellationToken cancellation) => Task.FromResult(status.UserInput);
        public void Dispose() { }
    }

    private sealed class DelayedStatus : ICodexAttentionClient
    {
        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource<bool?> Result { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public Task ConnectAsync(string endpoint, string token, CancellationToken cancellation) => Task.CompletedTask;
        public Task<bool?> ReadPermissionPendingAsync(string thread, CancellationToken cancellation)
        {
            Started.TrySetResult();
            return Result.Task;
        }
        public void Dispose() { }
    }

    private static SessionState Prepare(TestWorkspace workspace, string thread = "one", EventKind kind = EventKind.Permission)
    {
        var process = ProcessProbe.Identify(Environment.ProcessId)!;
        var runtime = new CodexRuntime("runtime-" + thread, "ws://127.0.0.1:12345", process, process);
        var runtimes = new CodexRuntimeStore(workspace.Paths);
        Directory.CreateDirectory(Path.GetDirectoryName(runtimes.RecordPath(runtime.Attachment))!);
        AtomicFile.Write(runtimes.RecordPath(runtime.Attachment), BellTower.IO.Json.Encode(runtime));
        File.WriteAllText(runtimes.TokenPath(runtime.Attachment), "fixture-token");
        new AttachmentStore(workspace.Paths).Save(new(runtime.Attachment, "codex", process, new("unknown", process), workspace.Root, DateTimeOffset.UtcNow));
        var state = new AppState();
        var permission = TestWorkspace.Event(kind, DateTimeOffset.UtcNow.AddMinutes(-1), "tool") with
        {
            Provider = "codex",
            SessionId = thread,
            AttachmentId = runtime.Attachment,
            Directory = workspace.Root
        };
        SessionReducer.Apply(state, permission);
        return state.Sessions[permission.Key];
    }

    [Theory]
    [InlineData("{\"type\":\"active\",\"activeFlags\":[\"waitingOnApproval\"]}", true)]
    [InlineData("{\"type\":\"active\",\"activeFlags\":[]}", false)]
    [InlineData("{\"type\":\"active\",\"activeFlags\":[\"waitingOnUserInput\"]}", false)]
    [InlineData("{\"type\":\"idle\"}", false)]
    [InlineData("{\"type\":\"notLoaded\"}", null)]
    [InlineData("{\"type\":\"active\"}", null)]
    public void NativeStatusDistinguishesHumanApprovalFromOtherActivity(string status, bool? expected)
    {
        using var thread = JsonDocument.Parse("{\"status\":" + status + "}");
        Assert.Equal(expected, CodexConnection.PermissionPending(thread.RootElement));
    }

    [Theory]
    [InlineData("{\"type\":\"active\",\"activeFlags\":[\"waitingOnUserInput\"]}", true)]
    [InlineData("{\"type\":\"active\",\"activeFlags\":[\"waitingOnApproval\"]}", false)]
    [InlineData("{\"type\":\"idle\"}", false)]
    [InlineData("{\"type\":\"notLoaded\"}", null)]
    public void NativeStatusReportsAQuestionDialogSeparatelyFromAnApproval(string status, bool? expected)
    {
        using var thread = JsonDocument.Parse("{\"status\":" + status + "}");
        Assert.Equal(expected, CodexConnection.UserInputPending(thread.RootElement));
    }

    [Theory]
    [InlineData(true, HumanConfirmation.Confirmed)]
    [InlineData(false, HumanConfirmation.Provisional)]
    [InlineData(null, HumanConfirmation.Provisional)]
    public async Task RuntimeQuestionStatusIsConfirmationEvidenceAndNeverGatesTheQuestion(bool? userInput, HumanConfirmation expected)
    {
        using var workspace = new TestWorkspace();
        var session = Prepare(workspace, kind: EventKind.Question);
        var status = new Status { Pending = false, UserInput = userInput };
        using var pump = new CodexPermissionPump(workspace.Paths, () => new Client(status));
        var now = DateTimeOffset.UtcNow;
        // Before the first check and after every answer the question is attention: its timing is unchanged.
        Assert.True(session.RequiresAttention(now));
        await pump.RefreshAsync([session]);
        pump.Apply([session], now);
        Assert.Equal(userInput, session.NativeUserInputPending);
        Assert.Equal(AttentionReason.Question, session.Reason);
        Assert.True(session.RequiresAttention(now));
        Assert.Equal(expected, session.EffectiveConfirmation(Assert.Single(session.Waits)));
    }

    [Fact]
    public async Task AutomaticReviewOrRunningCommandNeverCreatesPermissionAttention()
    {
        using var workspace = new TestWorkspace();
        var session = Prepare(workspace);
        var status = new Status { Pending = false };
        using var pump = new CodexPermissionPump(workspace.Paths, () => new Client(status));
        await pump.RefreshAsync([session]);
        var now = DateTimeOffset.UtcNow;
        pump.Apply([session], now);
        Assert.Equal("one", status.Thread);
        Assert.False(session.NativePermissionPending);
        Assert.False(session.RequiresAttention(now));
        Assert.Null(session.NotificationDue);
        Assert.Empty(new AlertPolicy(now.AddMinutes(-2)).Collect([session], now, false));
        Assert.Single(session.Waits);
        Assert.Empty(Directory.GetFiles(workspace.Paths.Inbox));
    }

    [Fact]
    public async Task HumanApprovalStartsGraceAtConfirmationAndResolutionHidesItBeforeToolCompletion()
    {
        using var workspace = new TestWorkspace();
        var session = Prepare(workspace);
        var originalSince = session.Since;
        var originalEpisode = session.Episode;
        var status = new Status { Pending = true };
        using var pump = new CodexPermissionPump(workspace.Paths, () => new Client(status));
        await pump.RefreshAsync([session]);
        pump.Apply([session], DateTimeOffset.UtcNow);
        var due = session.NativePermissionSince!.Value.AddSeconds(5);
        Assert.Equal(due, session.Due);
        Assert.False(session.RequiresAttention(due.AddTicks(-1)));
        Assert.True(session.RequiresAttention(due));
        status.Pending = false;
        await pump.RefreshAsync([session]);
        pump.Apply([session], DateTimeOffset.UtcNow);
        Assert.False(session.RequiresAttention(due));
        Assert.Single(session.Waits);
        Assert.Equal(originalSince, session.Since);
        Assert.Equal(originalEpisode, session.Episode);
    }

    [Fact]
    public async Task FailedUnknownOrStaleVerificationLeavesTheRequestUnverifiedNeverConfirmed()
    {
        using var workspace = new TestWorkspace();
        var session = Prepare(workspace);
        var status = new Status { Pending = false };
        using var pump = new CodexPermissionPump(workspace.Paths, () => new Client(status));
        await pump.RefreshAsync([session]);
        pump.Apply([session], DateTimeOffset.UtcNow.AddSeconds(6));
        Assert.Null(session.NativePermissionPending);
        // Elapsed hook time alone never establishes a human wait.
        Assert.Null(session.Due);
        Assert.Null(session.Reason);
        Assert.False(session.RequiresAttention(DateTimeOffset.UtcNow.AddMinutes(10)));
        status.Fail = true;
        await pump.RefreshAsync([session]);
        pump.Apply([session], DateTimeOffset.UtcNow);
        Assert.Null(session.NativePermissionPending);
        Assert.False(session.RequiresAttention(DateTimeOffset.UtcNow.AddMinutes(10)));
        status.Fail = false;
        status.Pending = null;
        await pump.RefreshAsync([session]);
        pump.Apply([session], DateTimeOffset.UtcNow);
        Assert.Null(session.NativePermissionPending);
        Assert.False(session.RequiresAttention(DateTimeOffset.UtcNow.AddMinutes(10)));
        Assert.Single(session.Waits);
    }

    [Fact]
    public async Task FreshConfirmationCannotReplayAnAlreadyDueWaitAfterTrayRestart()
    {
        using var workspace = new TestWorkspace();
        var session = Prepare(workspace);
        var restartedAt = DateTimeOffset.UtcNow;
        using var pump = new CodexPermissionPump(workspace.Paths, () => new Client(new Status { Pending = true }));
        await pump.RefreshAsync([session]);
        pump.Apply([session], DateTimeOffset.UtcNow);
        Assert.True(session.RequiresAttention(session.Due!.Value));
        Assert.Empty(new AlertPolicy(restartedAt).Collect([session], session.Due.Value, false));
    }

    [Fact]
    public async Task AlreadyDueHookWaitDoesNotFlashDuringInitialVerification()
    {
        using var workspace = new TestWorkspace();
        var session = Prepare(workspace);
        var ready = new TaskCompletionSource();
        var status = new Status { Pending = false, ConnectWait = ready.Task };
        using var pump = new CodexPermissionPump(workspace.Paths, () => new Client(status));
        var refresh = pump.RefreshAsync([session]);
        try
        {
            pump.Apply([session], DateTimeOffset.UtcNow);
            Assert.False(session.RequiresAttention(DateTimeOffset.UtcNow));
        }
        finally
        {
            ready.SetResult();
            await refresh;
        }
        pump.Apply([session], DateTimeOffset.UtcNow);
        Assert.False(session.RequiresAttention(DateTimeOffset.UtcNow));
    }

    [Fact]
    public async Task RetryingUnavailableVerificationStaysUnverifiedAndEachEpisodeKeepsItsInitialCheckGuard()
    {
        using var workspace = new TestWorkspace();
        var session = Prepare(workspace);
        var status = new Status { Pending = null };
        using var pump = new CodexPermissionPump(workspace.Paths, () => new Client(status));
        await pump.RefreshAsync([session]);
        var ready = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        status.ConnectWait = ready.Task;
        var refresh = pump.RefreshAsync([session]);
        try
        {
            pump.Apply([session], DateTimeOffset.UtcNow);
            Assert.Null(session.NativePermissionPending);
            Assert.False(session.RequiresAttention(DateTimeOffset.UtcNow));
        }
        finally
        {
            ready.SetResult();
            await refresh;
        }
        session.Episode = "new-permission";
        var newReady = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        status.ConnectWait = newReady.Task;
        // Each new episode still gets its own initial-check guard.
        refresh = pump.RefreshAsync([session]);
        try
        {
            pump.Apply([session], DateTimeOffset.UtcNow);
            Assert.False(session.NativePermissionPending);
        }
        finally
        {
            newReady.SetResult();
            await refresh;
        }
    }

    [Fact]
    public async Task SlowSessionCannotBlockVerificationOfAnotherSession()
    {
        using var workspace = new TestWorkspace();
        var slow = Prepare(workspace);
        var fast = Prepare(workspace, "two");
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var read = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var statuses = new Queue<Status>([new() { ConnectWait = release.Task }, new() { Pending = false, Read = read }]);
        using var pump = new CodexPermissionPump(workspace.Paths, () => new Client(statuses.Dequeue()));
        var refresh = pump.RefreshAsync([slow, fast]);
        try
        {
            await read.Task.WaitAsync(TimeSpan.FromSeconds(2));
            pump.Apply([fast], DateTimeOffset.UtcNow);
            Assert.False(fast.NativePermissionPending);
            Assert.False(refresh.IsCompleted);
        }
        finally
        {
            release.SetResult();
            await refresh;
        }
    }

    [Fact]
    public void PermissionVerificationCannotSuppressANewErrorNotification()
    {
        using var workspace = new TestWorkspace();
        var session = Prepare(workspace);
        var state = new AppState();
        state.Sessions[session.Key] = session;
        var now = DateTimeOffset.UtcNow;
        var first = TestWorkspace.Event(EventKind.Failure, now.AddMinutes(-1), "old-error") with
        {
            Provider = "codex",
            SessionId = session.SessionId,
            AttachmentId = session.AttachmentId
        };
        SessionReducer.Apply(state, first);
        session.NativePermissionPending = false;
        SessionReducer.Apply(state, first with { Id = "new-error", OperationId = "new-error", At = now });
        Assert.Single(new AlertPolicy(now.AddSeconds(-1)).Collect([session], now, false));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task VerificationCannotLeakIntoAnotherAttachmentOrEpisode(bool changeAttachment)
    {
        using var workspace = new TestWorkspace();
        var session = Prepare(workspace);
        var status = new Status { Pending = false };
        using var pump = new CodexPermissionPump(workspace.Paths, () => new Client(status));
        await pump.RefreshAsync([session]);
        if (changeAttachment)
        { session.AttachmentId = "replacement"; }
        else
        { session.Episode = "next-permission"; }
        pump.Apply([session], DateTimeOffset.UtcNow);
        Assert.Null(session.NativePermissionPending);
    }

    [Fact]
    public async Task AReportedHumanApprovalIsStoredThroughTheSharedConfirmationTransitionAndRestoresSilently()
    {
        using var workspace = new TestWorkspace();
        var prepared = Prepare(workspace);
        var since = prepared.Waits[0].Since;
        new EventStore(workspace.Paths).Publish(TestWorkspace.Event(EventKind.Permission, since, "tool") with
        {
            Provider = "codex",
            SessionId = "one",
            AttachmentId = prepared.AttachmentId,
            Directory = workspace.Root
        });
        var session = new StateStore(workspace.Paths).Drain().Sessions["codex:one"];
        Assert.Equal(HumanConfirmation.Provisional, Assert.Single(session.Waits).Confirmation);
        var status = new Status { Pending = true };
        using var pump = new CodexPermissionPump(workspace.Paths, () => new Client(status));
        await pump.RefreshAsync([session]);
        Assert.Single(Directory.GetFiles(workspace.Paths.Inbox));
        var stored = new StateStore(workspace.Paths).Drain().Sessions["codex:one"];
        var wait = Assert.Single(stored.Waits);
        Assert.Equal(HumanConfirmation.Confirmed, wait.Confirmation);
        // A wait that predates this tray run is confirmed back-dated, so it restores silently like a confirmed Claude wait.
        Assert.Equal(since.AddMilliseconds(1), wait.ConfirmedAt);
        // The request was provisional for over the unverified grace, so confirmation starts a fresh episode (QA-100).
        Assert.NotEqual(session.Episode, stored.Episode);
        var now = DateTimeOffset.UtcNow;
        Assert.Null(stored.NativePermissionPending);
        Assert.True(stored.RequiresAttention(now));
        Assert.Empty(new AlertPolicy(now).Collect([stored], now, false));
        // A confirmed wait is not published again, and a verified negative status still suppresses it.
        await pump.RefreshAsync([stored]);
        Assert.Empty(Directory.GetFiles(workspace.Paths.Inbox));
        status.Pending = false;
        await pump.RefreshAsync([stored]);
        pump.Apply([stored], DateTimeOffset.UtcNow);
        Assert.False(stored.RequiresAttention(DateTimeOffset.UtcNow));
        Assert.Single(stored.Waits);
    }

    [Fact]
    public async Task SessionsWithoutASupportedSignalAreMarkedUnavailableWhileAVerifiedNegativeIsNot()
    {
        using var workspace = new TestWorkspace();
        var session = Prepare(workspace);
        var status = new Status { Pending = false };
        using var pump = new CodexPermissionPump(workspace.Paths, () => new Client(status));
        await pump.RefreshAsync([session]);
        pump.Apply([session], DateTimeOffset.UtcNow);
        Assert.False(session.ConfirmationSignalUnavailable);
        Assert.False(session.PossiblyWaiting(DateTimeOffset.UtcNow));
        status.Fail = true;
        await pump.RefreshAsync([session]);
        pump.Apply([session], DateTimeOffset.UtcNow);
        Assert.True(session.ConfirmationSignalUnavailable);
        Assert.True(session.PossiblyWaiting(DateTimeOffset.UtcNow));
        Assert.False(session.RequiresAttention(DateTimeOffset.UtcNow));
        status.Fail = false;
        status.Pending = null;
        await pump.RefreshAsync([session]);
        pump.Apply([session], DateTimeOffset.UtcNow);
        Assert.True(session.ConfirmationSignalUnavailable);
        status.Pending = true;
        await pump.RefreshAsync([session]);
        pump.Apply([session], DateTimeOffset.UtcNow);
        Assert.False(session.ConfirmationSignalUnavailable);
        Assert.False(session.PossiblyWaiting(DateTimeOffset.UtcNow));
    }

    [Fact]
    public async Task ACodexSessionWithNoRuntimeIsUnavailableAndAClaudeSessionIsNeverMarked()
    {
        using var workspace = new TestWorkspace();
        var state = new AppState();
        var at = DateTimeOffset.UtcNow.AddMinutes(-1);
        SessionReducer.Apply(state, TestWorkspace.Event(EventKind.Permission, at, "tool") with { Provider = "codex", AttachmentId = "no-runtime" });
        SessionReducer.Apply(state, TestWorkspace.Event(EventKind.Permission, at, "tool") with { Provider = "claude" });
        var codex = state.Sessions["codex:session-1"];
        var claude = state.Sessions["claude:session-1"];
        using var pump = new CodexPermissionPump(workspace.Paths, () => new Client(new Status { Pending = true }));
        await pump.RefreshAsync([codex, claude]);
        pump.Apply([codex, claude], DateTimeOffset.UtcNow);
        Assert.True(codex.PossiblyWaiting(DateTimeOffset.UtcNow));
        Assert.False(claude.ConfirmationSignalUnavailable);
        Assert.False(claude.PossiblyWaiting(DateTimeOffset.UtcNow));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AContinuingPositiveRuntimeStatusIsNeverCreditedToANewerRequestThroughTheRealPump(bool causal)
    {
        // ANALYSIS-002 (P2/P9): P1 runtime-confirmed and dismissed, P2 arrives while the runtime still reports pending.
        using var workspace = new TestWorkspace();
        var prepared = Prepare(workspace);
        var events = new EventStore(workspace.Paths);
        Observation Request(string operation, DateTimeOffset at) => TestWorkspace.Event(EventKind.Permission, at, operation) with
        {
            Provider = "codex",
            SessionId = "one",
            AttachmentId = prepared.AttachmentId,
            Directory = workspace.Root,
            IngressAt = causal ? at : null
        };
        events.Publish(Request("tool", prepared.Waits[0].Since));
        var store = new StateStore(workspace.Paths);
        var first = store.Drain().Sessions["codex:one"];
        var status = new Status { Pending = true };
        using var pump = new CodexPermissionPump(workspace.Paths, () => new Client(status));
        await pump.RefreshAsync([first]);
        var confirmed = store.Drain().Sessions["codex:one"];
        Assert.Equal(HumanConfirmation.Confirmed, Assert.Single(confirmed.Waits).Confirmation);
        var episode = confirmed.Episode;
        store.Acknowledge("codex:one");
        events.Publish(Request("tool-2", DateTimeOffset.UtcNow));
        var both = store.Drain().Sessions["codex:one"];
        Assert.Equal(2, both.Waits.Count);
        await pump.RefreshAsync([both]);
        // The pump publishes nothing for the newer request while a confirmed wait is outstanding.
        Assert.Empty(Directory.GetFiles(workspace.Paths.Inbox));
        var after = store.Drain().Sessions["codex:one"];
        Assert.Equal(episode, after.Episode);
        Assert.Equal(HumanConfirmation.Provisional, after.Waits.Single(wait => wait.OperationId == "tool-2").Confirmation);
        var now = DateTimeOffset.UtcNow.AddMinutes(1);
        Assert.False(after.RequiresAttention(now));
        Assert.Empty(new AlertPolicy(DateTimeOffset.UtcNow.AddMinutes(-5)).Collect([after], now, false));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AConsumedStaleStatusCannotConfirmAnAbaPermissionSetOrSuppressFreshEvidence(bool causal)
    {
        using var workspace = new TestWorkspace();
        var prepared = Prepare(workspace);
        var events = new EventStore(workspace.Paths);
        Observation Request(EventKind kind, string operation, DateTimeOffset at) => TestWorkspace.Event(kind, at, operation) with
        {
            Provider = "codex",
            SessionId = "one",
            AttachmentId = prepared.AttachmentId,
            Directory = workspace.Root,
            IngressAt = causal ? at : null
        };
        events.Publish(Request(EventKind.Permission, "A", DateTimeOffset.UtcNow));
        var store = new StateStore(workspace.Paths);
        var first = store.Drain().Sessions["codex:one"];
        var delayed = new DelayedStatus();
        var ready = new Status { Pending = true };
        var clients = new Queue<ICodexAttentionClient>([delayed, new Client(ready)]);
        using var pump = new CodexPermissionPump(workspace.Paths, () => clients.Dequeue());

        var oldRead = pump.RefreshAsync([first]);
        await delayed.Started.Task.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.Empty(Directory.GetFiles(workspace.Paths.Inbox));

        // The old query is unresolved while a second automatic review begins. Its positive result is consumed against A+B.
        var resolvedAt = DateTimeOffset.UtcNow;
        events.Publish(Request(EventKind.Permission, "B", resolvedAt));
        var current = store.Drain().Sessions["codex:one"];
        Assert.Equal(2, current.Waits.Count);
        Assert.All(current.Waits, wait => Assert.Equal(HumanConfirmation.Provisional, wait.Confirmation));

        // The transient cache and in-flight marker are both scoped to the old request set.
        pump.Apply([current], resolvedAt.AddMilliseconds(2));
        Assert.Null(current.NativePermissionPending);
        Assert.False(current.RequiresAttention(resolvedAt.AddMinutes(1)));

        delayed.Result.SetResult(true);
        await oldRead;
        pump.Apply([current], resolvedAt.AddMilliseconds(3));
        Assert.Null(current.NativePermissionPending);
        Assert.False(current.RequiresAttention(resolvedAt.AddMinutes(1)));
        Assert.Single(Directory.GetFiles(workspace.Paths.Inbox));

        // Consumption/replay also checks the captured scope, so the old observation is harmless.
        var afterStale = store.Drain().Sessions["codex:one"];
        Assert.All(afterStale.Waits, wait => Assert.Equal(HumanConfirmation.Provisional, wait.Confirmation));
        Assert.False(afterStale.RequiresAttention(resolvedAt.AddMinutes(1)));
        Assert.Empty(new AlertPolicy(resolvedAt).Collect([afterStale], resolvedAt.AddMinutes(1), false));

        // B resolves, returning the wait identities to A. The cached A result must remain stale across this ABA transition.
        events.Publish(Request(EventKind.ToolEnd, "B", resolvedAt.AddMilliseconds(4)));
        var onlyA = store.Drain().Sessions["codex:one"];
        Assert.Equal("A", Assert.Single(onlyA.Waits).OperationId);
        pump.Apply([onlyA], resolvedAt.AddMilliseconds(5));
        Assert.Null(onlyA.NativePermissionPending);
        Assert.False(onlyA.RequiresAttention(resolvedAt.AddMinutes(1)));

        // A fresh query for the same A identity has a new observation ID and generation, so it can confirm after stale consumption.
        await pump.RefreshAsync([onlyA]);
        Assert.Single(Directory.GetFiles(workspace.Paths.Inbox));
        var confirmed = store.Drain().Sessions["codex:one"];
        Assert.Equal(HumanConfirmation.Confirmed, Assert.Single(confirmed.Waits).Confirmation);
        Assert.False(confirmed.RequiresAttention(DateTimeOffset.UtcNow.AddSeconds(2)));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DelayedStatusForAmbiguousPermissionCannotConfirmAfterOneCandidateResumes(bool causal)
    {
        using var workspace = new TestWorkspace();
        var prepared = Prepare(workspace);
        var events = new EventStore(workspace.Paths);
        Observation Request(EventKind kind, string? operation, DateTimeOffset at) => TestWorkspace.Event(kind, at, operation) with
        {
            Provider = "codex",
            SessionId = "one",
            AttachmentId = prepared.AttachmentId,
            Directory = workspace.Root,
            OperationFingerprint = kind == EventKind.Permission ? "shared-fingerprint" : "shared-fingerprint",
            IngressAt = causal ? at : null
        };
        var start = DateTimeOffset.UtcNow.AddSeconds(-1);
        events.Publish(Request(EventKind.ToolStart, "A", start));
        events.Publish(Request(EventKind.ToolStart, "B", start.AddMilliseconds(1)));
        events.Publish(Request(EventKind.Permission, null, start.AddMilliseconds(2)));
        var store = new StateStore(workspace.Paths);
        var first = store.Drain().Sessions["codex:one"];
        var ambiguous = Assert.Single(first.Waits);
        Assert.Equal(["A", "B"], ambiguous.Candidates!);

        var delayed = new DelayedStatus();
        var ready = new Status { Pending = true };
        var clients = new Queue<ICodexAttentionClient>([delayed, new Client(ready)]);
        using var pump = new CodexPermissionPump(workspace.Paths, () => clients.Dequeue());
        var oldRead = pump.RefreshAsync([first]);
        await delayed.Started.Task.WaitAsync(TimeSpan.FromSeconds(2));

        events.Publish(Request(EventKind.ToolStart, "A", DateTimeOffset.UtcNow));
        var narrowed = store.Drain().Sessions["codex:one"];
        var remaining = Assert.Single(narrowed.Waits);
        Assert.Equal(["B"], remaining.Candidates!);
        Assert.True(narrowed.ApprovalRevision > first.ApprovalRevision);
        pump.Apply([narrowed], DateTimeOffset.UtcNow);
        Assert.Null(narrowed.NativePermissionPending);

        delayed.Result.SetResult(true);
        await oldRead;
        pump.Apply([narrowed], DateTimeOffset.UtcNow);
        Assert.Null(narrowed.NativePermissionPending);
        Assert.Single(Directory.GetFiles(workspace.Paths.Inbox));

        var afterStale = store.Drain().Sessions["codex:one"];
        Assert.Equal(HumanConfirmation.Provisional, Assert.Single(afterStale.Waits).Confirmation);
        Assert.False(afterStale.RequiresAttention(DateTimeOffset.UtcNow.AddMinutes(1)));

        // The narrowed set has its own scope and query ID; stale evidence cannot poison this valid response.
        await pump.RefreshAsync([afterStale]);
        var confirmed = store.Drain().Sessions["codex:one"];
        Assert.Equal(HumanConfirmation.Confirmed, Assert.Single(confirmed.Waits).Confirmation);
    }
}
