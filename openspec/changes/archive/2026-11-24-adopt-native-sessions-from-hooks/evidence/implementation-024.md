# Implementation 024: DEV-023 fix cycle 1 (test-only)

Developer assignment DEV-024 on top of [implementation-023](implementation-023.md). Baseline `b6c0b45`. Fixes for QA-1700 and ANALYSIS-900 to ANALYSIS-906, awaiting independent verification; nothing here closes a finding. No product code changed and no test exposed a product defect.

## QA-1700: git test isolation

- `Testing/GitHelper` is the single place that configures a test git process. `Run` and `RunWith` scrub `GIT_DIR`, `GIT_WORK_TREE`, `GIT_INDEX_FILE`, `GIT_COMMON_DIR`, `GIT_OBJECT_DIRECTORY`, `GIT_ALTERNATE_OBJECT_DIRECTORIES` and `GIT_NAMESPACE`, and set `GIT_CEILING_DIRECTORIES` to the parent of the test workspace root (`artifacts/unit-tests`).
- `IdentityPrecedenceTests` and `GitTests` had private copies of the helper; they now call `GitHelper.Run`. `ReviewIdentityScriptTests.Execute` applies `Isolate` (it keeps its own `GIT_CONFIG_*` isolation). `PackageScriptTests.Git` reads the real repository on purpose, so it only scrubs the selectors and gets no ceiling.
- `WorktreeFamily.Create` checks, before any commit, that `<dir>/.git` exists and that `rev-parse --show-toplevel` equals the family directory, and throws otherwise. `Commit` is now private.
- Regression `IO/GitIsolationTests`: a throwaway decoy repository inside the disposable workspace, with `GIT_DIR`, `GIT_WORK_TREE`, `GIT_INDEX_FILE`, `GIT_COMMON_DIR`, `GIT_OBJECT_DIRECTORY` and `GIT_NAMESPACE` pointing at it as an ambient environment, and a family created inside the decoy's work tree. It asserts the decoy keeps its head and one commit and the family is its own repository with its own commit. A second test asserts discovery from a plain workspace directory fails and the ceiling is the root's parent. The real repository is never referenced.
- Mutation check: with the scrub disabled, the decoy test failed (the `.git` guard refused to commit); restored afterwards.
- Not done: moving the test root to the system temp directory. Several tests locate the repository from the workspace and the guard plus ceiling already close the vector, so the root stays under `artifacts/unit-tests`.
- The root cause of the earlier leaked commit is still unconfirmed (QA-1700 states only a demonstrated vector).

## ANALYSIS-900, 904, 905, 906: real-process rows

`Hosts/NativeAgentResolveProcessTests` now puts the provider arguments before `/s /c` (cmd.exe tolerates leading tokens; the `{}` stdout and stored attachment are the self-check). Rows, each through both entry points (`event` and the fast-hook `bell-tower-ingress.exe`, drained with `StateStore`, via the new `BuiltApplication.Ingress`):

| Provider | Provider arguments | Expected mode |
| --- | --- | --- |
| claude | none, `-p`, `--print` | interactive-client, headless, headless |
| claude | `-- -p` | interactive-client |
| codex | none, `"fixture prompt"` | embedded-runtime |
| codex | `exec`, `e` | headless, no host claimed |
| codex | `app-server` | shared-backend, never claims the process |
| codex | `review`, `--future-option value` | unknown, shared runtime, no process claimed |

- Nested (ANALYSIS-905), through a batch file: Claude under a Codex image (`app-server` and embedded outer) claims only its own process, never the outer Codex; a Codex hook above a nested `claude.exe` image claims the nearest `codex.exe`. Both entry points.
- Mutation check: expecting `embedded-runtime` for the `exec` row failed both entries; restored. The embedded row can now fail because its marker is a real first token.
- Correction: the implementation-023 statement that Codex `exec` cannot be shaped through a `cmd.exe` image is superseded; ANALYSIS-900 measured it and these rows cover it.
- Each run starts a process-tree kill in a `finally`, and every assertion message carries exit code, stdout and stderr (ANALYSIS-906). Failures no longer leave `claude.exe` or `codex.exe` copies locking the workspace.
- Fixing `BuiltApplication.Ingress` replaced the hand-built path in `CaptureTests`. `HookMaintenanceTests.AddPayload` still builds its own Release path to copy payloads into a fixture layout, so it is unchanged.

## ANALYSIS-902, 903: native fixtures

`Fixtures/native` is now one file per provider, version and event: `claude-2.1.310-{sessionstart,userpromptsubmit}.json` and `codex-0.164.0-{sessionstart,userpromptsubmit}.json`, each with `provider`, `providerVersion`, `source`, `note`, `payload`, redacted `agentArguments`, `ancestorNames` and `expected {mode, dedicated, lifecycleOwner}`. All four are labelled `reconstructed`: from the retained real captures (Claude Code 2.1.310 and Codex 0.164.0) only the session ID, hook ancestry names and classification are real; the payload follows recorded shapes, and `agentArguments` is the print-mode form that classification implies (Claude) or null because no agent was found (Codex). The parallel `retained-hook-captures.json` was folded in and removed.

`Providers/NativeReplayTests` has one data-driven theory over the folder: the classifier reads `agentArguments`, the expected mode and dedicated outcome are asserted, and the payload replays through `HookIngestor` and the capture path with the same assertions (live conversation, no invented wait, health, lifecycle owner, process or shared ownership). Session IDs come from the fixtures. A fact checks provider coverage, file-name convention, provenance labels, and that arguments hold no path. A new fixture file needs no test edit. Mutation check: dropping `-p` from the Claude fixture failed the theory; restored. `Testing/NativeFixtures` is the shared loader, also used by the Resolve rows. The duplicated parallel cases are gone.

## ANALYSIS-901: shared sleeper

`Testing/DisposableSleeper` starts `ping.exe` directly with discarded output and kills the whole tree on dispose. `NativeAdoptionTests.Sleeper` was removed; `NativeReplayTests`, `NativeAdoptionTests` and `NativeAdoptionReconcileTests` use it.

## Checks

See the handoff for counts. Release build, full suite and format verification are recorded there.

## Limitations

- Payloads and the Claude `agentArguments` are reconstructions; a verbatim native envelope through product ingress is still unproven. A Codex fixture with a found agent does not exist yet because no retained capture has one; the Codex classifier vocabulary is exercised by the real-process rows.
- cmd.exe's tolerance of text before `/c` is observed behaviour, guarded by the rows' own `{}` and attachment assertions.
