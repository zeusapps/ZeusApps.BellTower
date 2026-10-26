using BellTower.Core;
using BellTower.IO;
using BellTower.Providers;
using BellTower.Sessions;
using BellTower.Tests.Testing;
using Xunit;

namespace BellTower.Tests.Providers;

public sealed class CodexNameTests
{
    private sealed class Names
    {
        public Dictionary<(string Endpoint, string Thread), string?> Values { get; } = [];
        public int Writes { get; set; }
        public bool Fail { get; set; }
    }

    private sealed class Client(Names names) : ICodexNameClient
    {
        private string endpoint = "";
        public Task ConnectAsync(string address, string token, CancellationToken cancellation)
        {
            if (names.Fail)
            {
                throw new IOException("test connection failure");
            }
            endpoint = address;
            return Task.CompletedTask;
        }
        public Task<string?> ReadNameAsync(string thread, CancellationToken cancellation) =>
            Task.FromResult(names.Values.GetValueOrDefault((endpoint, thread)));
        public Task SetNameAsync(string thread, string name, CancellationToken cancellation)
        {
            names.Values[(endpoint, thread)] = name;
            names.Writes++;
            return Task.CompletedTask;
        }
        public void Dispose() { }
    }

    private static SessionState Prepare(TestWorkspace workspace, string thread, string name, int port = 12345, string? attachmentId = null)
    {
        var process = ProcessProbe.Identify(Environment.ProcessId)!;
        var attachment = attachmentId ?? "attachment-" + thread;
        var runtime = new CodexRuntime(attachment, "ws://127.0.0.1:" + port, process, process);
        var runtimes = new CodexRuntimeStore(workspace.Paths);
        Directory.CreateDirectory(Path.GetDirectoryName(runtimes.TokenPath(attachment))!);
        AtomicFile.Write(runtimes.RecordPath(attachment), Json.Encode(runtime));
        File.WriteAllText(runtimes.TokenPath(attachment), "test-token");
        new AttachmentStore(workspace.Paths).Save(new(attachment, "codex", process, new("unknown", process), workspace.Root, DateTimeOffset.UtcNow));
        new IdentityStore(workspace.Paths).Get("codex:" + thread, workspace.Root, name);
        return new() { Key = "codex:" + thread, Provider = "codex", SessionId = thread, AttachmentId = attachment, Directory = workspace.Root };
    }

    [Fact]
    public async Task SharedCheckoutProjectionAvoidsNewGitReadsAndPreservesNativeRenameOwnership()
    {
        using var workspace = new TestWorkspace();
        var session = Prepare(workspace, "one", "Managed");
        var names = new Names();
        var service = new CodexNameService(workspace.Paths, () => new Client(names));
        var reads = 0;
        var now = DateTimeOffset.UtcNow;
        var cache = new CheckoutCache(_ => { reads++; return new("feature", "linked-checkout"); }, () => now);
        for (var tick = 0; tick < 50; tick++)
        {
            var context = await cache.ReadAsync(workspace.Root);
            service.Sync(session, context);
            Assert.Equal(context, await cache.ReadAsync(workspace.Root));
        }
        Assert.Equal(1, reads);
        Assert.Equal(1, names.Writes);
        Assert.Equal("Managed | worktree: linked-checkout", names.Values[("ws://127.0.0.1:12345", "one")]);
        names.Values[("ws://127.0.0.1:12345", "one")] = "User rename";
        service.Sync(session, await cache.ReadAsync(workspace.Root));
        Assert.Equal(1, names.Writes);
        Assert.False(new IdentityStore(workspace.Paths).Get(session.Key, workspace.Root).NativeSyncEnabled);
    }

    [Fact]
    public void NativeNamesFollowIdentityAndDuplicateWritesAreSuppressed()
    {
        using var workspace = new TestWorkspace();
        var session = Prepare(workspace, "one", "First");
        var names = new Names();
        var service = new CodexNameService(workspace.Paths, () => new Client(names));
        service.Sync(session);
        service.Sync(session);
        Assert.Equal(1, names.Writes);
        new IdentityStore(workspace.Paths).Update(session.Key, workspace.Root, value => { value.ManualName = "Second"; return true; });
        service.Sync(session);
        Assert.Equal(2, names.Writes);
        var identity = new IdentityStore(workspace.Paths).Get(session.Key, workspace.Root);
        Assert.Equal("Second", identity.NativeLastName);
        Assert.Equal("synchronized", identity.NativeSyncStatus);
    }

    [Fact]
    public void ResumedThreadRetainsNativeOwnershipAcrossRuntimeRestart()
    {
        using var workspace = new TestWorkspace();
        var session = Prepare(workspace, "one", "Saved title");
        var names = new Names();
        new CodexNameService(workspace.Paths, () => new Client(names)).Sync(session);
        var palette = new IdentityStore(workspace.Paths).Get(session.Key, workspace.Root).Palette;
        new CodexRuntimeStore(workspace.Paths).Stop(session.AttachmentId);
        var resumed = Prepare(workspace, "one", "Stale launch title", 12346, "resumed-attachment-one");
        // The replacement server reads the same durable native thread name.
        names.Values[("ws://127.0.0.1:12346", "one")] = "Saved title";
        var restartedService = new CodexNameService(workspace.Paths, () => new Client(names));
        restartedService.Sync(resumed);
        Assert.Equal(1, names.Writes);
        var restored = new IdentityStore(workspace.Paths).Get(session.Key, workspace.Root);
        Assert.True(restored.NativeSyncEnabled);
        Assert.Equal("synchronized", restored.NativeSyncStatus);
        Assert.Equal(palette, restored.Palette);

        new IdentityStore(workspace.Paths).Update(session.Key, workspace.Root, value =>
        {
            value.ManualName = null;
            value.Change = "next-change";
            return true;
        });
        restartedService.Sync(resumed);
        Assert.Equal("next-change", names.Values[("ws://127.0.0.1:12346", "one")]);
        Assert.Equal(2, names.Writes);
    }

    [Fact]
    public void NativeRenamePausesWritesUntilExplicitResume()
    {
        using var workspace = new TestWorkspace();
        var session = Prepare(workspace, "one", "Managed");
        var names = new Names();
        var service = new CodexNameService(workspace.Paths, () => new Client(names));
        service.Sync(session);
        names.Values[("ws://127.0.0.1:12345", "one")] = "Native manual";
        service.Sync(session);
        service.Sync(session);
        Assert.Equal(1, names.Writes);
        var identity = new IdentityStore(workspace.Paths).Get(session.Key, workspace.Root);
        Assert.False(identity.NativeSyncEnabled);
        Assert.StartsWith("paused:", identity.NativeSyncStatus);
        service.SetEnabled(session, true);
        service.Sync(session);
        Assert.Equal(2, names.Writes);
        Assert.Equal("Managed", names.Values[("ws://127.0.0.1:12345", "one")]);
    }

    [Fact]
    public void PreExistingNativeNameIsPreservedAndDisabledSyncDoesNothing()
    {
        using var workspace = new TestWorkspace();
        var session = Prepare(workspace, "one", "Managed");
        var names = new Names();
        names.Values[("ws://127.0.0.1:12345", "one")] = "Saved name";
        var service = new CodexNameService(workspace.Paths, () => new Client(names));
        service.Sync(session);
        Assert.Equal(0, names.Writes);
        service.SetEnabled(session, false);
        service.Sync(session);
        Assert.Equal("disabled", new IdentityStore(workspace.Paths).Get(session.Key, workspace.Root).NativeSyncStatus);
    }

    [Fact]
    public void ParallelRuntimesAndThreadIdsStayIsolated()
    {
        using var workspace = new TestWorkspace();
        var one = Prepare(workspace, "one", "First", 12345);
        var two = Prepare(workspace, "two", "Second", 12346);
        var names = new Names();
        var service = new CodexNameService(workspace.Paths, () => new Client(names));
        service.Sync(one);
        service.Sync(two);
        Assert.Equal("First", names.Values[("ws://127.0.0.1:12345", "one")]);
        Assert.Equal("Second", names.Values[("ws://127.0.0.1:12346", "two")]);
        Assert.Equal(2, names.Values.Count);
    }

    [Fact]
    public void FailureAndStoppedRuntimeRetainIdentityAndAttention()
    {
        using var workspace = new TestWorkspace();
        var session = Prepare(workspace, "one", "Managed");
        new EventStore(workspace.Paths).Publish(TestWorkspace.ConfirmedPermission() with
        {
            Provider = "codex",
            SessionId = "one",
            AttachmentId = session.AttachmentId,
            Directory = workspace.Root
        });
        var states = new StateStore(workspace.Paths);
        var before = Json.Encode(states.Drain());
        var names = new Names { Fail = true };
        var service = new CodexNameService(workspace.Paths, () => new Client(names));
        service.Sync(session);
        Assert.Equal("unavailable: native metadata connection failed", new IdentityStore(workspace.Paths).Get(session.Key, workspace.Root).NativeSyncStatus);
        new CodexRuntimeStore(workspace.Paths).Stop(session.AttachmentId);
        service.Sync(session);
        Assert.Equal("unavailable: no verified dedicated runtime", new IdentityStore(workspace.Paths).Get(session.Key, workspace.Root).NativeSyncStatus);
        Assert.Equal(before, Json.Encode(states.Read()));
        Assert.Equal(0, names.Writes);
    }

    [Fact]
    public async Task NativeTransportRejectsNonlocalEndpointsBeforeConnecting()
    {
        using var connection = new CodexConnection();
        await Assert.ThrowsAsync<InvalidDataException>(() => connection.ConnectAsync("ws://example.com:12345", "token", CancellationToken.None));
    }

    [Fact]
    public void OrdinaryDetachDoesNotStopTheNativeRuntime()
    {
        using var workspace = new TestWorkspace();
        var session = Prepare(workspace, "one", "Managed");
        new AttachmentLifecycle(workspace.Paths).Detach(session.AttachmentId);
        Assert.False(File.Exists(new CodexRuntimeStore(workspace.Paths).StopPath(session.AttachmentId)));
    }
}
