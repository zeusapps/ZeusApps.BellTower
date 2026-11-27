> Saved verbatim by the orchestrator (Claude Code, session claude-release-gate-20261125) after the reviewer's write was refused.
> Reviewer: ANALYSIS-009 code-analyzer subagent; client Claude Code 2.1.332 (delegated subagent, role default); model claude-opus-5-5.
> Refusal message as reported by the reviewer: "Subagents should return findings as text". Known restriction: [Known client restrictions](../../../../docs/agent-workflow.md#known-client-restrictions).

# Analysis report: validate-next-release-field-acceptance / ANALYSIS-009

## Review target

- Reviewer / role: code-analyzer subagent for orchestrator session `claude-release-gate-20261125` / code-analyzer
- Client / version / mode / model (resolved ID): Claude Code 2.1.332, subagent, auto mode / `claude-opus-5-5`
- Date: 2026-11-25T16:22Z
- Change root / assignment: `openspec/changes/validate-next-release-field-acceptance` / ANALYSIS-009
- Task and requirement references: [tasks.md](../tasks.md) 2.2 and 2.3 (with reuse by 2.4 to 2.7); [release-readiness spec](../specs/release-readiness/spec.md) "Installed-package acceptance", "Provider parity", "Release provider capability matrix"; main specs `openspec/specs/standalone-setup/spec.md` (ingress contract) and `openspec/specs/session-identity/spec.md`
- Scope and relevant neighboring paths: `tools/Shared/HookTiming.cs`; `tools/BellTower.Ingress.Bench/**`; `tools/BellTower.FieldAcceptance/**` (new `Harness/SandboxGit.cs`, `TranscriptModel.cs`, `ConversationSurfaces.cs`, `CaptureObserver.cs`, `MachineLoad.cs`, `Scenarios/CandidateInstall.cs`, `FiveConversationsScenario.cs`, `PackagedHookBenchmarkScenario.cs`; edited `ProcessResult`, `AgentSession`, `Instance`, `ProviderModel`, `RunReport`, `RunContext`, `Program`); `tests/BellTower.Tests/FieldAcceptance/LoadScenarioTests.cs`; `docs/distribution-validation.md`; `docs/development.md`. Neighbors read: `src/BellTower/Core/NativeNamePolicy.cs`, `Core/AlertPolicy.cs`, `Core/CheckoutPolicy.cs`, `Sessions/IdentityHistoryReader.cs`, `Sessions/CodexNameService.cs`, `Diagnostics/CapabilityMatrix.cs`, `Setup/HookCommandBuilder.cs`, the archived `fix-codex-hook-command-shell` design and benchmark evidence, `docs/handoffs/next-release-plan.md`.
- Baseline commit: `02d4f56`
- Implementation identity (before): `./scripts/review-identity.ps1 -Baseline 02d4f56 -Paths Directory.Build.props README.md vscode src scripts docs tests tools ZeusApps.BellTower.slnx openspec/changes/validate-next-release-field-acceptance/evidence` printed HEAD `02d4f569cab34d2354d48ba846883065e8438223`, 8 modified and 56 untracked paths, tracked diff SHA-256 `e5222ec504dfc294c8a588d9f45398e52b477fcdf4276e07d26112a497550307`, summary `head=02d4f56 baseline=02d4f56 files=64 diff=e5222ec504df untracked=56/35edf1b0f27d`. Matches the assignment.
- Before/after identity: after the analysis the same command printed `head=02d4f56 baseline=02d4f56 files=64 diff=e5222ec504df untracked=56/35edf1b0f27d`. Unchanged; no unexpected mutation.
- Prior reports / rechecked findings: none rechecked. Deferred items that DEV-006 was asked to handle: ANALYSIS-107 (Bench reuse, thread safety) and ANALYSIS-142 (hooks through `AgentSession.Hook`). Both are assessed below as observations, not as rechecks.

## Analysis performed

| Area | Method / command | Evidence | Outcome / limitation |
| --- | --- | --- | --- |
| Bench behavior preserved | Read the `tools/BellTower.Ingress.Bench/Program.cs` diff against `tools/Shared/HookTiming.cs`; checked `BellTower.IO.Json.Encode` (reflection-based `JsonSerializer.Serialize` with shared options) | `Batches` yields the same sample numbers as the removed loop; `BaselineFirst` is the same expression; `Summarize` is the removed `Statistics` body with `Count` for `Length`. The records moved from file-local `internal` to `public` in namespace `BellTower.Tools` with identical property names, so the JSON field names do not change. No CI workflow builds or runs Bench | Preserved by inspection. Bench was not rerun, by DEV-006 or by me |
| Duplication: Bench and harness | Compared shell resolution, shell arguments, agent wrapper, payload and batch runner | Only the batch plan and the statistics are shared. Shell discovery, PowerShell arguments, the fake agent and the payload all differ | ANALYSIS-165 |
| Duplication: product code | Compared the harness surface checks with `AlertPolicy.Phrase`, `SessionPresentation.Title` and the `list` output | The speech check copies the product's subject-word rule; the tab and chip checks are true by construction | ANALYSIS-162 |
| Measurement: interval timed | Traced `Fire`, then `AgentSession.Hook`, then `FakeAgent.RunChild`, plus `CaptureObserver` | Round trip is the fake agent's process start of the shell through exit, plus one IPC line and a `last-attempt` read. In-agent time is the same interval measured inside the agent. Durable time runs from `Fire` start to the inbox name first being seen | Interval is sound and conservative. Gate choice: ANALYSIS-160 |
| Measurement: shell dominance | Measured PowerShell 7.6.10 startup on this machine: serial, interleaved, n=25 each, `-NoProfile -NonInteractive -Command`, outside the harness and repository | Medians: `echo 1` 283.4 ms; `Write-Output 1` 302.8 ms; `[Console]::Out.Write('1')` 231.8 ms; `& <System32>\whoami.exe /?` 228.9 ms. The harness's serial first Codex hook took 262.1 ms (harness-2.3) | The `echo 1` control is not neutral under PowerShell: ANALYSIS-161 |
| Measurement: gate versus contract | Read `openspec/specs/standalone-setup/spec.md:86-89`, the archived fix-codex design line 52, [tasks.md](../tasks.md) 2.3 and `PackagedHookBenchmarkScenario.cs:244-246` | The current ingress contract gates the adjusted mean. The task and the harness gate the raw median | ANALYSIS-160 |
| Percentile method | Compared `HookTiming.Percentile` with Bench | Same nearest-rank rule (`ceil(q*n)-1`) on the same code path. The mixed summary mislabels its first sample | Consistent. Label issue: ANALYSIS-166 |
| Concurrency in the benchmark | Read worker assignment (`sample % 8`), the `AgentSession` lock, `ConcurrentBag`, `LockedList` and `CaptureObserver` | One batch never puts two hooks on one worker. Records are thread-safe. Attribution and the observer can lose a race with the tray | Sound. Fail-closed race: ANALYSIS-167 |
| `Instance.WithProcessEnvironment` contract | Read `Instance.cs:54-56, 177-204` and the only caller, `UpgradeWithoutSetupScenario.cs:105` | The contract is documented but not enforced | ANALYSIS-171 |
| Codex native-name claim | Searched `src/` for `NativeTitle` writes and title harvesting; read `NativeNamePolicy.Pause`, `CodexNameService`, `CapabilityMatrix` and ledger F7 | Claim verified: only Claude `custom-title` records reach `NativeTitle` (`IdentityHistoryReader.cs:323-325`, `:227`). Codex `Pause` never promotes a name | Parity gap: ANALYSIS-164 |
| 2.2 checks against specs | Compared the branch-change rows of [harness-2.2](../evidence/harness-2.2.md) with `session-identity` spec lines 110-112 and 127-129 and with `CheckoutPolicy.Choose` | After a branch change, conversations collapse onto the launch checkout's surfaces, and the check excludes that case | ANALYSIS-163 |
| Architecture for 2.4 to 2.7 | Read the scenario structure, `ConversationSurfaces`, and the provider branches against the `docs/development.md` rule | Reusable pieces exist, but the fixture building and the surfaces reader are locked to one scenario and to the candidate build | ANALYSIS-170, -171 |
| Performance of 2.2 | Counted launcher invocations per poll; total run time 5 min 1 s from [harness-2.2](../evidence/harness-2.2.md) | `list` runs once per conversation per poll | ANALYSIS-169 (breakdown is a hypothesis) |
| Unit tests | `dotnet test tests/BellTower.Tests -c Release --no-build --no-restore --filter "FullyQualifiedName~FieldAcceptance"` | 78 passed, 0 failed | passed |
| Format | `dotnet format ZeusApps.BellTower.slnx --no-restore --verify-no-changes --include <18 changed .cs files>` | exit 0 | passed |
| Harness run, Bench run | Not run | QA owns functional runs. A harness run registers a real Task Scheduler task (ANALYSIS-101 history), and the build output may be in use by QA-009 | not performed (deliberate) |

## Findings

### ANALYSIS-160: The 2.3 latency gate is not the current ingress contract, and the Codex outcome flips with the metric

- Severity: warning
- Related task / requirement: 2.3; release-readiness "Packaged hook benchmark" ("satisfy the ingress contract"); standalone-setup "Baseline-adjusted hook runtime comparison"
- Locations:
  - `tools/BellTower.FieldAcceptance/Scenarios/PackagedHookBenchmarkScenario.cs:244-246`: the gate is the raw median below 300 ms.
  - `PackagedHookBenchmarkScenario.cs:162-172`: the mixed run has no control leg.
  - `openspec/specs/standalone-setup/spec.md:86-89`: the contract.
  - `openspec/changes/archive/2026-10-28-fix-codex-hook-command-shell/design.md:52`: the user's revision.
  - [tasks.md](../tasks.md) line 14.
  - `tools/BellTower.Ingress.Bench/Program.cs:118-120`: Bench applies the contract.
- Observation:
  - The main spec now applies the 300 ms comparison to mean(full hook) minus mean(`echo 1`) through the same shell. This was the user's explicit revision on 2026-10-28, after the raw Codex median failed (385 ms). The spec also says historical raw-median results are not relabeled.
  - Task 2.3 still says "median below 300 ms", wording carried over from the 2026-10-23 design. The harness implements that wording.
  - On the committed run:
    - Codex: raw median 354.3 ms fails. The contract estimate is 366.9 − 517.7 = −150.8 ms, a pass.
    - Claude: 142.6 − 100.5 = 42.1 ms, a pass.
    - Mixed: no estimate can be computed, because the mixed run has no control.
  - The release-readiness spec requires the contract. So the one failed check in [harness-2.3](../evidence/harness-2.3.md) may not be a contract failure, and the contract metric itself is not reported.
- Proposed improvement and benefit:
  - The orchestrator asks the user which gate 2.3 enforces:
    - the raw median (the task text), or
    - the contract's adjusted mean (the spec).
  - Then align task 2.3 and the harness to the answer.
  - Either way, the harness should report both metrics as separate, labeled checks. It should also give the mixed run a control leg in the same batches, so the contract metric exists for all three rows. That removes the ambiguity from the release decision.
- Behavior-preservation risks: Keep the raw median visible, so no historical raw result is relabeled. A negative estimate must stay visible (spec). The control leg must keep the same shell, session and input path.
- Evidence type: source observation; arithmetic from the committed run
- Verification / measurement plan: Rerun `packaged-hook-benchmark` after the change. The result must show, per provider and mixed, the raw median, the mean, the control mean and their difference, each with its own pass or fail.
- Relationship: new. Context for ANALYSIS-161 and -168.

### ANALYSIS-161: `echo 1` is not a neutral PowerShell baseline, so "dominated by PowerShell startup" is not shown by this control

- Severity: warning
- Related task / requirement: 2.3; standalone-setup baseline-adjusted comparison; ledger F1
- Locations:
  - `PackagedHookBenchmarkScenario.cs:140` (`const string control = "echo 1"`) and `:246`.
  - `tools/BellTower.FieldAcceptance/Harness/AgentSession.cs:95` (`-NoProfile -NonInteractive -Command`).
  - `tools/BellTower.Ingress.Bench/Program.cs:276-279, 307-312`.
  - `src/BellTower/Setup/HookCommandBuilder.cs:40` (the Codex hook is `& '<ingress>' --provider codex --data-dir '<data>'`).
  - [implementation-006](../evidence/implementation-006.md) item 1.
- Observation:
  - In PowerShell, `echo` is an alias of `Write-Output`, and resolving it auto-loads a module. The installed Codex hook only calls a native executable.
  - Measured on this machine (PowerShell 7.6.10, serial, interleaved, n=25 each):

    | Command | Median |
    | --- | --- |
    | `echo 1` | 283.4 ms |
    | `Write-Output 1` | 302.8 ms |
    | `[Console]::Out.Write('1')` | 231.8 ms |
    | a native `& whoami.exe /?` | 228.9 ms |

  - Under the benchmark's eight-way concurrency the gap widens: the control median was 511.9 ms against 354.3 ms for the hook. This gives the reported "−157.6 ms added".
  - A neutral comparison points the other way. The serial first Codex hook took 262.1 ms, against about 229 ms for PowerShell starting a native executable. That suggests roughly 30 to 35 ms of ingress cost on top of the shell. This is an estimate across two processes, not an isolated measurement.
  - So PowerShell startup very likely dominates, and the 300 ms raw gate mostly measures the shell. But the evidence DEV-006 cites (control slower than hook) is an artifact of the control.
  - The same bias makes the Codex contract estimate in Bench negative (archived `benchmark-mean-002`: 481 − 531 ms). A Codex regression in the ingress smaller than the module-load cost could never fail the contract gate.
  - The PowerShell arguments are also a modeling assumption that dominates the number:
    - The harness uses `-NoProfile -NonInteractive` and Bench adds `-NoLogo`.
    - The archived evidence pins Codex's shell selection but not its argument vector.
    - If Codex 0.164.0 does not pass `-NoProfile`, field hooks also pay for loading the user's profile.
- Proposed improvement and benefit:
  - Keep the user's literal `echo 1` leg, since the contract names it.
  - Add a third, informational leg that starts the shell and a native no-op the way the hook does. Examples: `[Console]::Out.Write('1')`, or a native executable that prints `1`. Report the hook's added time over that leg, so the "shell dominates" statement rests on a like-for-like control.
  - Record the Codex PowerShell argument vector as modeled and unverified in the 2.3 notes, with the closing evidence a source line or a probe.
- Behavior-preservation risks: An extra leg adds about 121 shell starts per provider (roughly 40 s for Codex). The new leg must be labeled so it cannot be mistaken for the contract baseline.
- Evidence type: measured result for the PowerShell timings, taken outside the harness; the attribution under concurrency is a hypothesis
- Verification / measurement plan: Interleave the three legs in the same batches and repeat over two runs. The neutral-leg median should sit near the hook median minus the ingress cost (tens of ms), and below the `echo 1` median.
- Relationship: new; refines DEV-006 item 1.

### ANALYSIS-162: Three of the four surface checks are true by construction or copy product code, and they are labeled as installed outputs

- Severity: warning
- Related task / requirement: 2.2 ("verify independent badge/tab/chip/speech snapshots"); release-readiness "Honest silent acceptance evidence"
- Locations:
  - `tools/BellTower.FieldAcceptance/Harness/ConversationSurfaces.cs:34`: `Read` returns null unless `snapshot.Title == row.Name`.
  - `ConversationSurfaces.cs:44, 49, 51`: `identity` is decoded from the same `list` row; the tab is `SessionPresentation.Title(snapshot.Title, …)`; speech is in-process `AlertPolicy.Phrase`.
  - `tools/BellTower.FieldAcceptance/Scenarios/FiveConversationsScenario.cs:315, 321, 325, 329`.
  - `src/BellTower/Core/AlertPolicy.cs:54-56`.
- Observation:
  - **Tab:** `Tab.StartsWith(Name)` follows from `Title(name, git)` once `Read` has required that the snapshot title equals the name.
  - **Chip:** `ChipName != Name` compares `identity["name"]` from the same `list` row with `row.Name`, which is the same field.
  - **Speech:** line 315 re-implements the product's subject rule (replace `-`, `_`, `:`; split; take `SpokenSubjectWords`) and compares it with `AlertPolicy.Phrase`, which runs the same rule in-process.
  - Only the badge (the installed `statusline` command) is an independent output of the installed candidate. The tab and speech come from the harness's own linked `BellTower.dll`, built from the working tree, yet the rows are labeled `InstalledHarness`.
  - Today the fixture candidate is built from the same tree, so the values agree. In 2.4, reading 0.1.9 data back after a rollback, these helpers would judge 0.1.9 state with candidate policy.
  - The speech independence test also passes trivially in one case. "Login flow review DEP-7104" is spoken as "Login flow review DEP, working" (four words), so it has no ticket number to collide on. Two conversations whose names differ only after the fourth word would sound identical, and no check compares spoken phrases across conversations.
- Proposed improvement and benefit:
  - Keep the badge check as it is.
  - Label tab, chip and speech as "product function over installed state (in-process, harness build)" in the check detail.
  - Replace the self-comparisons with cross-conversation checks:
    - distinct tabs among non-fallback conversations;
    - distinct chip details;
    - distinct spoken phrases, which would surface the four-word truncation as a real field outcome.
  - Drop the line 315 copy.
  - For 2.4, obtain these values from installed outputs (`list`, `doctor` or a CLI), or skip them with a reason for a 0.1.9 install.
- Behavior-preservation risks: A speech-distinctness check may fail on today's product policy (the four-word cap). That is a finding to triage, not a harness bug.
- Evidence type: source observation
- Verification / measurement plan:
  - Unit: feed `VerifySurfaces` two conversations named alike in the first four words and expect a failure.
  - Run: `five-conversations` reports the new checks.
- Relationship: new. Related to ANALYSIS-105 (self-fulfilling checks, deferred) and ANALYSIS-170 (version binding).

### ANALYSIS-163: After a branch change, conversations collapse onto the launch checkout's surfaces, and the 2.2 check defines this away

- Severity: warning
- Related task / requirement: 2.2 ("independent … snapshots through resume and branch changes"); ledger F9 and B; `openspec/specs/session-identity/spec.md` scenarios "The checkout changes branch" (lines 110-112) and "Ticket worktree differs from launch directory" (lines 127-129)
- Locations:
  - `FiveConversationsScenario.cs:348-355`: fallback conversations are excluded from distinctness.
  - `src/BellTower/Core/CheckoutPolicy.cs:19-37`: the working checkout comes only from a pin, provider evidence or work.
  - `CheckoutPolicy.cs:57-75`: a branch change clears the work.
  - [harness-2.2](../evidence/harness-2.2.md) rows 58-59, 75-76, 92-93.
- Observation:
  - After `git switch -c` in their own worktrees:
    - `codex-work` and `codex-reset` both read `[ main ]  branch: main`, with speech "main, working". In the mixed run, three conversations do.
    - The manual-name conversations keep their names but show `branch: main` with no worktree, although their work continues in a ticket worktree now on `DEP-…-hotfix`.
  - The cause: invalidating the work also removes the only basis for the working checkout, so `Choose` returns the launch fallback.
  - The spec says that when the checkout changes branch, "the displayed Git context refreshes" (to the new branch). It also says a ticket worktree's branch and worktree are shown even when the process launched in the main checkout.
  - Field finding F9 is exactly this situation. The harness records it as a pass by excluding provenance `fallback` from distinctness. DEV-006 judged it "the documented last tier" without a triage decision.
- Proposed improvement and benefit:
  - The orchestrator triages the observed behavior against the two spec scenarios and F9: is it an accepted fallback or a product defect?
  - Until then, the check should report the collapse as failed or expected-failure with this reason, not pass. The run summary would then show it.
  - If it is accepted, record the user's decision in the ledger F9 row.
- Behavior-preservation risks: None for the harness. A product change (for example, keeping the last verified checkout across a branch change of that same checkout) would touch the shared checkout policy for both providers and needs its own change.
- Evidence type: measured result (committed run) plus source observation
- Verification / measurement plan: After triage, rerun `five-conversations` and confirm the check outcome matches the decision for Claude, Codex and mixed.
- Relationship: new. DEV-006 finding 3 raised it but did not escalate it.

### ANALYSIS-164: Codex explicit native names are an unfinished parity gap the release gate must list; the ledger and the 2.2 summary understate it

- Severity: warning
- Related task / requirement: 2.2, 4.3, 5.2, 5.3, 6.3; ledger F7; release-readiness "Only one provider has passed"; AGENTS.md parity rule
- Locations:
  - `src/BellTower/Core/NativeNamePolicy.cs:32-40`: `Pause` never sets `NativeTitle`.
  - `src/BellTower/Sessions/IdentityHistoryReader.cs:323-325` and `:227`: only Claude `custom-title` records are harvested.
  - `src/BellTower/Sessions/CodexNameService.cs:57-62`.
  - `src/BellTower/Diagnostics/CapabilityMatrix.cs:62-64`: the Codex row is `Open`.
  - [coverage-ledger](../evidence/coverage-ledger.md) row F7.
  - `FiveConversationsScenario.cs:54, 169-170`.
  - `docs/handoffs/next-release-plan.md:126, 137`.
- Observation:
  - DEV-006's claim is correct. No path in `src/` promotes a Codex name set outside Bell Tower to the explicit-native-title tier. Only `null` assignments exist besides the Claude harvest. A Codex name set outside Bell Tower only pauses synchronization.
  - The shared precedence in the handoff (tier 2) and its line 137 require adapters for both clients.
  - The product's capability matrix states the gap honestly (`Open`, with the closing evidence named). So it is unfinished or unverified work, not a proven restriction.
  - The release evidence understates it in three places:
    - Ledger F7 says "X: fixture (exact-conversation native names)".
    - The `codex/native-and-reset` check asserts `NativeTitle == ""` and passes, which pins the gap as expected behavior.
    - The headline is "63 of 63 PASSED", with the gap only in the notes.
  - No change owns the read side. The deferred `sync-native-session-titles` covers writing.
  - The matched fixtures are not matched for this role either. The Codex "reset" conversation exercises a weaker path: it falls back to its work and then to `main` after the branch change.
- Proposed improvement and benefit:
  - The orchestrator adds an explicit gap row to the ledger (F7) and to the 5.3 matrix: "Codex explicit native title as tier 2: unfinished; closing evidence a supported read with name provenance, or a version-scoped probe proving none exists".
  - Ask the user to choose: implement, prove a restriction, or revise the release scope.
  - Give the harness a way to record a not-performed or unfinished parity item as its own check outcome that the summary counts, instead of a passing assertion.
- Behavior-preservation risks: None for the product. Changing the check outcome changes the 2.2 headline counts, which is intended.
- Evidence type: source observation
- Verification / measurement plan: Search `src/` for `NativeTitle` writers (done: three, two of them `null`). After the ledger update, verify F7 X no longer claims coverage of native names, and that the 2.2 summary shows the Codex native-title item as unfinished.
- Relationship: new; confirms DEV-006 finding 2. Related to task 6.4's existing ANALYSIS-300 pattern (gap retained, not waived).

### ANALYSIS-165: Bench and the harness share only the batch plan and the statistics; their numbers are not comparable, contrary to the docs

- Severity: suggestion
- Related task / requirement: 2.3; ANALYSIS-107 (deferred reuse); ledger F1
- Locations:
  - `tools/BellTower.Ingress.Bench/Program.cs:214-256` (own Git Bash resolution, and a Codex PowerShell order with the fixed `pwsh` first), `:258-282` (`-NoLogo -NoProfile -NonInteractive`), `:284-305` (the agent is a `cmd.exe` copy wrapping the shell), `:335-341` (synthetic `Stop` payload), `:70-105` (batch runner).
  - `tools/BellTower.FieldAcceptance/Harness/CodexShell.cs`, which follows the `rust-v0.164.0` order with `PATH` first.
  - `PackagedHookBenchmarkScenario.cs:137-160`: a second copy of the alternating batch runner.
  - `docs/development.md:65` ("the hook timing sampling and statistics live in one shared source").
  - `tools/Shared/HookTiming.cs:4-10`: public types compiled into two assemblies.
- Observation:
  - The measured interval differs between the two tools:
    - Bench's agent is `cmd.exe /d /s /c <shell …>`, so every sample includes a cmd startup.
    - The harness's agent is a resident .NET process.
  - The harness's Claude native median is 140.8 ms. Bench's archived run gave 204 ms for the same command shape, a gap of about the cmd wrapper.
  - Codex shell selection can differ when `PATH` holds another `pwsh`, which is the residual ANALYSIS-104 fixed only in the harness.
  - The docs sentence is true only for the sampling plan and the percentile rule. Ledger F1 cites both tools as one benchmark line.
  - Linking public types into two assemblies (`BellTower.Tools.TimingSample` in both Bench and FieldAcceptance) gives CS0433 ambiguity for any project that ever references both.
- Proposed improvement and benefit:
  - Move `CodexShell` and the Git Bash resolution into `tools/Shared` and use them from Bench. That gives one shell policy for both tools.
  - Make the linked types `internal`, so each assembly gets a private copy.
  - Optionally share the batch runner as a delegate-taking helper.
  - If Bench keeps its cmd wrapper on purpose, state in `docs/distribution-validation.md` and ledger F1 that the two tools time different intervals.
- Behavior-preservation risks:
  - Changing Bench's Codex shell order changes which PowerShell it times on machines with a `pwsh` on `PATH`. Record it as a deliberate change.
  - `internal` types need `InternalsVisibleTo` for `BellTower.Tests`.
- Evidence type: source observation; the cmd-wrapper share of the 64 ms gap is a hypothesis
- Verification / measurement plan: Run Bench once after the move. Its JSON field names must be unchanged, and its Codex `Shell` must be the one `CodexShell.Resolve` returns.
- Relationship: residual of ANALYSIS-107

### ANALYSIS-166: The mixed run's "first (serial)" value is a concurrent sample

- Severity: suggestion
- Related task / requirement: 2.3 (tail latency reported honestly)
- Locations:
  - `PackagedHookBenchmarkScenario.cs:162-172`: no serial sample in `RunMixed`.
  - `PackagedHookBenchmarkScenario.cs:223-224, 233`: `Summarize` is called with the fixed `ParallelSamples`.
  - `tools/Shared/HookTiming.cs:36-39`: the first sample is always `measured[0]`.
  - [harness-2.3](../evidence/harness-2.3.md) line 56: "first (serial) 158.1 ms".
- Observation:
  - The mixed batches start at sample 1, so `FirstSampleMilliseconds` is a concurrent hook. The detail still calls it serial.
  - `FirstSampleCount = 1` and `ParallelSamples = 120` are constants regardless of the subset (durable subsets included).
- Proposed improvement and benefit:
  - Pass the presence of a serial first sample into `Summarize` (Bench passes true), or omit the field for mixed.
  - Derive `ParallelSamples` from the data.
- Behavior-preservation risks: Bench output must stay byte-identical, so keep the default for Bench.
- Evidence type: source observation
- Verification / measurement plan: Unit test: a mixed-style sample set has no serial label.
- Relationship: new

### ANALYSIS-167: Capture attribution races the tray's deletion, so durable and count gates can fail while nothing was lost

- Severity: suggestion
- Related task / requirement: 2.3 (exact counts, durable completion)
- Locations:
  - `tools/BellTower.FieldAcceptance/Harness/CaptureObserver.cs:186-199`: the file is opened after the event; on failure, provider and session are null.
  - `PackagedHookBenchmarkScenario.cs:197-215`: matching needs `SessionId`.
  - `PackagedHookBenchmarkScenario.cs:256-260`: any `unknown` capture fails `event-counts-exact`.
- Observation:
  - The tray deletes a committed capture within one 250 ms tick. If the observer's handler opens the file after that (thread-pool delay under eight concurrent shells, or an antivirus scan holding the file), the capture has no session.
  - It then matches no hook, so `durable-within-3s` fails on `matched.Count`, and `event-counts-exact` fails on `unknown`, although every envelope was delivered.
  - This fails closed, but it would turn a slow machine, which is the field case, into a spurious harness failure.
- Proposed improvement and benefit:
  - Attribute captures by envelope id through the applied session journals. `AppliedIds` already reads per-session `CausalEvents`, so keep the session key there.
  - Keep the observer only for the first-seen time, which needs no file read.
- Behavior-preservation risks: Attribution would then depend on delivery. A capture that is durable but never delivered must still fail the counts check, as today.
- Evidence type: source observation; the frequency is a hypothesis (0 unknown in the committed runs)
- Verification / measurement plan: Unit test with an observer fed names only (no readable file). Attribution via journal ids still matches all hooks.
- Relationship: new

### ANALYSIS-168: One idle-machine run cannot settle a gate whose margin is smaller than the run-to-run spread

- Severity: suggestion
- Related task / requirement: 2.3 ("under representative load"); archived speed-up design ("representative loaded setup")
- Locations:
  - `tools/BellTower.FieldAcceptance/Harness/MachineLoad.cs:247-248` (`idle` below 20%).
  - [harness-2.3](../evidence/harness-2.3.md) lines 51 and 69.
  - [implementation-006](../evidence/implementation-006.md) item 1: Codex medians 359.9, 354.3 and 283.9 ms over three runs.
- Observation:
  - The Codex median moved by 76 ms between runs, more than its 54 ms distance from the gate.
  - The "load" is only the harness's own eight concurrent hooks, on a machine classified idle (6 to 10% busy).
  - The task does not define representative load, and the harness records one run as the outcome.
- Proposed improvement and benefit:
  - Run the benchmark k times, for example three, and report each run, or require the gate in every run.
  - State "idle machine, harness load only" in the check detail, or add an optional background CPU load profile, so the evidence does not imply field representativeness.
- Behavior-preservation risks: Run time grows about 1 minute per repetition.
- Evidence type: measured result (DEV-006's three runs); the cause of the spread is a hypothesis
- Verification / measurement plan: Three back-to-back runs. Record the spread of the medians next to the gate.
- Relationship: related to ANALYSIS-160

### ANALYSIS-169: `five-conversations` starts a `list` process per conversation per poll

- Severity: suggestion
- Related task / requirement: 2.2; reuse by 2.4, 2.7 and 6.3
- Locations:
  - `tools/BellTower.FieldAcceptance/Harness/ConversationSurfaces.cs:86`: `Identity` runs a full `list`.
  - `FiveConversationsScenario.cs:122-124` (one `list` per conversation each 300 ms poll, then three more per conversation), `:132`, `:157`, `:209`.
  - `ConversationSurfaces.cs:58-83`: one `list` plus N `statusline` every 1.5 s, two agreeing reads required.
  - `FiveConversationsScenario.cs:224`: fixed 8 s sleep.
- Observation:
  - In the mixed group each poll of the waits starts 10 launcher processes, and `recoveredRows` starts 30.
  - The committed 2.2 run took 5 min 1 s. The share caused by these reads is not measured.
- Proposed improvement and benefit: Read `list` once per poll and index it by key. Replace the fixed sleep with a bounded wait on the history pass.
- Behavior-preservation risks: Snapshot semantics are unchanged; fewer reads mean slightly coarser settling, so keep the two-read agreement.
- Evidence type: source observation; time saved is a hypothesis
- Verification / measurement plan: Compare `startedAt` and `finishedAt` of two runs before and two after the change, with the stored per-command elapsed times.
- Relationship: related to ANALYSIS-108 (fixed waits, deferred)

### ANALYSIS-170: The 2.2 fixture and surfaces reader are not yet reusable by 2.4, 2.7 and 6.3

- Severity: suggestion
- Related task / requirement: 2.4, 2.7, 6.3; `docs/development.md:65` (no provider-name branches in new scenarios)
- Locations:
  - `FiveConversationsScenario.cs:54` (`HasNativeTitle` tests `Model.Name`), `:68-229` (one 160-line `RunGroup` doing fixture building, controls and checks), `:360-381` (`WriteTranscript` branches on provider).
  - `PackagedHookBenchmarkScenario.cs:218-220` (`SessionOf` branches on provider).
  - `ConversationSurfaces.cs:33, 44`: candidate-schema `IdentityPresentationStore` and `SessionIdentity` decoded in-process.
- Observation:
  - 2.4 needs the same conversations through tray downtime and a rollback to 0.1.9. 6.3 needs the same five-conversation layout.
  - The worktree, transcript and change-folder fixture lives inside `RunGroup`.
  - The transcript writer is a provider branch rather than a `ProviderModel` member, which contradicts the rule DEV-006 added to `docs/development.md`.
  - The surfaces reader reads the candidate's presentation store and identity schema in-process. Against a 0.1.9 install it would return null, if 0.1.9 has no such store, or decode with the candidate's types, so `ReadSettled` would time out with a partial result.
- Proposed improvement and benefit:
  - Extract a `ConversationFixture`: worktrees, change folders, transcripts and sessions, plus per-provider transcript records behind `ProviderModel`.
  - Keep `RunGroup` to controls and checks.
  - Make the surfaces reader declare which installed version it supports, and fail with a clear message otherwise.
- Behavior-preservation risks: None to behavior if the extraction keeps the same session ids and numbering.
- Evidence type: source observation
- Verification / measurement plan: `five-conversations` gives identical check ids and details before and after the extraction.
- Relationship: related to ANALYSIS-162

### ANALYSIS-171: `WithProcessEnvironment` is single-threaded by comment only, and the benchmark changes the global thread pool

- Severity: suggestion
- Related task / requirement: 2.4 (upgrade or rollback with hooks), 2.7; ANALYSIS-107 thread-safety residual
- Locations:
  - `tools/BellTower.FieldAcceptance/Harness/Instance.cs:171-204`.
  - `Instance.cs:54-56`: `Instance.Create` reads the process environment.
  - `tools/BellTower.FieldAcceptance/Scenarios/UpgradeWithoutSetupScenario.cs:105-108`.
  - `PackagedHookBenchmarkScenario.cs:45`.
- Observation:
  - Harness-started processes are immune to the swap: `Instance.Run` and `AgentSession.Start` clear and set the child environment, and `AgentSession.Hook` only writes to an existing agent.
  - Two paths are not immune:
    - `Instance.Create` run on another thread during a swap builds its child environment from another sandbox's variables.
    - A variable the in-process product call creates survives the restore, because only the saved and instance keys are reset.
  - Nothing detects either case. A 2.4 scenario that runs a rollback while a parallel load or a second instance is being prepared would break the contract silently.
  - `ThreadPool.SetMinThreads` is set by one scenario and never restored, so later scenarios in `--scenario all` inherit it.
- Proposed improvement and benefit:
  - Add a process-wide flag set inside `WithProcessEnvironment`. `Instance.Create`, `Instance.Run` and `AgentSession.Start` throw while it is set and the caller is on another thread.
  - Alternatively, run the in-process update in a helper child process with an explicit environment, which removes the swap.
  - Restore the thread-pool minimums in a `finally`.
- Behavior-preservation risks: The flag must not block the action's own thread (the update transaction starts the tray from that thread).
- Evidence type: source observation
- Verification / measurement plan: Unit test: calling `Instance.Run` from a second thread during `WithProcessEnvironment` throws.
- Relationship: residual of ANALYSIS-107

## Re-verification

None assigned. ANALYSIS-142 was observed, not rechecked:
- The load and five-conversation scenarios run every hook through `AgentSession.Hook` (`PackagedHookBenchmarkScenario.cs:131`, `FiveConversationsScenario.cs:386, 392`).
- The contract is documented in `Instance.cs:86-89` and `AgentSession.cs` (the `Hook` summary).
- `Instance.Run` does not itself observe hook starts, so the rule is still a convention.

## Assessment and limitations

Scope covered:
- the shared timing source and Bench (behavior preserved by inspection);
- both new scenarios and their harness pieces;
- the measured interval and the gates against the current ingress contract;
- concurrency and the environment-swap contract;
- the Codex native-name claim (verified);
- the docs sentences DEV-006 added.

Clients covered: Claude Code and Codex rows of 2.2 and 2.3, and the mixed rows, by source and committed evidence. No live provider check was run.

Decisions needed (for the orchestrator and the user):
1. ANALYSIS-160: which latency gate 2.3 enforces, the task's raw median or the contract's adjusted mean. Under the contract metric the committed Codex result is a pass (−150.8 ms); under the task text it is a failure (354.3 ms).
2. ANALYSIS-163: whether the post-branch-change collapse onto the launch checkout is the accepted fallback or a product defect against the `session-identity` branch-change and ticket-worktree scenarios (F9).
3. ANALYSIS-164: the release-scope treatment of Codex explicit native names (implement, prove a restriction, or revise the scope). The ledger F7 row should stop claiming Codex fixture coverage of native names.

Measurements:
- The PowerShell startup timings in ANALYSIS-161 were taken outside the harness and the repository, on this machine, serial (no concurrency).
- All other numbers come from DEV-006's committed runs.
- No harness or Bench run was performed. QA owns functional runs, and a harness run touches the real Task Scheduler.

Checks: FieldAcceptance unit tests 78 of 78 passed (`--no-build`); format verification exit 0 on the 18 changed C# files.

Writes and identity: the one write attempt of this report was refused by the client ("Subagents should return findings as text"), so the report is returned inline for the orchestrator to save. No repository file was written. The review identity is unchanged before and after. No production installation, data directory, `~/.claude`, `~/.codex` or tray was touched. The orchestrator owns triage and ledger status.
