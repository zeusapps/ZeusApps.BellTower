# Implementation 028: task 2.5 mode cross-check with process relationships

Developer assignment DEV-028 on top of `93e3534`. Awaiting independent review; nothing here closes a finding or ticks task 2.5.

## Rules and the evidence each rests on

One shared policy, `NativeAdoption.CrossCheck(mode, mayShareBackend, relations)`, applied by `AttachmentCapture.Resolve` between the existing command-line classification and `NativeAdoption.Apply`. It returns the given mode or `unknown`, never a stronger mode. Relations are the agent's direct same-image parent and direct same-image children (`ProcessProbe.Neighbors`, one Toolhelp snapshot, creation-time consistency check against pid reuse, at most 8 children and incomplete beyond that), each classified by the unchanged command-line classifier (unreadable means `unknown`). The provider adapter contributes only `MayShareBackend` (Codex true, Claude false). Only Codex pays the extra reads; a Claude hook does no extra work.

| Rule | Applies to | Evidence |
| --- | --- | --- |
| R1. A `shared-backend` needs a live same-provider parent whose own mode is an interactive/embedded client; otherwise `unknown`. | Codex | Native 009/010/011: hook ancestry is hook shell, backend, client that started the backend. Retained fixture `codex-0.164.0-*` ancestry shows `codex.exe` directly above `codex.exe`. Decision 7 text. |
| R2. An interactive/embedded client with a same-provider parent is `unknown` (a client sits under a shell or terminal host, a backend sits under a client). Catches a renamed backend subcommand started by a client. | Codex | Decision 7; same topology as R1. |
| R3. An interactive/embedded client with a same-provider child classified as backend or client is `unknown` (it acts as a backend or launcher). A headless or unknown child does not contradict. An incomplete child list (over 8) is `unknown`. | Codex | Decision 7 ("not the backend other clients connect to"). Clients connect over sockets, so only direct launched clients are visible; no socket enumeration was added. |
| R4. No relationship rule. | Claude | The only Claude retained evidence (claude-native-001, fixtures 2.1.310) is a headless `-p` agent under Codex and shells; it contains no contradiction pattern, and Claude has no shared backend. Matched rows assert that the same relationship shapes leave Claude unchanged. |
| R5. Relationships never upgrade: an unreadable agent command line stays `unknown`; null relations keep a client mode and fail a backend to `unknown`. | both | Assignment; unit rows. |

Unchanged: all `NativeAdoptionTests.Modes` command-line rows, retained fixtures, nested Claude-in-Codex and Codex-above-Claude rows. Disagreement only changes the mode; `unknown` takes the existing fail-closed shared-runtime path, so tracking, attention and notifications are untouched and only the process claim, host and presentation are withheld (design decisions 3 and 7).

## Changed files

- `src/BellTower/Core/NativeAdoption.cs`: `RelatedProcess`, `ProcessRelations`, `CrossCheck`.
- `src/BellTower/IO/ProcessProbe.cs`: `ProcessNeighbors` record, `Neighbors`, shared private `Snapshot` (`Ancestors` unchanged in behavior).
- `src/BellTower/Hosts/AttachmentCapture.cs`: `CrossChecked` called from `Resolve` (no csproj change; the Ingress project already links these three files).
- `tests/BellTower.Tests/Providers/ModeCrossCheckTests.cs` (new), `tests/BellTower.Tests/Hosts/NativeAgentRelationshipProcessTests.cs` (new), `tests/BellTower.Tests/Hosts/NativeAgentResolveProcessTests.cs` (helpers made internal; the harness-launched `app-server` row now expects `unknown`, and shared rows also assert the stored mode).

## Tests

- `ModeCrossCheckTests` (unit, fake trees): 24 policy rows for Codex and Claude (backend under client, shell/none, backend, headless, unknown parent; client with each parent and child shape, incomplete children; unjudged modes; Claude unchanged under the same shapes); null relations never strengthen; unreadable command line stays unknown for both providers; every `Modes` classifier row keeps its mode under ordinary relations; disagreement keeps a live, wait-free conversation and removes only the process/host claim (Claude stays direct).
- `NativeAgentRelationshipProcessTests` (real processes, stand-in images that are copies of `cmd.exe`, both `event` and `bell-tower-ingress.exe`): 16 nested parent rows (backend under client verified; under backend, headless, Claude or none unknown; embedded and renamed `serve` under a Codex process unknown; embedded under Claude and headless under Codex unchanged; Claude under Claude/Codex unchanged) and 8 held-child rows (agent with a same-image child kept alive beside the hook: backend or client child makes Codex `unknown`; headless child unchanged; Claude unchanged), plus a mixed simultaneous row (verified Codex backend, contradicted Codex client and a Claude client against one data folder, each judged alone). Held rows start the child detached and kill the tree after a done marker.
- Existing 2.2 real-process row: a `codex app-server` launched directly by the test process (non-client launcher) is now `unknown` by design; the verified shape is covered by the nested rows.

## Mutation checks (each restored afterwards)

| Mutation | Result |
| --- | --- |
| R1 removed (backend always accepted) | 13 failures |
| R1 parent mode check removed (`Client(...)` replaced by true) | 7 failures |
| R2 removed (parent ignored) | 15 failures |
| R3 removed (children ignored) | 11 failures |
| Incomplete-children check removed | 1 failure (unit only; no real-process row has over 8 children) |
| Provider gate removed (rules applied to Claude) | 3 failures |
| Neighbors queried with a non-matching image (no relations gathered) | 22 failures |

## Results

Release build: 0 warnings, 0 errors. Full suite once, nothing else running: 1376 passed, 2 failed (`StripRenderingTests.IncreasingTheInheritedFontRefitsTheChipAndItsStrip`, `HookMaintenanceTests.CleanupHashingDoesNotBlockGeneratedClaudeCodexOrMixedLegacyHooks`); both passed on rerun together (known load-sensitive tests, unrelated paths). `dotnet format ZeusApps.BellTower.slnx --no-restore --verify-no-changes`: clean. Build server shut down, no dotnet/testhost/stand-in processes left from this work.

## Limits and decisions needed

- A renamed backend started directly by a shell (no client parent) looks like an embedded client and keeps `embedded-runtime`; relationships cannot catch it without socket evidence (not added). The existing vocabulary row `codex serve` under a non-client launcher stays embedded.
- A genuine backend whose launching client already exited has no live parent and is `unknown` (same shared-runtime shape, only the diagnostic mode differs). A Bell Tower managed dedicated backend (`Start-Codex`) has a launcher parent, not a Codex client, so it is also `unknown` now; with a managed identifier it becomes a provisional candidate that Reconcile cannot verify for the backend (the launch record names the TUI), so it ends shared as before. Decision for the orchestrator: whether the Bell Tower launcher should count as an accepted backend parent.
- R2 assumes a native Codex client has no same-image launcher; none was observed (retained ancestry), but a re-exec wrapper would trip it and show up in the drift checks.
- Mode `unknown` from a disagreement carries no reason in the persisted evidence; adding one would change the persisted shape (task 2.4 gate), so diagnostics cannot yet say which rule fired. Proposed planning edit: a diagnostics task for a reason code, plus a capability matrix note that Claude has no relationship rule (no evidence).
- Not performed: any real `claude`/`codex` launch, socket enumeration, live-instance checks.

## Identity

`./scripts/review-identity.ps1 -Baseline 93e3534 -Paths <six code and test files above>`:
`head=93e3534 baseline=93e3534 files=6 diff=3145ed9aff3c untracked=2/2160868ac512`
(this evidence file is not part of that identity).
