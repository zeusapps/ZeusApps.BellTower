using BellTower.Core;
using BellTower.IO;
using BellTower.Providers;
using BellTower.Sessions;
using BellTower.Tests.Providers;
using BellTower.Tests.Testing;
using Xunit;

namespace BellTower.Tests.Sessions;

/// <summary>
/// The name service and the permission pump over the one runtime opener, against stand-in backends: a shared backend of the
/// resolved home (Unix-domain socket) and an owned authenticated loopback runtime, alone and at the same time. Codex only: Claude
/// has no shared backend and no native-title transport in this change; its name behavior is covered by the existing
/// native-name tests, which this file does not touch.
/// </summary>
public sealed class CodexSharedRuntimeServiceTests : IDisposable
{
    private static readonly CodexTransportBudget Quick = new(TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(2));
    private readonly TestWorkspace workspace = new();
    private readonly List<string> roots = [];
    private readonly List<CodexStandInServer> servers = [];

    public void Dispose()
    {
        foreach (var server in servers)
        {
            server.Dispose();
        }
        workspace.Dispose();
        foreach (var root in roots)
        {
            try
            { Directory.Delete(root, true); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }

    private string Home()
    {
        var root = Path.Combine(Path.GetTempPath(), "bt" + Guid.NewGuid().ToString("N")[..6]);
        roots.Add(root);
        Directory.CreateDirectory(root);
        return root;
    }

    private CodexStandInServer Backend(string home)
    {
        var server = new CodexStandInServer(CodexSharedEndpoint.SocketPath(home), home);
        servers.Add(server);
        return server;
    }

    private CodexRuntimeOpener Opener(string home, CodexBackendCheck? check = null) =>
        new(workspace.Paths, new CodexSharedAccess(null, Quick, check ?? CodexBackendCheck.Accepting()), () => home);

    private SessionState SharedSession(string id, string name, string mode = AgentModes.SharedBackend)
    {
        var captured = new Attachment("captured", "codex", null, new("unknown"), workspace.Root, DateTimeOffset.UtcNow);
        var attachment = NativeAdoption.Shared(captured, id, mode);
        new AttachmentStore(workspace.Paths).Save(attachment);
        return Named(id, attachment.Id, name);
    }

    private SessionState OwnedSession(string id, string name, int port)
    {
        var process = ProcessProbe.Identify(Environment.ProcessId)!;
        var attachment = "owned-" + id;
        var runtimes = new CodexRuntimeStore(workspace.Paths);
        Directory.CreateDirectory(Path.GetDirectoryName(runtimes.TokenPath(attachment))!);
        AtomicFile.Write(runtimes.RecordPath(attachment), Json.Encode(new CodexRuntime(attachment, "ws://127.0.0.1:" + port, process, process)));
        File.WriteAllText(runtimes.TokenPath(attachment), "owned-token");
        new AttachmentStore(workspace.Paths).Save(new(attachment, "codex", process, new("unknown", process), workspace.Root, DateTimeOffset.UtcNow));
        return Named(id, attachment, name);
    }

    private SessionState Named(string id, string attachment, string name)
    {
        var session = IdentityFixtures.Session("codex", id, workspace.Root);
        session.AttachmentId = attachment;
        new IdentityStore(workspace.Paths).Update(session.Key, workspace.Root, value => { value.ManualName = name; return true; });
        return session;
    }

    private SessionIdentity Identity(SessionState session) => new IdentityStore(workspace.Paths).Get(session.Key, workspace.Root);

    private static string Thread(string id, string? name = null, string extra = "") =>
        "{\"id\":\"" + id + "\",\"sessionId\":\"" + id + "\"" + (name is null ? "" : ",\"name\":\"" + name + "\"") + extra + "}";

    private static void NeverResumesOrStartsTurns(CodexStandInServer server) =>
        Assert.DoesNotContain(server.RecordedMethods(), method => method is "thread/resume" or "thread/start" || method.StartsWith("turn/", StringComparison.Ordinal));

    // Task 3.3: closing Bell Tower's connection (tray exit, detach, update restart) is observer disposal only. Neither a shared
    // backend nor an owned runtime is shut down, reconfigured or loses its listener, and both can be reached again.
    [Fact]
    public async Task DisposingAnObserverConnectionLeavesBothBackendsServingTheNextOne()
    {
        var home = Home();
        var shared = Backend(home);
        var owned = new CodexStandInServer(null, "owned");
        servers.Add(owned);
        var opener = Opener(home);
        var sharedSession = SharedSession("a", "Alpha");
        var ownedSession = OwnedSession("o", "Omega", owned.Port);
        for (var round = 0; round < 2; round++)
        {
            foreach (var session in new[] { sharedSession, ownedSession })
            {
                var access = opener.Select(session)!;
                using (await opener.ConnectAsync(access, CancellationToken.None))
                {
                }
            }
        }
        Assert.Equal(2, shared.RecordedMethods().Count(method => method == "initialize"));
        Assert.Equal(2, owned.RecordedMethods().Count(method => method == "initialize"));
        Assert.DoesNotContain(shared.RecordedMethods().Concat(owned.RecordedMethods()), method => method is "thread/resume" or "thread/start" or "thread/archive" || method.StartsWith("turn/", StringComparison.Ordinal));
    }

    [Fact]
    public void SharedConversationNameIsWrittenThroughTheBackendToThatThreadOnly()
    {
        var home = Home();
        var server = Backend(home);
        server.Threads["a"] = Thread("a");
        server.Threads["b"] = Thread("b", "Existing title of another conversation");
        var session = SharedSession("a", "Alpha");
        new CodexNameService(workspace.Paths, opener: Opener(home)).Sync(session, new GitContext());
        Assert.Contains("Alpha", server.Names["a"], StringComparison.Ordinal);
        Assert.False(server.Names.ContainsKey("b"));
        Assert.Equal("synchronized", Identity(session).NativeSyncStatus);
        Assert.Equal(["initialize", "initialized", "thread/read", "thread/name/set"], server.RecordedMethods());
        NeverResumesOrStartsTurns(server);
    }

    [Fact]
    public void NativeUserTitleOnASharedConversationPausesBellTowerWrites()
    {
        var home = Home();
        var server = Backend(home);
        server.Threads["a"] = Thread("a", "My own title");
        var session = SharedSession("a", "Alpha");
        var service = new CodexNameService(workspace.Paths, opener: Opener(home));
        service.Sync(session, new GitContext());
        service.Sync(session, new GitContext());
        Assert.DoesNotContain("thread/name/set", server.RecordedMethods());
        var identity = Identity(session);
        Assert.False(identity.NativeSyncEnabled);
        Assert.StartsWith("paused", identity.NativeSyncStatus, StringComparison.Ordinal);
    }

    [Fact]
    public void ExplicitDisableAndEnablePersistAcrossServiceInstances()
    {
        var home = Home();
        var server = Backend(home);
        server.Threads["a"] = Thread("a");
        var session = SharedSession("a", "Alpha");
        NativeNameControl.SetEnabled(workspace.Paths, session, false);
        new CodexNameService(workspace.Paths, opener: Opener(home)).Sync(session, new GitContext());
        Assert.Equal(0, server.Accepted);
        Assert.Equal("disabled", Identity(session).NativeSyncStatus);

        // A restarted tray (new service, new opener) still honors the saved control.
        new CodexNameService(workspace.Paths, opener: Opener(home)).Sync(session, new GitContext());
        Assert.Equal(0, server.Accepted);

        NativeNameControl.SetEnabled(workspace.Paths, session, true);
        new CodexNameService(workspace.Paths, opener: Opener(home)).Sync(session, new GitContext());
        Assert.Contains("Alpha", server.Names["a"], StringComparison.Ordinal);
    }

    [Fact]
    public async Task TwoConversationsOnOneBackendNeverReadOrWriteEachOther()
    {
        var home = Home();
        var server = Backend(home);
        server.Threads["a"] = Thread("a");
        server.Threads["b"] = Thread("b");
        var first = SharedSession("a", "Alpha");
        var second = SharedSession("b", "Beta");
        var opener = Opener(home);
        var service = new CodexNameService(workspace.Paths, opener: opener);
        await Task.WhenAll(Task.Run(() => service.Sync(first, new GitContext())), Task.Run(() => service.Sync(second, new GitContext())));
        Assert.Contains("Alpha", server.Names["a"], StringComparison.Ordinal);
        Assert.Contains("Beta", server.Names["b"], StringComparison.Ordinal);
        Assert.DoesNotContain("Beta", server.Names["a"], StringComparison.Ordinal);
        Assert.DoesNotContain("Alpha", server.Names["b"], StringComparison.Ordinal);
        Assert.Equal("synchronized", Identity(first).NativeSyncStatus);
        Assert.Equal("synchronized", Identity(second).NativeSyncStatus);
        // Each request names only its own conversation.
        var requests = server.RecordedRequests().Where(request => request.Contains("threadId", StringComparison.Ordinal)).ToArray();
        Assert.Equal(2, requests.Count(request => request.Contains("\"threadId\":\"a\"", StringComparison.Ordinal)));
        Assert.Equal(2, requests.Count(request => request.Contains("\"threadId\":\"b\"", StringComparison.Ordinal)));
        Assert.DoesNotContain(requests, request => request.Contains("\"a\"", StringComparison.Ordinal) && request.Contains("\"b\"", StringComparison.Ordinal));
        NeverResumesOrStartsTurns(server);
    }

    [Fact]
    public async Task OwnedRuntimeAndSharedBackendWorkAtTheSameTimeWithoutCrossingState()
    {
        var home = Home();
        var shared = Backend(home);
        shared.Threads["s"] = Thread("s");
        using var owned = new CodexStandInServer(null, "ignored");
        owned.Threads["o"] = Thread("o");
        var sharedSession = SharedSession("s", "Shared one");
        var ownedSession = OwnedSession("o", "Owned one", owned.Port);
        var service = new CodexNameService(workspace.Paths, opener: Opener(home));
        await Task.WhenAll(Task.Run(() => service.Sync(sharedSession, new GitContext())), Task.Run(() => service.Sync(ownedSession, new GitContext())));
        Assert.Contains("Shared one", shared.Names["s"], StringComparison.Ordinal);
        Assert.Contains("Owned one", owned.Names["o"], StringComparison.Ordinal);
        Assert.Single(shared.Names);
        Assert.Single(owned.Names);
        Assert.Equal("Bearer owned-token", owned.Authorization);
        Assert.All(shared.RecordedRequests(), request => Assert.DoesNotContain("\"o\"", request, StringComparison.Ordinal));
        Assert.All(owned.RecordedRequests(), request => Assert.DoesNotContain("\"s\"", request, StringComparison.Ordinal));
        NeverResumesOrStartsTurns(shared);
        NeverResumesOrStartsTurns(owned);
    }

    [Fact]
    public void ElevatedSharedBackendIsRefusedWithItsOwnStatusAndNothingIsSent()
    {
        var home = Home();
        var server = Backend(home);
        server.Threads["a"] = Thread("a");
        var check = new CodexBackendCheck(null, _ => ProcessLiveness.Live, _ => true, _ => "codex app-server --listen unix://", _ => true);
        var session = SharedSession("a", "Alpha");
        new CodexNameService(workspace.Paths, opener: Opener(home, check)).Sync(session, new GitContext());
        Assert.Equal("unavailable: shared backend runs elevated", Identity(session).NativeSyncStatus);
        Assert.Empty(server.RecordedMethods());
    }

    [Fact]
    public void ForkedOrDelegatedThreadIsRefusedByPolicyWithAnUnverifiedStatusAndNothingIsWritten()
    {
        var home = Home();
        var server = Backend(home);
        server.Threads["a"] = Thread("a", extra: ",\"forkedFromId\":\"parent\"");
        var session = SharedSession("a", "Alpha");
        new CodexNameService(workspace.Paths, opener: Opener(home)).Sync(session, new GitContext());
        Assert.Equal("unavailable: fork or delegated thread mapping unverified", Identity(session).NativeSyncStatus);
        Assert.DoesNotContain("thread/name/set", server.RecordedMethods());
    }

    [Fact]
    public void ADifferentThreadIdIsAMismatchWithItsOwnStatusAndNothingIsWritten()
    {
        var home = Home();
        var server = Backend(home);
        server.Threads["a"] = Thread("other");
        var session = SharedSession("a", "Alpha");
        new CodexNameService(workspace.Paths, opener: Opener(home)).Sync(session, new GitContext());
        Assert.Equal("unavailable: runtime thread does not match the conversation", Identity(session).NativeSyncStatus);
        Assert.DoesNotContain("thread/name/set", server.RecordedMethods());
    }

    [Fact]
    public void MissingBackendReportsItsReasonAndDoesNotRetryDuringBackoff()
    {
        var home = Home();
        var session = SharedSession("a", "Alpha");
        var opener = Opener(home);
        var service = new CodexNameService(workspace.Paths, opener: opener);
        service.Sync(session, new GitContext());
        Assert.Equal("unavailable: shared backend is not running", Identity(session).NativeSyncStatus);
        // The second attempt is inside the shared backoff: no new attempt, and the status is not replaced by a vaguer one.
        service.Sync(session, new GitContext());
        Assert.Equal("unavailable: shared backend is not running", Identity(session).NativeSyncStatus);
    }

    [Fact]
    public void SelectionRuleIsOneForBothServicesAndFailsClosed()
    {
        var home = Home();
        var opener = Opener(home);
        Assert.IsType<CodexAccess.Shared>(opener.Select(SharedSession("a", "A")));
        Assert.Null(opener.Select(SharedSession("b", "B", AgentModes.Unknown)));
        var claude = IdentityFixtures.Session("claude", "c", workspace.Root);
        Assert.Null(opener.Select(claude));
        var owned = OwnedSession("o", "O", 12345);
        Assert.IsType<CodexAccess.Owned>(opener.Select(owned));
        // An ended attachment has no access, owned or shared.
        var store = new AttachmentStore(workspace.Paths);
        store.Save(store.Get(owned.AttachmentId)! with { EndedAt = DateTimeOffset.UtcNow });
        Assert.Null(opener.Select(owned));
    }

    private SessionState Waiting(string id, string attachment)
    {
        var state = new AppState();
        SessionReducer.Apply(state, TestWorkspace.Event(EventKind.Permission, DateTimeOffset.UtcNow.AddMinutes(-1), "tool") with
        {
            Provider = "codex",
            SessionId = id,
            AttachmentId = attachment,
            Directory = workspace.Root
        });
        return state.Sessions["codex:" + id];
    }

    [Fact]
    public async Task PermissionStatusComesFromTheExactConversationOnASharedBackend()
    {
        var home = Home();
        var server = Backend(home);
        server.Threads["a"] = Thread("a", extra: ",\"status\":{\"type\":\"active\",\"activeFlags\":[\"waitingOnApproval\"]}");
        server.Threads["b"] = Thread("b", extra: ",\"status\":{\"type\":\"idle\"}");
        var first = Waiting("a", SharedSession("a", "A").AttachmentId);
        var second = Waiting("b", SharedSession("b", "B").AttachmentId);
        using var pump = new CodexPermissionPump(workspace.Paths, opener: Opener(home));
        await pump.RefreshAsync([first, second]);
        pump.Apply([first, second], DateTimeOffset.UtcNow);
        Assert.True(first.NativePermissionPending);
        Assert.False(second.NativePermissionPending);
        Assert.False(first.ConfirmationSignalUnavailable);
        NeverResumesOrStartsTurns(server);
        Assert.All(server.RecordedRequests().Where(request => request.Contains("thread/read", StringComparison.Ordinal)),
            request => Assert.Contains("\"includeTurns\":false", request, StringComparison.Ordinal));
    }

    [Fact]
    public async Task PermissionPumpTreatsAnUnverifiedSharedBackendAsNoSupportedSignal()
    {
        var home = Home();
        var server = Backend(home);
        server.Threads["a"] = Thread("a", extra: ",\"status\":{\"type\":\"active\",\"activeFlags\":[\"waitingOnApproval\"]}");
        var session = Waiting("a", SharedSession("a", "A").AttachmentId);
        var elevated = new CodexBackendCheck(null, _ => ProcessLiveness.Live, _ => true, _ => "codex app-server --listen unix://", _ => true);
        using var pump = new CodexPermissionPump(workspace.Paths, opener: Opener(home, elevated));
        await pump.RefreshAsync([session]);
        pump.Apply([session], DateTimeOffset.UtcNow);
        Assert.Null(session.NativePermissionPending);
        Assert.True(session.ConfirmationSignalUnavailable);
        Assert.Empty(server.RecordedMethods());
    }

    // Production budgets on purpose: the shared open must end on its own budget, before the services' 3 s caller timeout.
    private CodexStandInServer SilentBackend(string home)
    {
        var server = new CodexStandInServer(CodexSharedEndpoint.SocketPath(home), home, CodexStandInServer.Behavior.Silent);
        servers.Add(server);
        return server;
    }

    [Fact]
    public void AStalledSharedBackendIsRecordedOnceAsATimeoutAndBacksOffTheNameService()
    {
        var home = Home();
        var server = SilentBackend(home);
        var access = new CodexSharedAccess(null, null, CodexBackendCheck.Accepting());
        var session = SharedSession("a", "Alpha");
        var service = new CodexNameService(workspace.Paths, opener: new(workspace.Paths, access, () => home));
        service.Sync(session, new GitContext());
        Assert.Equal("unavailable: shared backend did not answer in time", Identity(session).NativeSyncStatus);
        Assert.Equal(1, access.Failures(home));
        Assert.Equal(1, server.Accepted);
        service.Sync(session, new GitContext());
        Assert.Equal(1, server.Accepted);
        Assert.Equal("unavailable: shared backend did not answer in time", Identity(session).NativeSyncStatus);
    }

    [Fact]
    public async Task AStalledSharedBackendBacksOffThePermissionPumpForTheWholeHome()
    {
        var home = Home();
        var server = SilentBackend(home);
        var access = new CodexSharedAccess(null, null, CodexBackendCheck.Accepting());
        var first = Waiting("a", SharedSession("a", "A").AttachmentId);
        var second = Waiting("b", SharedSession("b", "B").AttachmentId);
        using var pump = new CodexPermissionPump(workspace.Paths, opener: new(workspace.Paths, access, () => home));
        await pump.RefreshAsync([first]);
        Assert.Equal(1, access.Failures(home));
        Assert.Equal(1, server.Accepted);
        // A second poll inside the window, and another conversation of the same home, make no new connection.
        await pump.RefreshAsync([first, second]);
        Assert.Equal(1, server.Accepted);
        Assert.Equal(1, access.Failures(home));
    }
}
