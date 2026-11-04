using System.Text.Json;
using BellTower.Core;
using BellTower.IO;
using BellTower.Tests.Testing;
using Xunit;

namespace BellTower.Tests.Providers;

// Terminal disconnect versus continuing conversation work (design decision 2, task 2.3). One shared rule; the terminal facts of a
// shared Codex conversation come from the binder adapter (task 4.2) and the work facts from the runtime transport (task 3.x),
// neither of which exists yet, so both are faked here. Direct-process rows run for Claude Code and Codex.
public sealed class ConversationLifecycleTests
{
    private static readonly TimeSpan Grace = TimeSpan.FromMinutes(10);

    // The read-only snapshot a caller gathers outside the locks. Keys here are session ids; the snapshot keys them by session key.
    private sealed class FakeEvidence
    {
        public Dictionary<string, SharedTerminalFacts> Terminals { get; } = [];
        public Dictionary<string, RuntimeWorkFacts> Works { get; } = [];
        public ConversationFacts? For(string id) => Terminals.ContainsKey(id) || Works.ContainsKey(id)
            ? new(Terminals.GetValueOrDefault(id), Works.GetValueOrDefault(id)) : null;
        public ConversationEvidenceSnapshot Snapshot() => new(Terminals.Keys.Concat(Works.Keys).Distinct().ToDictionary(id => "codex:" + id, id => For(id)!));
    }

    private sealed record Evaluated(Attachment Attachment)
    {
        public string Terminal => Attachment.Evidence!.TerminalBinding.Binding;
    }

    private sealed class FakeProcesses
    {
        public HashSet<ProcessIdentity> Ended { get; } = [];
        public ProcessLiveness Inspect(ProcessIdentity identity) => Ended.Contains(identity) ? ProcessLiveness.Ended : ProcessLiveness.Live;
    }

    private static readonly ProcessIdentity Owner = new(4001, 111);
    private static readonly ProcessIdentity OtherOwner = new(4002, 222);

    private static Attachment Shared(string id, DateTimeOffset observed, TerminalEvidence? terminal = null, string provider = "codex") =>
        new(id, provider, null, new(), "D:/example", observed.AddMinutes(-1))
        {
            Ownership = "shared-runtime",
            Health = "unverified",
            UnverifiedSince = observed,
            Evidence = new AttachmentEvidence
            {
                Mode = AgentModes.SharedBackend,
                LifecycleOwner = LifecycleOwners.SharedBackend,
                ConversationHealth = EvidenceStatus.HookObserved,
                ConversationObservedAt = observed,
                Terminal = terminal ?? new()
            }.Stamped(null)
        };

    private static TerminalEvidence Bound(DateTimeOffset at, ProcessIdentity? owner = null) =>
        new() { Binding = EvidenceStatus.Connected, Client = owner ?? Owner, ObservedAt = at, Source = "client-endpoint" };

    private static SharedTerminalFacts Facts(DateTimeOffset at, ProcessIdentity? owner = null, bool? listener = true, bool? backend = true, bool demoted = false,
        string? reported = "connected", ProcessIdentity? shell = null) => SharedTerminalFacts.FromEndpoint(at, owner ?? Owner, shell, listener, backend, demoted, "01a1", reported);

    private static Evaluated Evaluate(Attachment attachment, DateTimeOffset now, FakeEvidence evidence, FakeProcesses processes) =>
        new(new AttachmentHealth(processes.Inspect).Evaluate(attachment, now, Grace, evidence.For("conversation")));

    private static Evaluated Probe(Attachment attachment, DateTimeOffset now, Func<ProcessIdentity, ProcessLiveness> inspect) =>
        new(new AttachmentHealth(inspect).Evaluate(attachment, now, Grace));

    private static void Publish(TestWorkspace workspace, string provider, string attachment, string session, DateTimeOffset at) =>
        new EventStore(workspace.Paths).Publish(TestWorkspace.ConfirmedPermission(at) with
        {
            Provider = provider,
            AttachmentId = attachment,
            SessionId = session,
            Directory = workspace.Root
        });

    // ----- Directly attached processes: the existing exit and idle rules are unchanged (Claude Code and Codex). -----

    [Theory]
    [InlineData("claude")]
    [InlineData("codex")]
    public void ADirectAgentCrashEndsTheSessionThroughTheExistingRule(string provider)
    {
        using var workspace = new TestWorkspace();
        var at = DateTimeOffset.UtcNow.AddSeconds(-30);
        var agent = new ProcessIdentity(5001, 51);
        var processes = new FakeProcesses();
        new AttachmentStore(workspace.Paths).Save(new Attachment("direct", provider, agent, new("unknown"), workspace.Root, at) { Ownership = "observed" });
        Publish(workspace, provider, "direct", "s1", at);
        var store = new StateStore(workspace.Paths, processes.Inspect);
        var live = store.Drain().Sessions[provider + ":s1"];
        Assert.Equal("verified-live", live.AttachmentHealth);
        Assert.True(live.RequiresAttention(DateTimeOffset.UtcNow));
        processes.Ended.Add(agent);
        var ended = store.Drain().Sessions[provider + ":s1"];
        Assert.True(ended.Ended);
        Assert.Equal("confirmed-ended", ended.AttachmentHealth);
        Assert.Empty(ended.Waits);
        // Both ended together: the stored record is ended and the shared terminal facet was never touched.
        Assert.Equal("confirmed-ended", new AttachmentStore(workspace.Paths).Get("direct")!.Health);
    }

    [Theory]
    [InlineData("claude")]
    [InlineData("codex")]
    public void AQuietLiveProcessStaysVerifiedLiveWithoutHooks(string provider)
    {
        using var workspace = new TestWorkspace();
        var at = DateTimeOffset.UtcNow.AddHours(-6);
        var agent = new ProcessIdentity(5002, 52);
        new AttachmentStore(workspace.Paths).Save(new Attachment("quiet", provider, agent, new("unknown"), workspace.Root, at) { Ownership = "observed" });
        Publish(workspace, provider, "quiet", "s1", at);
        var session = new StateStore(workspace.Paths, new FakeProcesses().Inspect).Drain().Sessions[provider + ":s1"];
        Assert.False(session.Ended);
        Assert.Equal("verified-live", session.AttachmentHealth);
        Assert.True(session.RequiresAttention(DateTimeOffset.UtcNow));
    }

    // ----- Shared conversations: terminal side and work side are independent and never end the conversation. -----

    [Fact]
    public void ASharedOwnerKilledWhileIdleDisconnectsTheTerminalAndKeepsTheConversationWithoutCompletion()
    {
        var now = DateTimeOffset.UtcNow;
        var processes = new FakeProcesses();
        var evidence = new FakeEvidence();
        var attachment = Shared("shared", now.AddMinutes(-1), Bound(now.AddMinutes(-1)));
        processes.Ended.Add(Owner);
        evidence.Terminals["conversation"] = Facts(now, reported: "connected");
        var result = Evaluate(attachment, now, evidence, processes);
        Assert.Equal(EvidenceStatus.Disconnected, result.Terminal);
        Assert.Equal("owner-exited", result.Attachment.Evidence!.TerminalBinding.Source);
        Assert.Null(result.Attachment.EndedAt);
        Assert.NotEqual("confirmed-ended", result.Attachment.Health);
        // Retained: even with no evidence at all the conversation becomes unavailable, never ended or complete.
        var later = Evaluate(result.Attachment, now.AddHours(1), evidence, processes);
        Assert.Equal("unavailable", later.Attachment.Health);
        Assert.Null(later.Attachment.EndedAt);
        Assert.Equal(EvidenceStatus.Disconnected, later.Terminal);
    }

    [Fact]
    public void ASharedOwnerKilledWhileActiveDisconnectsTheTerminalButWorkContinues()
    {
        var now = DateTimeOffset.UtcNow;
        var processes = new FakeProcesses();
        var evidence = new FakeEvidence();
        // The last hook is long past; only exact-conversation runtime status shows the turn is still running.
        var attachment = Shared("shared", now.AddHours(-2), Bound(now.AddHours(-2)));
        processes.Ended.Add(Owner);
        evidence.Terminals["conversation"] = Facts(now);
        evidence.Works["conversation"] = new(WorkSignal.Active, now.AddSeconds(-20));
        var result = Evaluate(attachment, now, evidence, processes);
        Assert.Equal(EvidenceStatus.Disconnected, result.Terminal);
        Assert.Equal("unverified", result.Attachment.Health);
        Assert.Null(result.Attachment.EndedAt);
        // Without that runtime evidence the same record is only unavailable.
        evidence.Works.Clear();
        Assert.Equal("unavailable", Evaluate(attachment, now, evidence, processes).Attachment.Health);
    }

    [Theory]
    [InlineData(false, true, "listener-gone")]
    [InlineData(true, false, "backend-disconnected")]
    public void AConnectedStatusWithoutAListenerOrBackendConnectionIsStillDisconnected(bool listener, bool backend, string reason)
    {
        var now = DateTimeOffset.UtcNow;
        var evidence = new FakeEvidence();
        var attachment = Shared("shared", now, Bound(now.AddMinutes(-1)));
        // The owner process is alive and runtimeStatus still says connected: only the listener and backend connection decide.
        evidence.Terminals["conversation"] = Facts(now, listener: listener, backend: backend, reported: "connected");
        var result = Evaluate(attachment, now, evidence, new FakeProcesses());
        Assert.Equal(EvidenceStatus.Disconnected, result.Terminal);
        Assert.Equal(reason, result.Attachment.Evidence!.TerminalBinding.Source);
        // The same facts with a listener and a backend connection are connected, whatever the reported status says.
        evidence.Terminals["conversation"] = Facts(now.AddSeconds(1), reported: "disconnected");
        Assert.Equal(EvidenceStatus.Connected, Evaluate(result.Attachment, now.AddSeconds(1), evidence, new FakeProcesses()).Terminal);
    }

    [Fact]
    public void LatestOnlyDemotionIsNotCompletionAndWorkContinues()
    {
        var now = DateTimeOffset.UtcNow;
        var evidence = new FakeEvidence();
        var attachment = Shared("shared", now, Bound(now.AddMinutes(-1)));
        evidence.Terminals["conversation"] = Facts(now, demoted: true);
        evidence.Works["conversation"] = new(WorkSignal.Active, now);
        var result = Evaluate(attachment, now, evidence, new FakeProcesses());
        Assert.Equal(EvidenceStatus.Demoted, result.Terminal);
        Assert.Equal("latest-only", result.Attachment.Evidence!.TerminalBinding.Source);
        Assert.Null(result.Attachment.EndedAt);
        Assert.Equal("unverified", result.Attachment.Health);
    }

    private sealed class FixedTime(DateTimeOffset now) : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = now;
        public override DateTimeOffset GetUtcNow() => Now;
    }

    [Fact]
    public void ANeverBoundSharedConversationStaysTerminalUnverifiedAndKeepsAttentionWhileWorkContinues()
    {
        using var workspace = new TestWorkspace();
        new ConfigStore(workspace.Paths).Update(config => config.AttachmentEvidenceGraceMinutes = 1);
        var now = DateTimeOffset.UtcNow;
        var at = now.AddMinutes(-5);
        new AttachmentStore(workspace.Paths).Save(Shared("shared", at));
        Publish(workspace, "codex", "shared", "s1", at);
        var evidence = new FakeEvidence();
        evidence.Works["s1"] = new(WorkSignal.Active, now.AddSeconds(-5));
        var time = new FixedTime(now);
        var store = new StateStore(workspace.Paths, new FakeProcesses().Inspect, time);
        var session = store.Drain(evidence.Snapshot()).Sessions["codex:s1"];
        var stored = new AttachmentStore(workspace.Paths).Get("shared")!;
        Assert.Equal("unverified", session.AttachmentHealth);
        Assert.True(session.RequiresAttention(now));
        Assert.Equal(EvidenceStatus.Unverified, stored.Evidence!.TerminalBinding.Binding);
        Assert.NotEqual("confirmed-ended", stored.Health);
        Assert.False(session.Ended);
        // Without exact-conversation evidence the work-confirmed time still holds the grace, then expires.
        time.Now = now.AddSeconds(30);
        Assert.Equal("unverified", store.Drain().Sessions["codex:s1"].AttachmentHealth);
        time.Now = now.AddMinutes(2);
        Assert.Equal("unavailable", store.Drain().Sessions["codex:s1"].AttachmentHealth);
        Assert.Equal(EvidenceStatus.Unverified, new AttachmentStore(workspace.Paths).Get("shared")!.Evidence!.TerminalBinding.Binding);
    }

    // ANALYSIS-1203 / QA-2000: runtime-confirmed work gets the configured grace, like a hook, and the evidence time never moves back.
    [Fact]
    public void RuntimeConfirmedWorkKeepsTheConfiguredGraceWhenFactsPauseAndTheEvidenceTimeNeverMovesBack()
    {
        using var workspace = new TestWorkspace();
        var now = DateTimeOffset.UtcNow;
        var old = now.AddHours(-2);
        new AttachmentStore(workspace.Paths).Save(Shared("shared", old));
        Publish(workspace, "codex", "shared", "s1", old);
        var evidence = new FakeEvidence();
        evidence.Works["s1"] = new(WorkSignal.Active, now.AddSeconds(-10));
        var time = new FixedTime(now);
        var store = new StateStore(workspace.Paths, new FakeProcesses().Inspect, time);
        Assert.Equal("unverified", store.Drain(evidence.Snapshot()).Sessions["codex:s1"].AttachmentHealth);
        var first = new AttachmentStore(workspace.Paths).Get("shared")!.UnverifiedSince!.Value;
        Assert.Equal(now.AddSeconds(-10), first);
        // No facts three minutes later (a transport pause, backoff or restart); the default 10 minute grace still holds.
        time.Now = now.AddMinutes(3);
        var paused = store.Drain().Sessions["codex:s1"];
        Assert.Equal("unverified", paused.AttachmentHealth);
        Assert.True(new AttachmentStore(workspace.Paths).Get("shared")!.UnverifiedSince >= first);
        time.Now = now.AddMinutes(11);
        Assert.Equal("unavailable", store.Drain().Sessions["codex:s1"].AttachmentHealth);
        Assert.True(new AttachmentStore(workspace.Paths).Get("shared")!.UnverifiedSince >= first);
    }

    // ANALYSIS-1206: an exact-conversation hook is independent evidence and revives an unavailable shared conversation.
    [Fact]
    public void AnExactConversationHookRevivesAnUnavailableSharedConversationToUnverifiedNeverVerifiedLive()
    {
        using var workspace = new TestWorkspace();
        var now = DateTimeOffset.UtcNow;
        var old = now.AddHours(-2);
        var attachments = new AttachmentStore(workspace.Paths);
        attachments.Save(Shared("shared", old) with { Health = "unavailable" });
        Publish(workspace, "codex", "shared", "s1", old);
        var store = new StateStore(workspace.Paths, new FakeProcesses().Inspect);
        Assert.Equal("unavailable", store.Drain().Sessions["codex:s1"].AttachmentHealth);
        var hook = Shared("shared", now) with { Health = "unverified", RegisteredAt = now };
        attachments.Adopt(hook, now);
        var revived = store.Drain().Sessions["codex:s1"];
        Assert.Equal("unverified", revived.AttachmentHealth);
        var stored = attachments.Get("shared")!;
        Assert.Equal("unverified", stored.Health);
        Assert.Null(stored.Process);
        Assert.Null(stored.EndedAt);
        Assert.False(revived.Ended);
        // A process-less record can never reach verified-live, so there is no reminder, escalation or host action.
        Assert.NotEqual("verified-live", revived.AttachmentHealth);
        // A direct record with an unknown owner keeps the existing no-revival rule.
        attachments.Save(new Attachment("direct", "claude", null, new("unknown"), workspace.Root, old) { Ownership = "observed", Health = "unavailable", UnverifiedSince = old });
        Publish(workspace, "claude", "direct", "d1", old);
        attachments.Adopt(attachments.Get("direct")!, now);
        Assert.Equal("unavailable", store.Drain().Sessions["claude:d1"].AttachmentHealth);
    }

    // ANALYSIS-1201: an ambiguous probe is unverified, not a false disconnect, and a stored connection ages out.
    [Fact]
    public void AnIncompleteEndpointObservationIsUnverifiedNotDisconnected()
    {
        var now = DateTimeOffset.UtcNow;
        var evidence = new FakeEvidence();
        var attachment = Shared("shared", now, Bound(now.AddMinutes(-1)));
        evidence.Terminals["conversation"] = Facts(now, listener: null, backend: true);
        var result = Evaluate(attachment, now, evidence, new FakeProcesses());
        Assert.Equal(EvidenceStatus.Unverified, result.Terminal);
        Assert.Equal("incomplete-endpoint", result.Attachment.Evidence!.TerminalBinding.Source);
        Assert.Null(result.Attachment.EndedAt);
        // The binder may also state the outcome and reason itself (decision 3 step 5).
        evidence.Terminals["conversation"] = new(now.AddSeconds(1), Owner, null, EvidenceStatus.Unverified, "ambiguous-client");
        var stated = Evaluate(result.Attachment, now.AddSeconds(1), evidence, new FakeProcesses());
        Assert.Equal(EvidenceStatus.Unverified, stated.Terminal);
        Assert.Equal("ambiguous-client", stated.Attachment.Evidence!.TerminalBinding.Source);
    }

    [Fact]
    public void AStoredConnectionAgesToUnverifiedWithoutFactsAndOwnerDeathIsStillDetected()
    {
        var now = DateTimeOffset.UtcNow;
        var processes = new FakeProcesses();
        var none = new FakeEvidence();
        var fresh = Evaluate(Shared("shared", now, Bound(now.AddMinutes(-1))), now, none, processes);
        Assert.Equal(EvidenceStatus.Connected, fresh.Terminal);
        var staleRecord = Shared("shared", now, Bound(now - ConversationLifecycle.TerminalFreshness - TimeSpan.FromSeconds(1)));
        var aged = Evaluate(staleRecord, now, none, processes);
        Assert.Equal(EvidenceStatus.Unverified, aged.Terminal);
        Assert.Equal("stale-connection", aged.Attachment.Evidence!.TerminalBinding.Source);
        Assert.Equal(staleRecord.Evidence!.TerminalBinding.ObservedAt, aged.Attachment.Evidence.TerminalBinding.ObservedAt);
        Assert.Null(aged.Attachment.EndedAt);
        // The owner dying is detected with no facts at all.
        processes.Ended.Add(Owner);
        Assert.Equal("owner-exited", Evaluate(staleRecord, now, none, processes).Attachment.Evidence!.TerminalBinding.Source);
        // A later fact reconnects an aged record.
        processes.Ended.Clear();
        var again = new FakeEvidence();
        again.Terminals["conversation"] = Facts(now);
        Assert.Equal(EvidenceStatus.Connected, Evaluate(aged.Attachment, now, again, processes).Terminal);
    }

    // ANALYSIS-1202: the shell comes from evidence.terminal; Host.Shell of a shared record is never read.
    [Fact]
    public void AnExitedEvidenceShellDisconnectsTheTerminalAndHostShellIsNotRead()
    {
        var now = DateTimeOffset.UtcNow;
        var shell = new ProcessIdentity(4100, 410);
        var processes = new FakeProcesses();
        var none = new FakeEvidence();
        var record = Shared("shared", now, Bound(now) with { Shell = shell });
        Assert.Equal(EvidenceStatus.Connected, Evaluate(record, now, none, processes).Terminal);
        processes.Ended.Add(shell);
        var result = Evaluate(record, now, none, processes);
        Assert.Equal(EvidenceStatus.Disconnected, result.Terminal);
        Assert.Equal("shell-exited", result.Attachment.Evidence!.TerminalBinding.Source);
        Assert.Null(result.Attachment.EndedAt);
        Assert.NotEqual("confirmed-ended", result.Attachment.Health);
        // An ended Host.Shell on a shared record is not the terminal shell; it ends nothing and disconnects nothing.
        var hostOnly = Shared("shared", now, Bound(now)) with { Host = new("unknown") { Shell = shell } };
        var host = Evaluate(hostOnly, now, none, processes);
        Assert.Equal(EvidenceStatus.Connected, host.Terminal);
        Assert.Null(host.Attachment.EndedAt);
        Assert.Equal("unverified", host.Attachment.Health);
    }

    [Fact]
    public void AnOwnerChangeReplacesOrClearsTheRecordedShell()
    {
        var now = DateTimeOffset.UtcNow;
        var oldShell = new ProcessIdentity(4100, 410);
        var newShell = new ProcessIdentity(4200, 420);
        var processes = new FakeProcesses();
        var record = Shared("shared", now, Bound(now.AddMinutes(-1)) with { Shell = oldShell });
        var evidence = new FakeEvidence();
        // The same owner without a shell keeps the recorded one.
        evidence.Terminals["conversation"] = Facts(now);
        Assert.Equal(oldShell, Evaluate(record, now, evidence, processes).Attachment.Evidence!.TerminalBinding.Shell);
        // A different owner without a shell clears it, so the former shell dying cannot disconnect the new client.
        evidence.Terminals["conversation"] = Facts(now, OtherOwner);
        var cleared = Evaluate(record, now, evidence, processes);
        Assert.Null(cleared.Attachment.Evidence!.TerminalBinding.Shell);
        processes.Ended.Add(oldShell);
        Assert.Equal(EvidenceStatus.Connected, Evaluate(cleared.Attachment, now, new FakeEvidence(), processes).Terminal);
        // A different owner with a shell replaces it.
        evidence.Terminals["conversation"] = Facts(now, OtherOwner, shell: newShell);
        var replaced = Evaluate(record, now, evidence, processes);
        Assert.Equal(newShell, replaced.Attachment.Evidence!.TerminalBinding.Shell);
        Assert.Equal(EvidenceStatus.Connected, replaced.Terminal);
    }

    // ANALYSIS-1207 / QA-2001: evidence this build cannot use is never rewritten, by the drain or by a hook merge.
    [Fact]
    public void StoredEvidenceWithANewerRevisionKeepsItsBytesThroughADrainAndAHookMerge()
    {
        using var workspace = new TestWorkspace();
        var now = DateTimeOffset.UtcNow;
        var old = now.AddMinutes(-30);
        var future = Json.Decode<Attachment>($$"""
            {"id":"future","provider":"codex","host":{"kind":"unknown"},"directory":"D:/example","registeredAt":"{{old:O}}","ownership":"shared-runtime",
             "evidence":{"revision":2,"mode":"shared-backend","lifecycleOwner":"shared-backend","futureFacet":{"a":1} }
            }
            """);
        var attachments = new AttachmentStore(workspace.Paths);
        attachments.Save(future);
        Publish(workspace, "codex", "future", "s1", old);
        var before = Json.Encode(attachments.Get("future")!.Evidence);
        var drained = new StateStore(workspace.Paths, new FakeProcesses().Inspect).Drain().Sessions["codex:s1"];
        var afterDrain = attachments.Get("future")!;
        Assert.Equal(before, Json.Encode(afterDrain.Evidence));
        Assert.Equal(2, afterDrain.Evidence!.Revision);
        // Only the legacy fields were updated.
        Assert.Equal("unavailable", afterDrain.Health);
        Assert.Equal("unavailable", drained.AttachmentHealth);
        attachments.Adopt(Shared("future", now) with { RegisteredAt = old }, now);
        Assert.Equal(before, Json.Encode(attachments.Get("future")!.Evidence));
    }

    [Fact]
    public void StaleRuntimeEvidenceIsNotWork()
    {
        var now = DateTimeOffset.UtcNow;
        var evidence = new FakeEvidence();
        var attachment = Shared("shared", now.AddHours(-1));
        evidence.Works["conversation"] = new(WorkSignal.Active, now - ConversationLifecycle.RuntimeFreshness - TimeSpan.FromSeconds(1));
        var result = Evaluate(attachment, now, evidence, new FakeProcesses());
        Assert.Equal("unavailable", result.Attachment.Health);
        // An observation from the future is not trusted either.
        evidence.Works["conversation"] = new(WorkSignal.Active, now.AddHours(1));
        var future = Evaluate(attachment, now, evidence, new FakeProcesses());
        Assert.Equal("unavailable", future.Attachment.Health);
        Assert.Equal(attachment.UnverifiedSince, future.Attachment.UnverifiedSince);
        // A small forward skew is accepted but clamped to now, so the evidence time never runs ahead of the clock.
        evidence.Works["conversation"] = new(WorkSignal.Active, now.AddSeconds(10));
        var skewed = Evaluate(attachment, now, evidence, new FakeProcesses());
        Assert.Equal("unverified", skewed.Attachment.Health);
        Assert.Equal(now, skewed.Attachment.UnverifiedSince);
    }

    // QA-2100: a record an older tray already ended stays ended; neither facts nor a newer hook revive it.
    [Fact]
    public void ASharedRecordWithEndedAtStaysEndedAndIsNotRevivedByFactsOrHooks()
    {
        var now = DateTimeOffset.UtcNow;
        var endedAt = now.AddMinutes(-20);
        var ended = Shared("shared", now, Bound(now)) with { EndedAt = endedAt, Health = "confirmed-ended" };
        var evidence = new FakeEvidence();
        evidence.Terminals["conversation"] = Facts(now, listener: false);
        evidence.Works["conversation"] = new(WorkSignal.Active, now);
        var result = Evaluate(ended, now, evidence, new FakeProcesses());
        Assert.Equal("confirmed-ended", result.Attachment.Health);
        Assert.Equal(endedAt, result.Attachment.EndedAt);
        Assert.Equal(ended.Evidence, result.Attachment.Evidence);
    }

    // ANALYSIS-1300: non-late facts are the current binding observation (probes P1 to P4).
    private static FakeEvidence WithTerminal(SharedTerminalFacts facts)
    {
        var evidence = new FakeEvidence();
        evidence.Terminals["conversation"] = facts;
        return evidence;
    }

    [Fact]
    public void AMissingOriginOutcomeWithoutAnOwnerIsStoredForANeverBoundConversation()
    {
        var now = DateTimeOffset.UtcNow;
        var facts = new SharedTerminalFacts(now, null, null, EvidenceStatus.Unverified, "missing-origin");
        var result = Evaluate(Shared("shared", now), now, WithTerminal(facts), new FakeProcesses());
        Assert.Equal(EvidenceStatus.Unverified, result.Terminal);
        Assert.Equal("missing-origin", result.Attachment.Evidence!.TerminalBinding.Source);
        Assert.Equal(now, result.Attachment.Evidence.TerminalBinding.ObservedAt);
    }

    [Fact]
    public void FreshFactsWithoutAnOwnerReplaceAStaleOwnerExitedClaim()
    {
        var now = DateTimeOffset.UtcNow;
        var processes = new FakeProcesses();
        var record = Shared("shared", now, Bound(now.AddMinutes(-3)));
        processes.Ended.Add(Owner);
        var disconnected = Evaluate(record, now.AddMinutes(-2), new FakeEvidence(), processes).Attachment;
        Assert.Equal("owner-exited", disconnected.Evidence!.TerminalBinding.Source);
        var facts = new SharedTerminalFacts(now, null, null, EvidenceStatus.Unverified, "missing-origin");
        var result = Evaluate(disconnected, now, WithTerminal(facts), processes);
        Assert.Equal(EvidenceStatus.Unverified, result.Terminal);
        Assert.Equal("missing-origin", result.Attachment.Evidence!.TerminalBinding.Source);
        Assert.Null(result.Attachment.Evidence.TerminalBinding.Client);
    }

    [Fact]
    public void AnUnreadableOwnerDoesNotBlockAFreshDisconnectOutcome()
    {
        var now = DateTimeOffset.UtcNow;
        var record = Shared("shared", now, Bound(now.AddMinutes(-1)));
        var result = new AttachmentHealth(_ => ProcessLiveness.Unverified)
            .Evaluate(record, now, Grace, WithTerminal(Facts(now, listener: false)).For("conversation"));
        var terminal = result.Evidence!.TerminalBinding;
        Assert.Equal(EvidenceStatus.Disconnected, terminal.Binding);
        Assert.Equal("listener-gone", terminal.Source);
    }

    [Fact]
    public void ANewUnreadableOwnerTakesItsShellAndIsCappedToUnverified()
    {
        var now = DateTimeOffset.UtcNow;
        var oldShell = new ProcessIdentity(4100, 410);
        var newShell = new ProcessIdentity(4200, 420);
        var record = Shared("shared", now, Bound(now.AddMinutes(-1)) with { Shell = oldShell });
        var result = new AttachmentHealth(_ => ProcessLiveness.Unverified)
            .Evaluate(record, now, Grace, WithTerminal(Facts(now, OtherOwner, shell: newShell)).For("conversation"));
        var terminal = result.Evidence!.TerminalBinding;
        Assert.Equal(EvidenceStatus.Unverified, terminal.Binding);
        Assert.Equal("owner-unverifiable", terminal.Source);
        Assert.Equal(OtherOwner, terminal.Client);
        Assert.Equal(newShell, terminal.Shell);
    }

    [Fact]
    public void ARetainedIdleBackendThreadIsNeitherLiveNorConnected()
    {
        var now = DateTimeOffset.UtcNow;
        var evidence = new FakeEvidence();
        // The client exited long ago; the backend still holds the thread and thread/read reports idle.
        var attachment = Shared("shared", now.AddHours(-3), Bound(now.AddHours(-3)));
        var processes = new FakeProcesses();
        processes.Ended.Add(Owner);
        evidence.Works["conversation"] = new(WorkSignal.Idle, now);
        var result = Evaluate(attachment, now, evidence, processes);
        Assert.Equal("unavailable", result.Attachment.Health);
        Assert.Equal(EvidenceStatus.Disconnected, result.Terminal);
        Assert.Null(result.Attachment.EndedAt);
    }

    [Fact]
    public void DelayedAndOutOfOrderFactsNeverOverwriteNewerState()
    {
        var now = DateTimeOffset.UtcNow;
        var evidence = new FakeEvidence();
        var processes = new FakeProcesses();
        var attachment = Shared("shared", now, Bound(now.AddMinutes(-5)));
        evidence.Terminals["conversation"] = Facts(now.AddMinutes(-1), listener: false);
        var disconnected = Evaluate(attachment, now, evidence, processes).Attachment;
        Assert.Equal(EvidenceStatus.Disconnected, disconnected.Evidence!.TerminalBinding.Binding);
        // A connected observation taken before the disconnect arrives late: ignored.
        evidence.Terminals["conversation"] = Facts(now.AddMinutes(-2));
        Assert.Equal(EvidenceStatus.Disconnected, Evaluate(disconnected, now, evidence, processes).Attachment.Evidence!.TerminalBinding.Binding);
        // A newer connected observation reattaches the terminal.
        evidence.Terminals["conversation"] = Facts(now.AddSeconds(1));
        var reattached = Evaluate(disconnected, now.AddSeconds(1), evidence, processes);
        Assert.Equal(EvidenceStatus.Connected, reattached.Terminal);
        // And a delayed disconnect cannot undo it.
        evidence.Terminals["conversation"] = Facts(now.AddMinutes(-3), listener: false);
        Assert.Equal(EvidenceStatus.Connected, Evaluate(reattached.Attachment, now.AddSeconds(2), evidence, processes).Terminal);
    }

    [Fact]
    public void AnOwnerWithAReusedPidIsGoneAndAnUnreadableOwnerIsNotClaimedDisconnected()
    {
        var now = DateTimeOffset.UtcNow;
        var self = ProcessProbe.Identify(Environment.ProcessId)!;
        var reused = new Attachment("shared", "codex", null, new(), "D:/example", now)
        {
            Ownership = "shared-runtime",
            Evidence = new AttachmentEvidence { LifecycleOwner = LifecycleOwners.SharedBackend, Terminal = Bound(now, self with { StartedUtcTicks = 1 }) }.Stamped(null)
        };
        var real = Probe(reused, now, ProcessProbe.Inspect);
        Assert.Equal(EvidenceStatus.Disconnected, real.Terminal);
        var live = Probe(reused with { Evidence = reused.Evidence! with { Terminal = Bound(now, self) } }, now, ProcessProbe.Inspect);
        Assert.Equal(EvidenceStatus.Connected, live.Terminal);
        var unreadable = Probe(reused, now, _ => ProcessLiveness.Unverified);
        Assert.Equal(EvidenceStatus.Connected, unreadable.Terminal);
    }

    [Fact]
    public void BellTowerRestartKeepsDisconnectedStateRetainsTheSessionAndReattachesOnNewEvidence()
    {
        using var workspace = new TestWorkspace();
        var at = DateTimeOffset.UtcNow.AddSeconds(-30);
        new AttachmentStore(workspace.Paths).Save(Shared("shared", at, Bound(at.AddMinutes(-1))));
        Publish(workspace, "codex", "shared", "s1", at);
        var processes = new FakeProcesses();
        var first = new FakeEvidence();
        first.Terminals["s1"] = Facts(DateTimeOffset.UtcNow, listener: false);
        var before = new StateStore(workspace.Paths, processes.Inspect).Drain(first.Snapshot()).Sessions["codex:s1"];
        var identity = new IdentityStore(workspace.Paths).Get(before.Key, workspace.Root).Palette;
        Assert.Equal(EvidenceStatus.Disconnected, new AttachmentStore(workspace.Paths).Get("shared")!.Evidence!.TerminalBinding.Binding);
        // Restart: a new store without any facts changes nothing and does not fabricate a connection or an end.
        var restarted = new StateStore(workspace.Paths, processes.Inspect).Drain().Sessions["codex:s1"];
        Assert.False(restarted.Ended);
        Assert.Equal(before.Episode, restarted.Episode);
        Assert.Single(restarted.Waits);
        Assert.Equal(EvidenceStatus.Disconnected, new AttachmentStore(workspace.Paths).Get("shared")!.Evidence!.TerminalBinding.Binding);
        // A new client resumes the conversation and is bound again.
        var second = new FakeEvidence();
        second.Terminals["s1"] = Facts(DateTimeOffset.UtcNow.AddSeconds(1), OtherOwner);
        var reattached = new StateStore(workspace.Paths, processes.Inspect).Drain(second.Snapshot()).Sessions["codex:s1"];
        var stored = new AttachmentStore(workspace.Paths).Get("shared")!;
        Assert.Equal(EvidenceStatus.Connected, stored.Evidence!.TerminalBinding.Binding);
        Assert.Equal(OtherOwner, stored.Evidence.TerminalBinding.Client);
        Assert.Equal(before.Episode, reattached.Episode);
        Assert.Equal(identity, new IdentityStore(workspace.Paths).Get(before.Key, workspace.Root).Palette);
        // The restarted tray notices a bound owner that died while it was stopped, without any new facts.
        processes.Ended.Add(OtherOwner);
        new StateStore(workspace.Paths, processes.Inspect).Drain();
        Assert.Equal("owner-exited", new AttachmentStore(workspace.Paths).Get("shared")!.Evidence!.TerminalBinding.Source);
    }

    [Fact]
    public void MixedSimultaneousClaudeAndCodexSessionsAreDecidedIndependently()
    {
        using var workspace = new TestWorkspace();
        new ConfigStore(workspace.Paths).Update(config => config.AttachmentEvidenceGraceMinutes = 1);
        var at = DateTimeOffset.UtcNow.AddSeconds(-20);
        var claude = new ProcessIdentity(6001, 61);
        var embedded = new ProcessIdentity(6002, 62);
        var attachments = new AttachmentStore(workspace.Paths);
        attachments.Save(new Attachment("claude-direct", "claude", claude, new("unknown"), workspace.Root, at) { Ownership = "observed" });
        attachments.Save(new Attachment("codex-embedded", "codex", embedded, new("unknown"), workspace.Root, at) { Ownership = "observed" });
        attachments.Save(Shared("codex-shared", at, Bound(at)));
        Publish(workspace, "claude", "claude-direct", "c1", at);
        Publish(workspace, "codex", "codex-embedded", "e1", at);
        Publish(workspace, "codex", "codex-shared", "sh1", at);
        var processes = new FakeProcesses();
        processes.Ended.Add(embedded);
        processes.Ended.Add(Owner);
        var evidence = new FakeEvidence();
        evidence.Terminals["sh1"] = Facts(DateTimeOffset.UtcNow);
        evidence.Works["sh1"] = new(WorkSignal.Active, DateTimeOffset.UtcNow);
        var state = new StateStore(workspace.Paths, processes.Inspect).Drain(evidence.Snapshot());
        Assert.Equal("verified-live", state.Sessions["claude:c1"].AttachmentHealth);
        Assert.False(state.Sessions["claude:c1"].Ended);
        Assert.True(state.Sessions["codex:e1"].Ended);
        var shared = state.Sessions["codex:sh1"];
        Assert.False(shared.Ended);
        Assert.Equal("unverified", shared.AttachmentHealth);
        Assert.True(shared.RequiresAttention(DateTimeOffset.UtcNow));
        Assert.Equal(EvidenceStatus.Disconnected, attachments.Get("codex-shared")!.Evidence!.TerminalBinding.Binding);
        Assert.Null(attachments.Get("claude-direct")!.Evidence?.TerminalBinding.Client);
        Assert.Equal(claude, attachments.Get("claude-direct")!.Process);
    }

    [Fact]
    public void LegacySharedRecordsWithoutEvidenceKeepTheirBehaviorAndGainOnlyAdditiveEvidence()
    {
        using var workspace = new TestWorkspace();
        var at = DateTimeOffset.UtcNow.AddHours(-1);
        var legacy = Json.Decode<Attachment>($$"""
            {"id":"old","provider":"codex","host":{"kind":"unknown"},"directory":"D:/example","registeredAt":"{{at:O}}","ownership":"shared-runtime","futureField":1}
            """);
        new AttachmentStore(workspace.Paths).Save(legacy);
        Publish(workspace, "codex", "old", "s1", at);
        var session = new StateStore(workspace.Paths, new FakeProcesses().Inspect).Drain().Sessions["codex:s1"];
        Assert.Equal("unavailable", session.AttachmentHealth);
        var stored = new AttachmentStore(workspace.Paths).Get("old")!;
        Assert.True(stored.AdditionalData!.ContainsKey("futureField"));
        Assert.Null(stored.Process);
        Assert.Null(stored.EndedAt);
        Assert.Equal(EvidenceStatus.Unverified, stored.Evidence!.TerminalBinding.Binding);
    }

    // ----- Task 2.4: an older consumer reads the new records safely, so no consumer gate is required. -----

    // The attachment record the shipped health-v1 tray reads: no evidence property, extension data retained.
    private sealed record OlderAttachment(string Id, string Provider, ProcessIdentity? Process, HostTarget Host, string Directory, DateTimeOffset RegisteredAt)
    {
        public string Health { get; init; } = "unverified";
        public DateTimeOffset? UnverifiedSince { get; init; }
        public DateTimeOffset? EndedAt { get; init; }
        public string Ownership { get; init; } = "unverified";
        [System.Text.Json.Serialization.JsonExtensionData]
        public Dictionary<string, JsonElement>? AdditionalData { get; set; }
    }

    [Fact]
    public void AnOlderConsumerNeverReadsADisconnectedOrDemotedSharedRecordAsEndedOrLive()
    {
        var now = DateTimeOffset.UtcNow;
        var processes = new FakeProcesses();
        processes.Ended.Add(Owner);
        var shapes = new List<Attachment>();
        foreach (var (name, terminal, facts, work) in new (string, TerminalEvidence, SharedTerminalFacts?, RuntimeWorkFacts?)[]
        {
            ("owner killed idle", Bound(now), Facts(now), null),
            ("owner killed active", Bound(now), Facts(now), new(WorkSignal.Active, now)),
            ("demoted", Bound(now, OtherOwner), Facts(now, OtherOwner, demoted: true), new(WorkSignal.Active, now)),
            ("never bound", new TerminalEvidence(), null, null)
        })
        {
            var evidence = new FakeEvidence();
            if (facts is not null)
            {
                evidence.Terminals["conversation"] = facts;
            }
            if (work is not null)
            {
                evidence.Works["conversation"] = work;
            }
            var result = Evaluate(Shared("shared-" + name, now, terminal), now, evidence, processes).Attachment;
            shapes.Add(result);
            // Invariants of every persisted shared record: nothing a legacy rule treats as an end or as liveness evidence.
            Assert.Null(result.Process);
            Assert.Null(result.Host.Shell);
            Assert.Null(result.EndedAt);
            Assert.Equal("shared-runtime", result.Ownership);
            Assert.Contains(result.Health, new[] { "unverified", "unavailable" });
        }
        foreach (var attachment in shapes)
        {
            var older = Json.Decode<OlderAttachment>(Json.Encode(attachment));
            Assert.True(older.AdditionalData!.ContainsKey("evidence"));
            // The health-v1 evaluation: the unchanged legacy evaluator over the same fields.
            var legacy = new AttachmentHealth(_ => ProcessLiveness.Unverified).Evaluate(attachment with { Evidence = null }, now, Grace);
            Assert.Equal("unverified", legacy.Health);
            var stale = new AttachmentHealth(_ => ProcessLiveness.Unverified).Evaluate(attachment with { Evidence = null }, now.AddHours(1), Grace);
            Assert.Equal("unavailable", stale.Health);
            // Rewriting the record through the older shape keeps the evidence for the current tray.
            var restored = Json.Decode<Attachment>(Json.Encode(older));
            Assert.Equal(attachment.Evidence!.TerminalBinding.Binding, restored.Evidence!.TerminalBinding.Binding);
            Assert.Equal(attachment.Evidence.TerminalBinding.Source, restored.Evidence.TerminalBinding.Source);
        }
    }

    [Fact]
    public void TheLifecycleReducerNeverWritesALegacyEndOrLivenessFieldForASharedConversation()
    {
        using var workspace = new TestWorkspace();
        var at = DateTimeOffset.UtcNow.AddSeconds(-20);
        new AttachmentStore(workspace.Paths).Save(Shared("shared", at, Bound(at)));
        Publish(workspace, "codex", "shared", "s1", at);
        var processes = new FakeProcesses();
        processes.Ended.Add(Owner);
        var evidence = new FakeEvidence();
        evidence.Terminals["s1"] = Facts(DateTimeOffset.UtcNow);
        evidence.Works["s1"] = new(WorkSignal.Active, DateTimeOffset.UtcNow);
        var state = new StateStore(workspace.Paths, processes.Inspect).Drain(evidence.Snapshot());
        var stored = new AttachmentStore(workspace.Paths).Get("shared")!;
        Assert.Equal(EvidenceStatus.Disconnected, stored.Evidence!.TerminalBinding.Binding);
        Assert.Null(stored.Process);
        Assert.Null(stored.Host.Shell);
        Assert.Null(stored.EndedAt);
        Assert.Equal("unverified", stored.Health);
        Assert.False(state.Sessions["codex:s1"].Ended);
        Assert.Equal(1, state.AttachmentHealthVersion);
        // The current compatibility requirement is unchanged: a health-v1 consumer is enough, a legacy one is still refused.
        BellTower.Distribution.AttachmentHealthCompatibility.Ensure(workspace.Paths.Root, 1);
        Assert.Throws<InvalidDataException>(() => BellTower.Distribution.AttachmentHealthCompatibility.Ensure(workspace.Paths.Root, 0));
    }

    [Fact]
    public void RollbackToALegacyConsumerIsStillRejectedAndSharedAndManagedSessionsArePreserved()
    {
        using var workspace = new TestWorkspace();
        var at = DateTimeOffset.UtcNow.AddSeconds(-20);
        var attachments = new AttachmentStore(workspace.Paths);
        attachments.Save(Shared("shared", at, Bound(at)));
        attachments.Save(new Attachment("managed", "claude", new ProcessIdentity(7001, 71), new("unknown"), workspace.Root, at) { Ownership = "managed" });
        Publish(workspace, "codex", "shared", "s1", at);
        Publish(workspace, "claude", "managed", "m1", at);
        var processes = new FakeProcesses();
        processes.Ended.Add(Owner);
        var evidence = new FakeEvidence();
        evidence.Terminals["s1"] = Facts(DateTimeOffset.UtcNow);
        new StateStore(workspace.Paths, processes.Inspect).Drain(evidence.Snapshot());
        var installation = Updates.DistributionTests.CreateInstallation(Path.Combine(workspace.Root, "installed"), "0.1.7");
        var beforeState = File.ReadAllBytes(workspace.Paths.State);
        var beforeSelection = File.ReadAllBytes(installation.SelectionPath);
        var beforeShared = File.ReadAllBytes(Directory.GetFiles(workspace.Paths.Attachments, "*.json").First(file => File.ReadAllText(file).Contains("shared-runtime")));
        var offer = new BellTower.Updates.ReleaseOffer(new BellTower.Distribution.ReleaseContract("0.1.9"), new string('a', 64));
        var error = Assert.Throws<InvalidDataException>(() => BellTower.Updates.UpdateTransaction.Stage(installation, workspace.Paths, "not-read.zip", offer));
        Assert.Contains("health-v1", error.Message);
        Assert.Equal(beforeSelection, File.ReadAllBytes(installation.SelectionPath));
        Assert.Equal(beforeState, File.ReadAllBytes(workspace.Paths.State));
        Assert.Equal(beforeShared, File.ReadAllBytes(Directory.GetFiles(workspace.Paths.Attachments, "*.json").First(file => File.ReadAllText(file).Contains("shared-runtime"))));
        // A health-v1 successor is accepted and reads the same sessions.
        BellTower.Distribution.CaptureCompatibility.EnsureTrayInvocation(["tray", "--data-dir", workspace.Paths.Root], 1, 1);
        var reread = new StateStore(workspace.Paths, processes.Inspect).Read();
        Assert.Equal(2, reread.Sessions.Count);
        Assert.False(reread.Sessions["codex:s1"].Ended);
    }
}
