# Implementation 020: fix cycle 1 for tasks 2.1 and 2.2

Developer assignment DEV-020, on top of [implementation-019](implementation-019.md). Baseline `2f1de7f`, uncommitted working tree. These changes address QA-1300, QA-1302, QA-1303, QA-1305, QA-1306, QA-1307 and ANALYSIS-600 to ANALYSIS-604 and ANALYSIS-606; they await independent verification and close nothing. QA-1301, QA-1304 and ANALYSIS-605 are deferred as instructed.

## Changes by finding

### QA-1300 / ANALYSIS-600: inherited launch metadata on a nested client

- One Core policy function, `NativeAdoption.Reconcile`, is called by `AttachmentStore.Adopt` under the attachment lock. The `IO` layer no longer holds the rule.
- When a managed identifier is carried by a different live agent (a nested client), Reconcile:
  - assigns the client's own `provider-pid-ticks` identity (`observed`, lifecycle `agent-process`);
  - clears `ManagedTitle` and `ManagedBackground`;
  - keeps a shell only when the new additive `HostTarget.ShellSource` is `ancestry`; a shell named only by an inherited `BELLTOWER_SHELL_PID` (`launch-env`) resets the host to unknown, which also drops the inherited window and terminal session.
- `AttachmentCapture` stamps `ShellSource` (`ancestry`, or `launch-env` when the variable names a different shell than the ancestry found).
- `CaptureConsumer` and `HookIngestor` suppress the inherited Name, Change and launch palette whenever the adopted attachment identifier differs from the captured one (nested re-identification or fail-closed fallback). A genuine managed launch is unchanged (tested for both providers, including a non-null presentation resolution).
- Tests assert, for Claude and Codex and for interactive, embedded and headless nested clients, that the stored record has no managed title or background and no launch-derived shell, that the identity does not inherit the parent's name or change, and that `TerminalPresentationResolver.Resolve` returns null.
- Navigation remains possible only through a shell verified from this client's ancestry (the host is kept for the `ancestry` rows).

### QA-1302 / QA-1306 / ANALYSIS-601: Codex classification fails closed

Codex 0.165.0 vocabulary (`src/BellTower/Providers/AgentModeClassifier.cs`):

- `app-server` anywhere before `--` is the shared backend, regardless of earlier unknown options.
- Known non-session subcommands (`mcp`, `mcp-server`, `exec-server`, `execpolicy`, `login`, `logout`, `apply`, `a`, `cloud`, `sandbox`, `debug`, `completion`, `features`, `plugin`, `proto`, `review`, `responses-api-proxy`, `stdio-to-uds`, `help`, `app`, `marketplace`, `update`) are `unknown`.
- Any option before the first positional that is neither a known flag nor a known value-taking option is `unknown`, because its value cannot be skipped safely. `--name=value` forms are recognized by name.
- Embedded runtime only for verified shapes: no subcommand with known TUI options, a positional prompt that is not a known subcommand name, `resume`, `fork`, and `--no-daemon`. `exec` and `e` are headless.
- The subcommand and flag lists come from the 0.165.0 help and docs as known to this change; only `app-server`, `exec`, `--no-daemon` and the TUI shape were exercised natively. New rows cover a value-taking option before `app-server`, an unknown flag, each non-session subcommand and the `--` boundary. Claude vocabulary is unchanged, with paired rows (an unknown option and `--` before `-p` stay interactive).

### QA-1307: managed launch with an unproven mode

- A capture with an unknown mode, a managed launch identifier and a hooked agent process becomes provisional (`Evidence.Provisional`). `Reconcile` keeps it direct, with its launch host and presentation, only while the recorded launch process is live and equals the hooked agent. Otherwise it fails closed to the shared runtime (no process, host or presentation; Name and Change suppressed).
- Applies to the providers that can share a backend (Codex). Claude with an unknown mode keeps its existing direct classification, so it never becomes provisional; the paired test shows both outcomes.
- Limitation: the launch record is matched through the hooked agent found by the `<provider>.exe` ancestor name. A renamed or wrapped image has no matching agent and fails closed.
- Compatibility note: a managed `claude -p` and a managed `codex exec` are now headless, so they get no host, title or background. A managed run of an interactive client keeps them.

### Folded suggestions

- QA-1303: `tools/BellTower.Smoke` and `tools/BellTower.Ingress.Bench` no longer set `BELLTOWER_DEDICATED`. Their hooks run under a process image named like the provider (a copy of `cmd.exe`) that runs the real command, so the benchmark includes the ancestry and command-line read. Both tools build; they were not run (they need installs, trays and a bench environment).
- QA-1305: replacement keeps unknown fields nested in `runtime` and `terminal` evidence as well as at the top level.
- ANALYSIS-602: the lifecycle owner is derived in one function, `AttachmentEvidence.LifecycleFor(ownership, hasProcess)`, used by capture, derivation and Reconcile. Merge learns an unknown owner from the capture with the same rule as the mode.
- ANALYSIS-603: evidence carries a schema `revision` and the `generation` (process identity) it describes. A mismatched stamp is treated as absent and `AttachmentEvidence.Of` derives the conservative view; `Adopt` re-stamps after each merge. `TerminalEvidence` reserves `client`, `shell`, `orderingKey` and `observedAt` for the 4.2 binder (nothing writes them). An old-tray round trip test (a record type without the property) shows all of these fields survive.
- ANALYSIS-604: the provider vocabulary and `MayShareBackend` moved to `src/BellTower/Providers/AgentModeClassifier.cs`. `NativeAdoption` (Core) no longer references a provider; the capture passes the provider fact in. `AttachmentEvidence` stays in the protocol library; `NativeAdoption` and the classifier are linked into the ingress instead.
- ANALYSIS-606: `AdoptionOutcome.Mode` is gone (the outcome is `NativeAdoption.Outcome`), the redundant ownership override in `HookIngestor` is removed, and `NativeAdoption.OwnId` is the single builder of the `provider-pid-ticks` identifier (text unchanged). `AgentLauncher` stamps managed-launch evidence with the launched process as its generation.

## Compatibility (task 2.4 precursor, updated)

No gate change. New persisted additions are all additive and ignored by an older tray: `evidence.revision`, `evidence.generation`, `evidence.provisional`, the reserved terminal fields and `host.shellSource`. The provisional record exists only inside a capture envelope and is resolved before anything is persisted. A cached older ingress still writes the legacy shape and keeps the ANALYSIS-500 weakness until refreshed.

## Checks

See the handoff for commands and counts. Builds ran with `global.json` temporarily at 10.0.114 (10.0.113 is not installed); it was restored with `git checkout -- global.json`.
