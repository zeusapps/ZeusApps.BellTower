# Durable hook ingress

The packaged Windows x64 integration uses `bell-tower-ingress.exe`, a roughly 3 MB NativeAOT executable. Both providers use this capture path. It reads bounded JSON, captures process and launch evidence, writes one durable envelope, returns `{}` and exits. Git queries, identity writes, selection effects and shared state locks run in the consumer. Capturing does not start the tray, launcher, desktop application or a provider.

Development executables retain the legacy normalized `event` command for component checks and old helpers. A versioned installation requires a verified native helper before fast setup can proceed; packaging fails if native publication fails.

## Capture-v1 contract

`BellTower.Protocol` owns the shared DTOs, parser, validation, source-generated serializer and atomic spool. It targets net10.0 without Windows Desktop references. Native process inspection is linked separately into the Windows ingress executable.

| Field | Meaning |
| --- | --- |
| `protocol` | Exactly 1; incompatible versions are rejected |
| `id` | Unique N-format GUID, also used as the normalized observation ID |
| `ingressAt` | Capture start in UTC, independent of consumer processing time |
| `observation` | Provider, native session/turn/tool IDs, registration source, event kind, directory, argument fingerprint, main/subagent distinction and bounded background task IDs/types |
| `observation.at` | Eligible hook-shell creation time, otherwise ingress time |
| `observation.hookShellStartedAt`, `orderConfidence` | Available ordering evidence; `hook-shell` or `ingress-only`, never an invented provider timestamp |
| `observation.processedAt` | Added by the consumer; never used as event source order |
| `attachment`, `hookShell`, `ancestors` | Available process identities with PID and creation ticks; host and approved presentation ownership flags |
| `dedicated`, `name`, `change`, `role` | Approved launch metadata; shared/remote Codex keeps its deliberately unverified attachment policy |
| `transcriptPath` | Location only; capture never reads or stores transcript contents |
| `selection`, `selectionRoot`, `selectionPre`, `createdScaffold` | Bounded derived selection evidence; local scaffold confirmation is deferred |

Input is at most 1 MiB of strict UTF-8, allowing a UTF-8 BOM. Envelope output is at most 128 KiB, with at most 32 ancestors and 128 background tasks; JSON depth is 32. Session/attachment IDs are at most 200 characters, tool/turn IDs 256, and paths 32768. Tool arguments enter only a SHA-256 fingerprint or an accepted bounded selection candidate. Commands, tool responses, prompts, assistant messages and credentials, including BELLTOWER_CODEX_TOKEN, are excluded.

The only executable probe is `--protocol-info`. It returns `{"protocol":1,"minimumConsumer":1,"maximumConsumer":1}` and writes no data. Envelope and observation extension fields survive supported round trips. Shared identity, session, attachment and integration ownership records retain their declared extension fields.

## Durability and bounded replay

The helper writes a unique temporary file with write-through and flushes it before an atomic rename into `captures-v1/<utc-ticks>-<id>.json`. Failed writes clean up their temporary file. Rejection diagnostics use one bounded timestamp slot per sanitized category, without shared capture locks or exception messages.

The consumer takes at most 128 captures per drain. The legacy normalized inbox remains readable alongside it. Valid receipt identities are saved with state before deleting their files; unfinished commits leave valid captures for retry. Capture receipt retention checks the known receipt filenames without enumerating an entire capture backlog. Invalid payloads are discarded with a sanitized category; IO/access failures retain valid captures for retry.

Session replay keeps 256 ordered observations and a committed checkpoint per conversation. Ordering uses the captured event time, event kind and envelope ID. Older evidence behind the committed floor is stale and cannot reopen state. An ended capture attachment requires a newer explicit resume registration to reopen; delayed startup/unknown registrations cannot reopen it. Selection retains 128 compact derived evidence records; both legacy and capture adapters use the same selection policy. Successful unpaired results can select, while known newer starts suppress an older completion. No raw tool payload is retained in these journals. Snapshot writes enforce the existing 16 MiB read budget and retain pending captures if that budget is exceeded.

Process-clock evidence does not reconstruct arbitrary source ordering when the provider supplies none. Diagnostics and envelopes retain this uncertainty. Tests shuffle fixed source-evidence fixtures, including results before starts, old registrations after ends, and mixed providers. Viewed/acknowledged episode IDs and manual naming/palette overrides remain stable.

## Setup, migration and compatibility

Setup verifies the helper against the selected payload inventory and provisions immutable bytes at `integrations/provider-ingress/1/<sha256>/bell-tower-ingress.exe` under the chosen data directory. Existing images are retained for cached hooks. Hooks invoke that image directly: Bash quoting for Claude, Windows command-shell quoting for Codex. Spaces, apostrophes and Unicode are exercised through actual shell invocations.

Async is enabled for regular capture hooks. SessionEnd uses fast synchronous capture for both providers. Native provider timeout/cancellation behavior remains outside Bell Tower's ownership; async does not establish durability or latency by itself.

Ownership compares the full handler and group metadata. Ordinary setup retains edited handlers and offers `--migrate-edited-hooks`. Preview shows old/new handlers; adding `--apply` writes backups and replaces edited owned entries explicitly. Unrelated handlers remain. Migration can be repeated without duplicates.

`release.json` advertises `capture: 1`; absent means legacy-only. `capture-protocol.json` records enabled capture. Setup refuses to enable fast hooks under a running tray without a capture-v1 readiness receipt. The current launcher and updater reject incompatible tray selection/rollback while preserving legacy normalized event writers. Compatible rollback means another capture-v1 consumer, not an arbitrary old desktop executable. An old bootstrap that predates this guard cannot enforce it for manual pointer edits; do not use manual edits to select a legacy consumer after fast hooks are enabled.

Doctor reports capped backlog counts, oldest pending age, latest consumption lag, sanitized rejection categories and unverified active process attachments. `doctor --transcript <file> --transcript-provider claude` optionally scans at most 1 MiB, 2000 complete lines and 64 KiB per line. The verified shape is `type: attachment`, `attachment.type: hook_cancelled`, boolean `timedOut`, positive integer `timeoutMs` and a record timestamp. It reports only counts and time bounds. Private content, commands and exception messages are excluded. Codex 0.164.0 local exec/terminal transcripts do not retain the tested SessionStart timeout status; the reader reports complete counts unavailable and uses capture health/native status. Other version/mode pairs remain unverified. This restriction applies only to retrospective transcript counts, with [exact proof and regression coverage](../openspec/changes/archive/2026-10-23-speed-up-claude-hook-ingestion/evidence/provider-timeout-probes.md).

## Build and evidence

Package builds require PowerShell 7, .NET 10 and the Windows MSVC/SDK NativeAOT prerequisites. The desktop shell integration still supports Windows PowerShell 5.1. Run `pwsh -File scripts/package.ps1`, then the isolated `tools/BellTower.Ingress.Bench` against that package. It validates generated commands, neutral output, durable counts, latency and crash replay; errors produce a nonzero result rather than an unhandled Windows dialog.

On 2026-10-23, 121 native captures per provider ran at concurrency eight with spaces/apostrophes/Unicode in the selected data path. The first valid run persisted all 242 native captures and 50 legacy controls and restored eight independent sessions. Native Claude median/p95/p99/max: 115.12/131.37/139.80/140.63 ms. Native Codex: 83.19/89.44/102.40/111.53 ms. Legacy-chain median: 1535.03 ms Claude and 1502.01 ms Codex (25 samples each). Every native sample finished below three seconds. These are local generated-shell trials, not authenticated hooks or a reproduction of the field machine's reported timeout rate. The first capture occurs after the provisioning probe; no OS file-cache reset is claimed.

Retained raw results: `artifacts/hook-ingress/trial-03/results.json`. The benchmark initially failed its strict stderr check because Windows PowerShell wrote first-use progress CLIXML in the legacy control. That failed run is not a performance pass. Native ingress must remain silent; legacy controls must still return neutral output and persist every event.

Provider references and fixture provenance are retained in the change's [contract inventory](../openspec/changes/archive/2026-10-23-speed-up-claude-hook-ingestion/evidence/contract-inventory.md). Missing authenticated trials or transcript schemas remain open evidence, not provider restrictions.

The isolated packaged lifecycle trial at `artifacts/update-acceptance/run-5422462fc6d943a3b06c8e6437082182` also passed. Current source was compiled as a 0.1.7 baseline fixture and 0.1.8 candidate; this is not a test of the historical 0.1.7 release. Mixed captures survived monitoring restart, selection change, helper refresh while the cached old image remained callable, stopped-tray activation and readiness-failure rollback. The existing disposable provider/runtime remained alive, native-name ownership and palette/viewed receipts remained intact, and no real provider session was stopped.

Final rebuilt binaries were rechecked with the benchmark and lifecycle trial running concurrently. `artifacts/hook-ingress/trial-final/results.json` retained all 242 captures: native Claude median/p95/p99/max 122.77/136.62/152.65/153.55 ms, native Codex 89.63/107.22/112.78/113.89 ms. The final lifecycle run `artifacts/update-acceptance/run-d02076a966e947dc98ad3a97f4d3f65b` passed. Full verification: 332 .NET tests, eight bridge tests, zero-warning Release build, formatting and strict OpenSpec validation.
