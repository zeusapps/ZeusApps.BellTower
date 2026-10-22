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
        public void Dispose() { }
    }

    private static SessionState Prepare(TestWorkspace workspace, string thread = "one")
    {
        var process = ProcessProbe.Identify(Environment.ProcessId)!;
        var runtime = new CodexRuntime("runtime-" + thread, "ws://127.0.0.1:12345", process, process);
        var runtimes = new CodexRuntimeStore(workspace.Paths);
        Directory.CreateDirectory(Path.GetDirectoryName(runtimes.RecordPath(runtime.Attachment))!);
        AtomicFile.Write(runtimes.RecordPath(runtime.Attachment), BellTower.IO.Json.Encode(runtime));
        File.WriteAllText(runtimes.TokenPath(runtime.Attachment), "fixture-token");
        new AttachmentStore(workspace.Paths).Save(new(runtime.Attachment, "codex", process, new("unknown", process), workspace.Root, DateTimeOffset.UtcNow));
        var state = new AppState();
        var permission = TestWorkspace.Event(EventKind.Permission, DateTimeOffset.UtcNow.AddMinutes(-1), "tool") with
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
    public async Task FailedUnknownOrStaleVerificationUsesTheExplicitFallback()
    {
        using var workspace = new TestWorkspace();
        var session = Prepare(workspace);
        var status = new Status { Pending = false };
        using var pump = new CodexPermissionPump(workspace.Paths, () => new Client(status));
        await pump.RefreshAsync([session]);
        pump.Apply([session], DateTimeOffset.UtcNow.AddSeconds(6));
        Assert.Null(session.NativePermissionPending);
        Assert.Equal(session.Since!.Value.AddSeconds(15), session.Due);
        status.Fail = true;
        await pump.RefreshAsync([session]);
        pump.Apply([session], DateTimeOffset.UtcNow);
        Assert.Null(session.NativePermissionPending);
        status.Fail = false;
        status.Pending = null;
        await pump.RefreshAsync([session]);
        pump.Apply([session], DateTimeOffset.UtcNow);
        Assert.Null(session.NativePermissionPending);
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
    public async Task RetryingUnavailableVerificationDoesNotHideAnOverdueFallback()
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
            Assert.True(session.RequiresAttention(DateTimeOffset.UtcNow));
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
}
