using BellTower.Core;
using BellTower.IO;
using BellTower.Providers;
using BellTower.Sessions;
using BellTower.Tests.Testing;
using Xunit;

namespace BellTower.Tests.Sessions;

// Change-driven Codex native-name synchronization (the shipped write path), its ownership safeguards, and the rule that
// Claude has no native write in this release: the switches refuse it and no Claude session is ever visited.
// Fake provider clients only: no socket or real session is used.
public sealed class NativeNameTests : IDisposable
{
    private sealed class Clock
    {
        public DateTimeOffset Now { get; set; } = new(2026, 10, 24, 12, 0, 0, TimeSpan.Zero);
        public void Advance(TimeSpan span) => Now += span;
    }

    private sealed class FakeCodex : ICodexNameClient
    {
        public Dictionary<string, string?> Names { get; } = [];
        public int Reads { get; private set; }
        public int Writes { get; private set; }
        public bool Fail { get; set; }
        public Action<string, string>? AfterWrite { get; set; }
        public Task ConnectAsync(string endpoint, string token, CancellationToken cancellation) =>
            Fail ? throw new IOException("test failure") : Task.CompletedTask;
        public Task<string?> ReadNameAsync(string thread, CancellationToken cancellation)
        {
            Reads++;
            return Task.FromResult(Names.GetValueOrDefault(thread));
        }
        public Task SetNameAsync(string thread, string name, CancellationToken cancellation)
        {
            Writes++;
            Names[thread] = name;
            AfterWrite?.Invoke(thread, name);
            return Task.CompletedTask;
        }
        public void Dispose() { }
    }

    private readonly TestWorkspace workspace = new();
    private readonly Clock clock = new();
    private readonly FakeCodex codexClient = new();

    public void Dispose() => workspace.Dispose();

    private IdentityStore Identities => new(workspace.Paths);

    private SessionState ClaudeSession(string id = "one", string name = "First")
    {
        var session = IdentityFixtures.Session("claude", id, workspace.Root);
        Identities.Update(session.Key, workspace.Root, value => { value.ManualName = name; return true; });
        return session;
    }

    private SessionState CodexSession(string id = "one", string name = "First")
    {
        var process = ProcessProbe.Identify(Environment.ProcessId)!;
        var attachment = "attachment-codex-" + id;
        var runtimes = new CodexRuntimeStore(workspace.Paths);
        Directory.CreateDirectory(Path.GetDirectoryName(runtimes.TokenPath(attachment))!);
        AtomicFile.Write(runtimes.RecordPath(attachment), Json.Encode(new CodexRuntime(attachment, "ws://127.0.0.1:12345", process, process)));
        File.WriteAllText(runtimes.TokenPath(attachment), "test-token");
        new AttachmentStore(workspace.Paths).Save(new(attachment, "codex", process, new("unknown", process), workspace.Root, DateTimeOffset.UtcNow));
        var session = IdentityFixtures.Session("codex", id, workspace.Root);
        session.AttachmentId = attachment;
        Identities.Update(session.Key, workspace.Root, value => { value.ManualName = name; return true; });
        return session;
    }

    private (CodexNameService Service, NativeNameSchedule Schedule) Codex()
    {
        var schedule = new NativeNameSchedule(CodexNameService.VerifyEvery, () => clock.Now);
        return (new CodexNameService(workspace.Paths, () => codexClient, schedule), schedule);
    }

    [Fact]
    public void CodexUsesTheChangeDrivenScheduleAndBackoff()
    {
        var session = CodexSession();
        var (service, _) = Codex();
        for (var tick = 0; tick < 25; tick++)
        { service.Sync(session, new GitContext()); }
        Assert.Equal(1, codexClient.Reads);
        Assert.Equal(1, codexClient.Writes);
        clock.Advance(CodexNameService.VerifyEvery + TimeSpan.FromSeconds(1));
        service.Sync(session, new GitContext());
        service.Sync(session, new GitContext());
        Assert.Equal(2, codexClient.Reads);
        Assert.Equal(1, codexClient.Writes);
        Identities.Update(session.Key, workspace.Root, value => { value.ManualName = "Second"; return true; });
        service.Sync(session, new GitContext());
        Assert.Equal(2, codexClient.Writes);
    }

    [Fact]
    public void CodexFailuresBackOffAndRecoverWithoutLosingTheName()
    {
        var codex = CodexSession();
        var (service, _) = Codex();
        codexClient.Fail = true;
        for (var tick = 0; tick < 10; tick++)
        { service.Sync(codex, new GitContext()); }
        Assert.StartsWith("unavailable", Identities.Get(codex.Key, workspace.Root).NativeSyncStatus);
        clock.Advance(TimeSpan.FromSeconds(7));
        codexClient.Fail = false;
        service.Sync(codex, new GitContext());
        Assert.Equal("synchronized", Identities.Get(codex.Key, workspace.Root).NativeSyncStatus);
        Assert.Equal("First", codexClient.Names[codex.SessionId]);
    }

    [Fact]
    public void CodexRecordsTheIntendedNameBeforeTheWriteAndConfirmsAfterIt()
    {
        var session = CodexSession();
        string? pendingDuringWrite = null;
        codexClient.AfterWrite = (_, _) => pendingDuringWrite = Identities.Get(session.Key, workspace.Root).NativePendingName;
        var (service, _) = Codex();
        service.Sync(session, new GitContext());
        Assert.Equal("First", pendingDuringWrite);
        var identity = Identities.Get(session.Key, workspace.Root);
        Assert.Equal("First", identity.NativeLastName);
        Assert.Null(identity.NativePendingName);
        Assert.True(identity.NativeSyncEnabled);
    }

    [Fact]
    public void ACodexWriteThatLandedBeforeAFailureIsRecognizedAsOurOwnOnTheNextAttempt()
    {
        var codex = CodexSession();
        codexClient.AfterWrite = (_, _) => throw new IOException("died after the write");
        var (service, _) = Codex();
        service.Sync(codex, new GitContext());
        Assert.Equal("First", Identities.Get(codex.Key, workspace.Root).NativePendingName);
        codexClient.AfterWrite = null;
        clock.Advance(TimeSpan.FromSeconds(3));
        service.Sync(codex, new GitContext());
        var identity = Identities.Get(codex.Key, workspace.Root);
        Assert.True(identity.NativeSyncEnabled, "our own landed name is not a user rename");
        Assert.Equal("synchronized", identity.NativeSyncStatus);
        Assert.Equal("First", identity.NativeLastName);
        Assert.Null(identity.NativePendingName);
        Assert.Equal(1, codexClient.Writes);
    }

    [Fact]
    public void ACodexNameSetOutsideBellTowerPausesWritesAndIsNeverOverwrittenNorPromotedToAUserTitle()
    {
        var codex = CodexSession();
        var (service, _) = Codex();
        service.Sync(codex, new GitContext());
        codexClient.Names[codex.SessionId] = "Generated or typed outside";
        clock.Advance(TimeSpan.FromMinutes(1));
        service.Sync(codex, new GitContext());
        var identity = Identities.Get(codex.Key, workspace.Root);
        Assert.False(identity.NativeSyncEnabled);
        Assert.StartsWith("paused", identity.NativeSyncStatus);
        // Shipped pause-only policy: whether the provider name was typed by the user is unverified, so it never
        // becomes NativeTitle or wins precedence.
        Assert.Null(identity.NativeTitle);
        Assert.Equal("First", identity.Name);
        Assert.Equal(1, codexClient.Writes);
        Assert.Equal("Generated or typed outside", codexClient.Names[codex.SessionId]);
    }

    [Fact]
    public void UseBellTowerNameTakesOwnershipOfACodexNameAndWritesTheBellTowerNameOnce()
    {
        var codex = CodexSession();
        var (service, _) = Codex();
        codexClient.Names[codex.SessionId] = "Typed outside";
        service.Sync(codex, new GitContext());
        Assert.False(Identities.Get(codex.Key, workspace.Root).NativeSyncEnabled);
        NativeNameControl.UseBellTowerName(workspace.Paths, codex);
        service.Sync(codex, new GitContext());
        var identity = Identities.Get(codex.Key, workspace.Root);
        Assert.True(identity.NativeSyncEnabled);
        Assert.Equal("synchronized", identity.NativeSyncStatus);
        Assert.Equal("First", codexClient.Names[codex.SessionId]);
    }

    [Fact]
    public void ClaudeHasNoNativeWriteSoTheSyncSwitchesRefuseItAndTheStatusIsHonest()
    {
        var claude = ClaudeSession();
        Assert.Throws<NotSupportedException>(() => NativeNameControl.SetEnabled(workspace.Paths, claude, true));
        Assert.Throws<NotSupportedException>(() => NativeNameControl.SetEnabled(workspace.Paths, claude, false));
        var identity = Identities.Get(claude.Key, workspace.Root);
        Assert.True(identity.NativeSyncEnabled);
        Assert.Contains("sync-native-session-titles", NativeNamePolicy.StatusFor("claude", identity));
        Assert.Contains("not a provider restriction", NativeNamePolicy.StatusFor("claude", identity));
        Assert.Equal(identity.NativeSyncStatus, NativeNamePolicy.StatusFor("codex", identity));
    }

    [Fact]
    public async Task ThePumpSynchronizesCodexAndNeverVisitsAClaudeSession()
    {
        var claude = ClaudeSession("c1");
        var codex = CodexSession("x1");
        var (codexService, codexSchedule) = Codex();
        using var pump = new NativeNamePump(workspace.Paths, new CheckoutCache(_ => new GitContext()), codexService, codexSchedule);
        for (var tick = 0; tick < 10; tick++)
        { await pump.RunAsync([claude, codex]); }
        Assert.Equal(1, codexClient.Reads);
        Assert.Equal(1, codexClient.Writes);
        Assert.Equal("First", codexClient.Names[codex.SessionId]);
        var claudeIdentity = Identities.Get(claude.Key, workspace.Root);
        Assert.Equal("unavailable", claudeIdentity.NativeSyncStatus);
        Assert.Null(claudeIdentity.NativeLastName);
        Assert.Null(claudeIdentity.NativePendingName);
        Assert.Equal("First", claudeIdentity.Name);
    }

    [Fact]
    public void OursRecognizesBothTheLastWrittenAndThePendingName()
    {
        var identity = new SessionIdentity { Key = "codex:one", Palette = Palette.Create(), NativeLastName = "Old", NativePendingName = "New" };
        Assert.True(NativeNamePolicy.Ours(identity, "Old"));
        Assert.True(NativeNamePolicy.Ours(identity, "New"));
        Assert.False(NativeNamePolicy.Ours(identity, "User"));
        Assert.False(NativeNamePolicy.Ours(identity, null));
        Assert.False(NativeNamePolicy.Conflict(identity, "New"));
        Assert.True(NativeNamePolicy.Conflict(identity, "User"));
    }

    [Fact]
    public void ScheduleBackoffFollowsTheSharedTable()
    {
        var clock = new Clock();
        var schedule = new NativeNameSchedule(TimeSpan.FromMinutes(5), () => clock.Now);
        Assert.True(schedule.Due("k", "a"));
        foreach (var seconds in NativeNameSchedule.BackoffSeconds.Concat([60]))
        {
            schedule.Failed("k", "a");
            Assert.False(schedule.Due("k", "a"));
            clock.Advance(TimeSpan.FromSeconds(seconds - 1));
            Assert.False(schedule.Due("k", "a"));
            clock.Advance(TimeSpan.FromSeconds(1));
            Assert.True(schedule.Due("k", "a"));
        }
        Assert.True(schedule.Due("k", "changed"));
        schedule.Succeeded("k", "a");
        Assert.Equal(0, schedule.Failures("k"));
        Assert.False(schedule.Due("k", "a"));
        Assert.True(schedule.Due("k", "a", force: true));
    }
}
