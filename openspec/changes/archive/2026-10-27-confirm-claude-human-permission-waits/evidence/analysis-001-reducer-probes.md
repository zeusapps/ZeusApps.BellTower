# Analysis 001: disposable reducer probes

Code-analyzer evidence for `reports/analysis-001.md`. In-memory probes only: no data directory, provider settings, tray, audio or session was touched. The probe is a throwaway console project outside the repository (scratch directory, not retained) that references the Release `bell-tower.dll` built from the reviewed identity (`<repo>/tests/BellTower.Tests/bin/Release/net10.0-windows/bell-tower.dll`, built after the last source edit). It calls the public `SessionReducer.Apply`, sets the transient `NativePermissionPending`/`NativePermissionSince` exactly as `CodexPermissionPump.Apply` does, and asks `AlertPolicy.Collect`/`RequiresAttention` for the outcome. Observations with `IngressAt` take the causal (capture) path used in production; `causal=False` takes the unordered path.

- Client: Claude Code 2.1.310, model `claude-opus-5-5`; .NET 10 SDK; Release configuration.
- Reviewed identity: `head=754aed5 baseline=754aed5 files=50 diff=6b6da7c0e29e untracked=10/b02e4c1ab43a` (`src tests docs`).

## Project

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net10.0-windows</TargetFramework>
    <UseWindowsForms>true</UseWindowsForms>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
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
// Disposable analysis probes against the Release bell-tower.dll built from the reviewed identity. In-memory only; no data dir.
var start = new DateTimeOffset(2026, 10, 26, 0, 0, 0, TimeSpan.Zero);
Observation Ev(string provider, EventKind kind, double s, string? op = null, string? fp = null, string id = "", bool causal = true) => new()
{
    Provider = provider, SessionId = "s", AttachmentId = "a", Kind = kind, At = start.AddSeconds(s), TurnId = null,
    OperationId = op, OperationFingerprint = fp, Id = id.Length > 0 ? id : kind + "-" + s, IngressAt = causal ? start.AddSeconds(s) : null
};
Observation ClaudePrompt(double s, string id = "prompt") => Ev("claude", EventKind.Permission, s, null, null, id) with { HumanPrompt = true, ProviderDelay = TimeSpan.FromSeconds(5) };
AlertPolicy Policy() => new(start.AddSeconds(-1));
string Due(DateTimeOffset? d) => d is { } v ? "+" + (v - start).TotalSeconds + "s" : "null";

// P1: new consumer, Claude permission_prompt produced by an older cached ingress image (no additive fields).
foreach (var newIngress in new[] { true, false })
{
    var state = new AppState(); var policy = Policy();
    SessionReducer.Apply(state, Ev("claude", EventKind.Prompt, 0));
    SessionReducer.Apply(state, Ev("claude", EventKind.ToolStart, 1, "t1", "bash"));
    SessionReducer.Apply(state, Ev("claude", EventKind.Permission, 1.1, null, "bash"));
    SessionReducer.Apply(state, newIngress ? ClaudePrompt(7.3) : Ev("claude", EventKind.Permission, 7.3, null, null, "prompt"));
    var s = state.Sessions.Values.Single();
    Console.WriteLine($"P1 newIngressShape={newIngress}: Reason={s.Reason?.ToString() ?? "null"} waits=[{string.Join(",", s.Waits.Select(w => w.Reason + ":" + w.Confirmation))}] alerts@120s={policy.Collect([s], start.AddSeconds(120), false).Count}");
}
// P2: Codex P1 natively confirmed + dismissed, then provisional P2; causal (capture) path vs unordered path.
foreach (var causal in new[] { true, false })
{
    var state = new AppState(); var policy = Policy();
    SessionReducer.Apply(state, Ev("codex", EventKind.Prompt, 0, causal: causal));
    SessionReducer.Apply(state, Ev("codex", EventKind.Permission, 1, null, "p1", causal: causal));
    var s = state.Sessions.Values.Single();
    s.NativePermissionPending = true; s.NativePermissionSince = start.AddSeconds(1);
    var first = policy.Collect([s], start.AddSeconds(7), false).Count;
    s.AcknowledgedEpisode = s.Episode;
    SessionReducer.Apply(state, Ev("codex", EventKind.Permission, 10, null, "p2", causal: causal));
    s = state.Sessions.Values.Single();
    s.NativePermissionPending = true; s.NativePermissionSince = start.AddSeconds(1);
    Console.WriteLine($"P2 causal={causal}: firstAlerts={first} dismissedWaitVisibleAgain={s.RequiresAttention(start.AddSeconds(16))} reannouncements={policy.Collect([s], start.AddSeconds(16), false).Count}");
}
// P3: Codex question pending, then a provisional permission confirmed by owning-runtime status; Claude attributed analog.
foreach (var provider in new[] { "codex", "claude" })
{
    var state = new AppState(); var policy = Policy();
    SessionReducer.Apply(state, Ev(provider, EventKind.Prompt, 0));
    SessionReducer.Apply(state, Ev(provider, EventKind.Question, 1, "q1", "qf"));
    var questionEpisode = state.Sessions.Values.Single().Episode;
    SessionReducer.Apply(state, Ev(provider, EventKind.Permission, 2, null, "pf"));
    if (provider == "claude")
    {
        SessionReducer.Apply(state, Ev("claude", EventKind.Permission, 8, null, "pf", "attributed-prompt") with { HumanPrompt = true, ProviderDelay = TimeSpan.FromSeconds(5) });
    }
    var s = state.Sessions.Values.Single();
    if (provider == "codex") { s.NativePermissionPending = true; s.NativePermissionSince = start.AddSeconds(2); }
    var visible = s.RequiresAttention(start.AddSeconds(20));
    var announced = policy.Collect([s], start.AddSeconds(20), false).Count;
    s.AcknowledgedEpisode = questionEpisode;
    Console.WriteLine($"P3 {provider}: Reason={s.Reason} ownEpisode={s.Episode != questionEpisode} NotificationDue={Due(s.NotificationDue)} visible={visible} announced={announced} visibleAfterQuestionDismissed={s.RequiresAttention(start.AddSeconds(20))}");
}
// P4: Claude P1 at 0.5 s, P2 at 5 s, unattributed prompt at 6.5 s (documented ~6 s gate: only P1 can be its source).
{
    var state = new AppState();
    SessionReducer.Apply(state, Ev("claude", EventKind.Prompt, 0));
    SessionReducer.Apply(state, Ev("claude", EventKind.Permission, 0.5, null, "p1"));
    SessionReducer.Apply(state, Ev("claude", EventKind.Permission, 5, null, "p2"));
    SessionReducer.Apply(state, ClaudePrompt(6.5));
    var s = state.Sessions.Values.Single();
    Console.WriteLine("P4: " + string.Join("; ", s.Waits.Select(w => $"{w.Fingerprint}={w.Confirmation},ambiguous={w.Ambiguous}")));
}
// P5: Claude P1 resolved at 6 s, P2 pending since 5.5 s, unattributed prompt at 6.4 s (within the 30 s receipt window).
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
    Console.WriteLine($"P5: receipts={s.ResolvedApprovals.Count} waits=[{string.Join(",", s.Waits.Select(w => w.Fingerprint + "=" + w.Confirmation))}] Reason={s.Reason?.ToString() ?? "null"}");
}
// P6: Claude question + uncorrelated permission, unattributed prompt, then the question is answered.
{
    var state = new AppState(); var policy = Policy();
    SessionReducer.Apply(state, Ev("claude", EventKind.Prompt, 0));
    SessionReducer.Apply(state, Ev("claude", EventKind.Question, 1, "q1", "qf"));
    SessionReducer.Apply(state, Ev("claude", EventKind.Permission, 1.2, null, "pf"));
    SessionReducer.Apply(state, ClaudePrompt(7.3));
    SessionReducer.Apply(state, Ev("claude", EventKind.ToolEnd, 20, "q1", "qf"));
    var s = state.Sessions.Values.Single();
    Console.WriteLine($"P6: afterQuestionAnswered Reason={s.Reason?.ToString() ?? "null"} waits=[{string.Join(",", s.Waits.Select(w => w.Reason + ":" + w.Confirmation + ":ambiguous=" + w.Ambiguous))}] alerts@120s={policy.Collect([s], start.AddSeconds(120), false).Count}");
}
// P7: question-tool approval hook without tool_use_id or tool_input while one question is pending.
{
    var state = new AppState();
    SessionReducer.Apply(state, Ev("claude", EventKind.Prompt, 0));
    SessionReducer.Apply(state, Ev("claude", EventKind.Question, 1, "q1", "qf"));
    var episode = state.Sessions.Values.Single().Episode;
    SessionReducer.Apply(state, Ev("claude", EventKind.Permission, 1.1, null, null, "approval") with { QuestionTool = true });
    var s = state.Sessions.Values.Single();
    Console.WriteLine($"P7: questionWaits={s.Waits.Count(w => w.Reason == AttentionReason.Question)} newEpisode={s.Episode != episode}");
}
```

## Output (`dotnet build -c Release` then run)

```text
P1 newIngressShape=True: Reason=Permission waits=[Permission:Confirmed] alerts@120s=1
P1 newIngressShape=False: Reason=null waits=[Permission:Provisional,Permission:Provisional] alerts@120s=0
P2 causal=True: firstAlerts=1 dismissedWaitVisibleAgain=True reannouncements=1
P2 causal=False: firstAlerts=1 dismissedWaitVisibleAgain=False reannouncements=0
P3 codex: Reason=Permission ownEpisode=False NotificationDue=null visible=True announced=0 visibleAfterQuestionDismissed=False
P3 claude: Reason=Permission ownEpisode=True NotificationDue=+8s visible=True announced=1 visibleAfterQuestionDismissed=True
P4: p1=Confirmed,ambiguous=True; p2=Confirmed,ambiguous=True
P5: receipts=1 waits=[p2=Confirmed] Reason=Permission
P6: afterQuestionAnswered Reason=null waits=[Permission:Provisional:ambiguous=True] alerts@120s=0
P7: questionWaits=2 newEpisode=True
```

## Reading

- P1: same sequence; only the shape of the `permission_prompt` observation differs. Without the additive `humanPrompt`/`providerDelay` fields (the shape an older cached ingress image produces), the notification becomes a second provisional permission and nothing alerts. See ANALYSIS-001.
- P2: Codex, causal path: a provisional request un-dismisses and re-announces an older natively confirmed wait; the unordered path does not. See ANALYSIS-002.
- P3: Codex native confirmation of a permission while a question owns the episode gives no announcement and is hidden when the question is dismissed; the Claude analog gets its own episode and announcement. See ANALYSIS-002.
- P4, P5: an unattributed prompt confirms requests that started less than the documented ~6 s gate before it, including within 30 s of a resolution. See ANALYSIS-003.
- P6: question + uncorrelated permission stays ambiguous; after the question is answered the permission never alerts. See ANALYSIS-003 (decision).
- P7: a question approval hook without correlation data creates a second question wait and a new episode. See ANALYSIS-007.
