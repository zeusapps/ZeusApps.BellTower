# Analysis 002: disposable reducer and pump probes

Code-analyzer evidence for `reports/analysis-002.md` (recheck of ANALYSIS-001…003 and review of tasks 2.6 and 2A.6 after Implementation 002). The probe is a throwaway console project outside the repository (session scratch directory, not retained). It references the Release `bell-tower.dll` built from the reviewed identity, at `<repo>/tests/BellTower.Tests/bin/Release/net10.0-windows/`. That build is newer than the last source edit. The assembly is named `BellTower.Tests` only so that the existing `InternalsVisibleTo` lets it call `SessionReducer.Normalize` and the internal stores. Reducer probes run in memory. Probe P9 drives the real `CodexPermissionPump`, `EventStore` and `StateStore` against a disposable data directory under the probe's own output folder, deleted afterwards. It uses a fake `ICodexAttentionClient`, with no runtime, no audio and no default data directory.

- Client: Claude Code (subagent), model `claude-opus-5-5`; .NET 10 SDK; Release configuration.
- Reviewed identity: `head=754aed5 baseline=754aed5 files=58 diff=b9e37ea92406 untracked=13/6f4f55b6a727` (`src tests docs README.md`).
- `Runtime(...)` builds exactly the observation `CodexPermissionPump.SubmitConfirmation` publishes: `HumanPrompt`, `RuntimeApproval`, the current `AdapterRevision` and no `IngressAt`. When the session already has a causal journal, the reducer routes it causally.
- The decimal separator in the P5b output follows the probe machine's locale (`1,999` means 1.999 s).

## Project

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net10.0-windows</TargetFramework>
    <UseWindowsForms>true</UseWindowsForms>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <AssemblyName>BellTower.Tests</AssemblyName>
  </PropertyGroup>
  <ItemGroup>
    <Reference Include="bell-tower"><HintPath><repo>/tests/BellTower.Tests/bin/Release/net10.0-windows/bell-tower.dll</HintPath></Reference>
    <Reference Include="BellTower.Protocol"><HintPath><repo>/tests/BellTower.Tests/bin/Release/net10.0-windows/BellTower.Protocol.dll</HintPath></Reference>
  </ItemGroup>
</Project>
```

## Program.cs

```csharp
using BellTower.Core;
using BellTower.IO;
using BellTower.Providers;
using BellTower.Sessions;

// Analysis 002: disposable probes against the Release bell-tower.dll built from the reviewed identity. Reducer probes are
// in-memory; the pump probe uses a throwaway data directory under the probe's own folder, a fake runtime client and no audio.
var start = new DateTimeOffset(2026, 10, 26, 0, 0, 0, TimeSpan.Zero);
Observation Ev(string provider, EventKind kind, double s, string? op = null, string? fp = null, string id = "", bool causal = true, bool legacy = false) => new()
{
    Provider = provider, SessionId = "s", AttachmentId = "a", Kind = kind, At = start.AddSeconds(s), TurnId = null,
    OperationId = op, OperationFingerprint = fp, Id = id.Length > 0 ? id : kind + "-" + s + "-" + op + fp,
    IngressAt = causal ? start.AddSeconds(s) : null, AdapterRevision = legacy ? null : Observation.HumanWaitRevision
};
Observation ClaudePrompt(double s, string id = "prompt", bool causal = true) => Ev("claude", EventKind.Permission, s, null, null, id, causal) with { HumanPrompt = true, ProviderDelay = TimeSpan.FromSeconds(5) };
// Exactly what CodexPermissionPump.SubmitConfirmation publishes (no IngressAt; the session journal routes it causally when present).
Observation Runtime(double s, string id) => new()
{
    Provider = "codex", SessionId = "s", AttachmentId = "a", Kind = EventKind.Permission, At = start.AddSeconds(s), Id = id,
    HumanPrompt = true, RuntimeApproval = true, AdapterRevision = Observation.HumanWaitRevision
};
AlertPolicy Policy() => new(start.AddSeconds(-1));
string Due(DateTimeOffset? d) => d is { } v ? "+" + (v - start).TotalSeconds + "s" : "null";
string Waits(SessionState s) => string.Join(",", s.Waits.Select(w => $"{w.Reason}:{w.Fingerprint}:{w.Confirmation}{(w.Ambiguous ? ":ambiguous" : "")}"));

// P1 (ANALYSIS-001): legacy producer shape (no adapterRevision) versus current shape, capture (causal) path.
foreach (var legacy in new[] { false, true })
{
    var state = new AppState(); var policy = Policy();
    SessionReducer.Apply(state, Ev("claude", EventKind.Prompt, 0, legacy: legacy));
    SessionReducer.Apply(state, Ev("claude", EventKind.ToolStart, 1, "t1", "bash", legacy: legacy));
    SessionReducer.Apply(state, Ev("claude", EventKind.Permission, 1.1, null, "bash", legacy: legacy));
    SessionReducer.Apply(state, legacy ? Ev("claude", EventKind.Permission, 7.3, null, null, "prompt", legacy: true) : ClaudePrompt(7.3));
    var s = state.Sessions.Values.Single();
    Console.WriteLine($"P1 legacyProducer={legacy}: Reason={s.Reason?.ToString() ?? "null"} waits=[{Waits(s)}] alerts@120s={policy.Collect([s], start.AddSeconds(120), false).Count}");
}
// P1b: a legacy tool-bearing PermissionRequest (fingerprint) is never promoted to a prompt.
{
    var state = new AppState();
    SessionReducer.Apply(state, Ev("claude", EventKind.Prompt, 0, legacy: true));
    SessionReducer.Apply(state, Ev("claude", EventKind.Permission, 1, null, "bash", legacy: true));
    var s = state.Sessions.Values.Single();
    Console.WriteLine($"P1b legacy PermissionRequest: waits=[{Waits(s)}] Reason={s.Reason?.ToString() ?? "null"}");
}

// P2 (ANALYSIS-002): Codex P1 runtime-confirmed and dismissed, then provisional P2 while the runtime still reports a pending
// approval (for P1). Variant "pumpRepublishes" adds the observation the pump publishes next tick: oldest provisional is now P2.
foreach (var causal in new[] { true, false })
foreach (var republish in new[] { false, true })
{
    var state = new AppState(); var policy = Policy();
    SessionReducer.Apply(state, Ev("codex", EventKind.Prompt, 0, causal: causal));
    SessionReducer.Apply(state, Ev("codex", EventKind.Permission, 1, "t1", "p1", causal: causal));
    SessionReducer.Apply(state, Runtime(2, "runtime-p1"));
    var s = state.Sessions.Values.Single();
    s.NativePermissionPending = true; s.NativePermissionSince = start.AddSeconds(2);
    var first = policy.Collect([s], start.AddSeconds(8), false).Count;
    s.AcknowledgedEpisode = s.Episode;
    var episode = s.Episode;
    SessionReducer.Apply(state, Ev("codex", EventKind.Permission, 10, "t2", "p2", causal: causal));
    if (republish)
    {
        SessionReducer.Apply(state, Runtime(11, "runtime-p2"));
    }
    s = state.Sessions.Values.Single();
    s.NativePermissionPending = true; s.NativePermissionSince = start.AddSeconds(2);
    Console.WriteLine($"P2 causal={causal} pumpRepublishes={republish}: firstAlerts={first} episodeChanged={s.Episode != episode} dismissedVisibleAgain={s.RequiresAttention(start.AddSeconds(20))} reannouncements={policy.Collect([s], start.AddSeconds(20), false).Count} waits=[{Waits(s)}]");
}

// P3 (ANALYSIS-002): Codex question pending, provisional permission confirmed by the runtime; question chip dismissed first.
foreach (var causal in new[] { true, false })
{
    var state = new AppState(); var policy = Policy();
    SessionReducer.Apply(state, Ev("codex", EventKind.Prompt, 0, causal: causal));
    SessionReducer.Apply(state, Ev("codex", EventKind.Question, 1, "q1", "qf", causal: causal));
    var questionEpisode = state.Sessions.Values.Single().Episode;
    policy.Collect([state.Sessions.Values.Single()], start.AddSeconds(1), false);
    SessionReducer.Apply(state, Ev("codex", EventKind.Permission, 2, "t1", "pf", causal: causal));
    state.Sessions.Values.Single().AcknowledgedEpisode = questionEpisode;
    SessionReducer.Apply(state, Runtime(3, "runtime-pf"));
    var s = state.Sessions.Values.Single();
    s.NativePermissionPending = true; s.NativePermissionSince = start.AddSeconds(3);
    Console.WriteLine($"P3 codex causal={causal}: Reason={s.Reason} ownEpisode={s.Episode != questionEpisode} NotificationDue={Due(s.NotificationDue)} visibleAfterQuestionDismissed={s.RequiresAttention(start.AddSeconds(20))} announced={policy.Collect([s], start.AddSeconds(20), false).Count}");
}

// P4 (ANALYSIS-003): P1 at 0.5 s, P2 at 5 s, unattributed prompt at 6.5 s.
{
    var state = new AppState();
    SessionReducer.Apply(state, Ev("claude", EventKind.Prompt, 0));
    SessionReducer.Apply(state, Ev("claude", EventKind.Permission, 0.5, null, "p1"));
    SessionReducer.Apply(state, Ev("claude", EventKind.Permission, 5, null, "p2"));
    SessionReducer.Apply(state, ClaudePrompt(6.5));
    Console.WriteLine($"P4: waits=[{Waits(state.Sessions.Values.Single())}]");
}
// P5 (ANALYSIS-003): P1 resolved at 6 s, P2 pending since 5.5 s, racing prompt at 6.4 s.
{
    var state = new AppState();
    SessionReducer.Apply(state, Ev("claude", EventKind.Prompt, 0));
    SessionReducer.Apply(state, Ev("claude", EventKind.ToolStart, 0.3, "t1", "p1"));
    SessionReducer.Apply(state, Ev("claude", EventKind.Permission, 0.4, null, "p1"));
    SessionReducer.Apply(state, Ev("claude", EventKind.ToolStart, 5.4, "t2", "p2"));
    SessionReducer.Apply(state, Ev("claude", EventKind.Permission, 5.5, null, "p2"));
    SessionReducer.Apply(state, Ev("claude", EventKind.ToolEnd, 6, "t1", "p1"));
    SessionReducer.Apply(state, ClaudePrompt(6.4));
    var s = state.Sessions.Values.Single();
    Console.WriteLine($"P5: receipts={s.ResolvedApprovals.Count} waits=[{Waits(s)}] Reason={s.Reason?.ToString() ?? "null"}");
}
// P5b: racing-window boundary with an old-enough sibling (P2 since 0.5 s), P1 resolved at 6 s.
foreach (var offset in new[] { 1.999, 2.0, 2.001 })
{
    var state = new AppState();
    SessionReducer.Apply(state, Ev("claude", EventKind.Prompt, 0));
    SessionReducer.Apply(state, Ev("claude", EventKind.ToolStart, 0.3, "t1", "p1"));
    SessionReducer.Apply(state, Ev("claude", EventKind.Permission, 0.4, null, "p1"));
    SessionReducer.Apply(state, Ev("claude", EventKind.Permission, 0.5, null, "p2"));
    SessionReducer.Apply(state, Ev("claude", EventKind.ToolEnd, 6, "t1", "p1"));
    SessionReducer.Apply(state, ClaudePrompt(6 + offset));
    Console.WriteLine($"P5b prompt at resolution+{offset}s: waits=[{Waits(state.Sessions.Values.Single())}]");
}

// P6 (ANALYSIS-003 decision, task 2A.6): question + permission pending, unattributed prompt, question answered; no later prompt.
// "human": the permission is a real human wait. "autoReview": the permission is still under automatic review when the question is
// answered and is auto-approved at 40 s; the prompt at 7.3 s is the question's own (the AskUserQuestion fixture emits
// permission_prompt 6.27 s after its approval flow starts).
foreach (var autoReview in new[] { false, true })
{
    var state = new AppState(); var policy = Policy();
    SessionReducer.Apply(state, Ev("claude", EventKind.Prompt, 0));
    SessionReducer.Apply(state, Ev("claude", EventKind.Question, 1, "q1", "qf"));
    SessionReducer.Apply(state, Ev("claude", EventKind.Permission, 1.1, null, "qf") with { QuestionTool = true });
    SessionReducer.Apply(state, Ev("claude", EventKind.ToolStart, 1.15, "t1", "pf"));
    SessionReducer.Apply(state, Ev("claude", EventKind.Permission, 1.2, null, "pf"));
    SessionReducer.Apply(state, ClaudePrompt(7.3));
    var held = state.Sessions.Values.Single().HeldPrompt is not null;
    policy.Collect([state.Sessions.Values.Single()], start.AddSeconds(7.3), false);
    SessionReducer.Apply(state, Ev("claude", EventKind.ToolEnd, 20, "q1", "qf"));
    var s = state.Sessions.Values.Single();
    var after = $"Reason={s.Reason?.ToString() ?? "null"} waits=[{Waits(s)}] permissionAlerts@21s={policy.Collect([s], start.AddSeconds(21), false).Count(x => x.Reason == AttentionReason.Permission)}";
    if (autoReview)
    {
        SessionReducer.Apply(state, Ev("claude", EventKind.ToolEnd, 40, "t1", "pf"));
        s = state.Sessions.Values.Single();
        after += $" afterAutoApproval@40s Reason={s.Reason?.ToString() ?? "null"}";
    }
    Console.WriteLine($"P6 {(autoReview ? "autoReview" : "human")}: held={held} afterQuestionAnswered {after}");
}

// P10 (deviation 3): back-dated runtime approval (At = oldest provisional + 1 ms, as the pump publishes for waits that predate the
// tray run). (a) a second provisional request also predates the run; (b) more than CausalSessionReducer.Window events followed the wait.
foreach (var causal in new[] { true, false })
{
    var state = new AppState();
    SessionReducer.Apply(state, Ev("codex", EventKind.Prompt, 0, causal: causal));
    SessionReducer.Apply(state, Ev("codex", EventKind.Permission, 1, "t1", "p1", causal: causal));
    SessionReducer.Apply(state, Ev("codex", EventKind.Permission, 30, "t2", "p2", causal: causal));
    SessionReducer.Apply(state, Runtime(1.001, "runtime-backdated"));
    Console.WriteLine($"P10a causal={causal}: waits=[{Waits(state.Sessions.Values.Single())}]");
}
{
    var state = new AppState();
    SessionReducer.Apply(state, Ev("codex", EventKind.Prompt, 0));
    SessionReducer.Apply(state, Ev("codex", EventKind.Permission, 1, "t1", "p1"));
    for (var i = 0; i < CausalSessionReducer.Window + 10; i++)
    {
        SessionReducer.Apply(state, Ev("codex", i % 2 == 0 ? EventKind.BackgroundStart : EventKind.BackgroundEnd, 2 + i * 0.01, null, null, "bg-" + i) with { BackgroundId = "b" + (i / 2), IsMainThread = false });
    }
    SessionReducer.Apply(state, Runtime(1.001, "runtime-backdated"));
    var s = state.Sessions.Values.Single();
    SessionReducer.Apply(state, Runtime(1.001, "runtime-backdated"));
    Console.WriteLine($"P10b after {CausalSessionReducer.Window + 10} later events: waits=[{Waits(state.Sessions.Values.Single())}] Reason={state.Sessions.Values.Single().Reason?.ToString() ?? "null"}");
}

// P9 (ANALYSIS-002 end to end): the real CodexPermissionPump with a fake runtime client and isolated data directory.
// P1 is runtime-confirmed and dismissed; a newer P2 arrives while the runtime still reports a pending approval (for P1).
foreach (var causal in new[] { true, false })
{
    var root = Path.Combine(AppContext.BaseDirectory, "probe-data", Guid.NewGuid().ToString("N"));
    var paths = new DataPaths(root);
    paths.Ensure();
    try
    {
        var process = ProcessProbe.Identify(Environment.ProcessId)!;
        var runtime = new CodexRuntime("runtime-one", "ws://127.0.0.1:12345", process, process);
        var runtimes = new CodexRuntimeStore(paths);
        Directory.CreateDirectory(Path.GetDirectoryName(runtimes.RecordPath(runtime.Attachment))!);
        AtomicFile.Write(runtimes.RecordPath(runtime.Attachment), BellTower.IO.Json.Encode(runtime));
        File.WriteAllText(runtimes.TokenPath(runtime.Attachment), "fixture-token");
        new AttachmentStore(paths).Save(new(runtime.Attachment, "codex", process, new("unknown", process), root, DateTimeOffset.UtcNow));
        var events = new EventStore(paths);
        var store = new StateStore(paths);
        Observation Codex(EventKind kind, DateTimeOffset at, string? op, string? fp) => new()
        {
            Provider = "codex", SessionId = "one", AttachmentId = runtime.Attachment, Kind = kind, At = at, TurnId = "turn-1",
            OperationId = op, OperationFingerprint = fp, Directory = root, IngressAt = causal ? at : null, AdapterRevision = Observation.HumanWaitRevision
        };
        var t0 = DateTimeOffset.UtcNow.AddMinutes(-1);
        events.Publish(Codex(EventKind.Prompt, t0, null, null));
        events.Publish(Codex(EventKind.Permission, t0.AddSeconds(1), "t1", "p1"));
        var session = store.Drain().Sessions["codex:one"];
        var status = new FakeStatus { Pending = true };
        using var pump = new CodexPermissionPump(paths, () => new FakeClient(status));
        await pump.RefreshAsync([session]);
        var firstPublished = Directory.GetFiles(paths.Inbox).Length;
        session = store.Drain().Sessions["codex:one"];
        var policy = new AlertPolicy(t0.AddSeconds(-1));
        var now = DateTimeOffset.UtcNow;
        pump.Apply([session], now);
        var p1Visible = session.RequiresAttention(now);
        store.Acknowledge("codex:one");
        session = store.Drain().Sessions["codex:one"];
        var episode = session.Episode;
        policy.Collect([session], now, false);
        events.Publish(Codex(EventKind.Permission, DateTimeOffset.UtcNow, "t2", "p2"));
        session = store.Drain().Sessions["codex:one"];
        await pump.RefreshAsync([session]);
        await pump.RefreshAsync([session]);
        var republished = Directory.GetFiles(paths.Inbox).Length;
        session = store.Drain().Sessions["codex:one"];
        await pump.RefreshAsync([session]);
        var applied = DateTimeOffset.UtcNow;
        pump.Apply([session], applied);
        var later = applied.AddSeconds(10);
        Console.WriteLine($"P9 pump causal={causal}: firstPublished={firstPublished} p1Visible={p1Visible} republishedForP2={republished} waits=[{Waits(session)}] episodeChanged={session.Episode != episode} dismissedChipBack@+10s={session.RequiresAttention(later)} reannouncements@+10s={policy.Collect([session], later, false).Count}");
    }
    finally
    {
        Directory.Delete(root, true);
    }
}

sealed class FakeStatus { public bool? Pending { get; set; } }
sealed class FakeClient(FakeStatus status) : ICodexAttentionClient
{
    public Task ConnectAsync(string endpoint, string token, CancellationToken cancellation) => Task.CompletedTask;
    public Task<bool?> ReadPermissionPendingAsync(string thread, CancellationToken cancellation) => Task.FromResult(status.Pending);
    public void Dispose() { }
}
```

## Output (`dotnet build -c Release`, then run)

```text
P1 legacyProducer=False: Reason=Permission waits=[Permission:bash:Confirmed] alerts@120s=1
P1 legacyProducer=True: Reason=Permission waits=[Permission:bash:Confirmed] alerts@120s=1
P1b legacy PermissionRequest: waits=[Permission:bash:Provisional] Reason=null
P2 causal=True pumpRepublishes=False: firstAlerts=1 episodeChanged=False dismissedVisibleAgain=False reannouncements=0 waits=[Permission:p1:Confirmed,Permission:p2:Provisional]
P2 causal=True pumpRepublishes=True: firstAlerts=1 episodeChanged=True dismissedVisibleAgain=True reannouncements=1 waits=[Permission:p1:Confirmed,Permission:p2:Confirmed]
P2 causal=False pumpRepublishes=False: firstAlerts=1 episodeChanged=False dismissedVisibleAgain=False reannouncements=0 waits=[Permission:p1:Confirmed,Permission:p2:Provisional]
P2 causal=False pumpRepublishes=True: firstAlerts=1 episodeChanged=True dismissedVisibleAgain=True reannouncements=1 waits=[Permission:p1:Confirmed,Permission:p2:Confirmed]
P3 codex causal=True: Reason=Permission ownEpisode=True NotificationDue=+7s visibleAfterQuestionDismissed=True announced=1
P3 codex causal=False: Reason=Permission ownEpisode=True NotificationDue=+7s visibleAfterQuestionDismissed=True announced=1
P4: waits=[Permission:p1:Confirmed,Permission:p2:Provisional]
P5: receipts=1 waits=[Permission:p2:Provisional] Reason=null
P5b prompt at resolution+1,999s: waits=[Permission:p2:Provisional:ambiguous]
P5b prompt at resolution+2s: waits=[Permission:p2:Provisional:ambiguous]
P5b prompt at resolution+2,001s: waits=[Permission:p2:Confirmed]
P6 human: held=True afterQuestionAnswered Reason=Permission waits=[Permission:pf:Confirmed] permissionAlerts@21s=1
P6 autoReview: held=True afterQuestionAnswered Reason=Permission waits=[Permission:pf:Confirmed] permissionAlerts@21s=1 afterAutoApproval@40s Reason=null
P10a causal=True: waits=[Permission:p1:Confirmed,Permission:p2:Provisional]
P10a causal=False: waits=[Permission:p1:Confirmed,Permission:p2:Confirmed]
P10b after 266 later events: waits=[Permission:p1:Provisional] Reason=null
P9 pump causal=True: firstPublished=1 p1Visible=True republishedForP2=2 waits=[Permission:p1:Confirmed,Permission:p2:Confirmed] episodeChanged=True dismissedChipBack@+10s=True reannouncements@+10s=1
P9 pump causal=False: firstPublished=1 p1Visible=True republishedForP2=2 waits=[Permission:p1:Confirmed,Permission:p2:Confirmed] episodeChanged=True dismissedChipBack@+10s=True reannouncements@+10s=1
```

## Reading

- P1 (ANALYSIS-001): the legacy producer shape now confirms and alerts exactly like the current shape. P1b: a legacy tool-bearing `PermissionRequest` stays provisional.
- P2 (ANALYSIS-002), reducer only: the causal and unordered paths now agree. Without a further runtime observation, the newer request neither changes the episode nor re-announces. With the observation the pump actually publishes next (`pumpRepublishes=True`), the newer request is confirmed and gets a new episode. The dismissed chip then returns and is re-announced, identically on both paths.
- P9 (ANALYSIS-002), real pump end to end: P1 is runtime-confirmed and dismissed. P2 then arrives while the fake runtime still reports a pending approval. The pump publishes a runtime approval keyed to P2, the next drain confirms P2, and the dismissed chip returns with one re-announcement, on both paths. The pump published two inbox files for the same deterministic ID because the probe refreshed twice without draining; the reducer dropped the duplicate ID.
- P3 (ANALYSIS-002): a Codex permission confirmed by the runtime while a dismissed question owns the episode now gets its own episode, is announced and stays visible. Both paths agree.
- P4, P5 (ANALYSIS-003): fixed. P5b: the 2 s racing window is inclusive at 2.000 s, and at 2.001 s the old-enough sibling is confirmed.
- P6 (task 2A.6): when the permission is a human wait, it is confirmed and alerts once the question is answered. When the permission is still under automatic review at that moment (the prompt was the question's own), it is equally confirmed and alerts. It is then auto-approved at 40 s.
- P10a (deviation 3): a back-dated runtime approval sorts before a later request in the causal journal and confirms only the older request. The unordered path confirms both. This is transient in production, because the pump republishes for the remaining request on the next tick.
- P10b (deviation 3): once more than `CausalSessionReducer.Window` events follow the wait, a back-dated runtime approval falls below the causal floor and is dropped. Its ID is already recorded as applied, so the identical ID the pump republishes is ignored, and the restored wait stays provisional.
