# Implementation 001 (DEV-001): tasks 1.1 to 1.4

Developer handoff, 2026-11-25, baseline `02d4f56`. No checkboxes were changed. Nothing was committed.

## Task 1.3: candidate version 0.1.11

Evidence used:

- `git tag` lists `v0.1.8` and `v0.1.9`; `gh release list` lists the same two (v0.1.9 latest, published 2026-10-27; v0.1.8 on 2026-10-22). No 0.1.10 or 0.1.11 tag or release exists.
- `docs/development.md` ("Legacy dev payloads"), `docs/releases.md`, `openspec/specs/development-builds/spec.md` (scenario "Refuse an unmarked payload") and tests (`DevBuildTests`, `PayloadRetentionTests`, `InstallDevScriptTests`) all name an earlier unmarked, unpublished maintainer trial payload `0.1.10`.
- Code reading: `src/BellTower/Updates/UpdateTransaction.cs` `Stage` rejects an offer when `offer.Version <= installation.Contract(selected.Version).EffectiveVersion` ("already selected or superseded"), and throws "Candidate version already exists" when `versions/<offer.Version>` is present. `ReleaseContract.Effective` (`src/BellTower.Distribution/ReleaseContract.cs`) returns an unmarked non-dev-range version as itself, so an unmarked trial `0.1.10` that is selected would make a published 0.1.10 look already installed, and one that is merely present would block staging with a folder collision. `UpdateDiscovery.Hint` uses the same comparison to hide the offer. The docs state the same collision. `Retire` refuses unmarked payloads, so the machine owner cannot clear it with the supported tool; only automatic tray cleanup can, once it is neither selected nor retained.
- I did not inspect the user's installation (out of scope), so I cannot say whether the payload still exists there. The risk is real by code and documented behavior on any machine that still has it.

Decision: **0.1.11**. Published 0.1.8 and 0.1.9 are untouched. This also keeps the existing 0.1.10 fixtures and the development-builds spec scenario true (they describe the old trial payload as a different thing from a release).

Revision (DEV-002, user decision 2026-11-25, ANALYSIS-040): 0.1.11 is the selected candidate and is recorded here, not applied. The version declarations were reverted to the published 0.1.9 and stay there until candidate packaging (task 4.4), which synchronizes them. Reason: dev builds record `baseVersion` from `Directory.Build.props`, and a base of 0.1.11 would hide and refuse the published 0.1.11 for a dev selection. Task 4.4 must not reselect the version.

Declarations that task 4.4 will set (originally edited by DEV-001):

- `Directory.Build.props`: `Version` 0.1.9 to 0.1.11 (single source for the app, ingress, launcher, protocol, distribution, package script, release descriptor and probes).
- `vscode/package.json`: `version` 0.1.9 to 0.1.11 (`scripts/package.ps1` refuses a mismatch with `Directory.Build.props`; there is no `vscode/package-lock.json`).
- `tests/BellTower.Tests/packages.lock.json`, `tools/BellTower.Ingress.Bench/packages.lock.json`, `tools/BellTower.Smoke/packages.lock.json`: project-reference versions `[0.1.9, )` to `[0.1.11, )` for BellTower.Distribution and BellTower.Protocol.
- `README.md`: the packaged-folder example `bell-tower-0.1.8-win-x64` to `bell-tower-0.1.11-win-x64`.

Not changed on purpose: `scripts/update-acceptance.ps1` (reads the version from `Directory.Build.props`), fixture and historical versions in tests, `.agents`/`.claude` install-dev-build skills, and `docs/handoffs/` (orchestrator-owned).

Superseded checks: the agreement checks below (the 0.1.11 plan output, the build and the focused tests) were run against the applied edits before the revert. After the revert the declarations equal baseline `02d4f56`; see implementation-002.md.

Agreement checks:

- Search over all tracked files outside the archive for `0.1.9` in version declarations (`Directory.Build.props`, `vscode/package.json`, all `packages.lock.json`, README): none remain.
- `pwsh -File scripts/package.ps1 -PlanOnly` reports `version` 0.1.11, `informationalVersion` 0.1.11, no build block.
- `dotnet build -c Release --no-restore`: succeeded, 0 warnings, 0 errors (after the edits).
- `dotnet test -c Release --no-build --no-restore --filter "FullyQualifiedName~DevBuild|~PayloadRetention|~Distribution|~Package|~Version|~InstallDev|~Release"`: passed 189 of 189 (5 min 3 s). This includes `PackageScriptTests`, `DistributionTests`, `DevBuildTests`, `PayloadRetentionTests` and the `InstallDev` script tests.
- `node --test vscode/service.test.js vscode/focus.test.js`: 39 of 39 passed.
- Not run: the complete .NET suite and `dotnet format` (no C# file changed; task 3.4 owns the full run), `scripts/update-acceptance.ps1` (task 3.4; it builds and runs packaged fixtures and was not needed for a version bump).

## Task 1.1 and 1.4: ledger

Created `evidence/coverage-ledger.md`: one row for each of findings 1-13 (finding 12 split into local reminders F12a and the remote part F12b, recorded as user-cancelled on 2026-10-29, not passed and not deferred), the badge rules and the original VS Code activation report; per-row implementing changes, source and test references (every referenced path was checked to exist), archive evidence locations, status, outstanding live check and separate Claude Code, Codex and mixed coverage. It also holds the predecessor reconciliation table (archived checkbox counts and evidence per change), the named gaps (G1 follow-openspec-session-titles 1.1, G2 its 5.3, G3 restore-claude-badge-identity 5.1, G4 ANALYSIS-300, G5 unobserved retained hook captures, G6 the distinct-session-colors 4.3 managed-session and VS Code gap, G7 open provider confirmation limits, G8 adoption follow-ups), the task 1.4 confirmation and the later-release scope table.

Method limits: the archived checkbox counts, evidence listings and the `adopt-native-sessions-from-hooks` ledger were read; test names were matched to files but the full suite was not re-run, so "implemented-with-evidence" relies on the archived reviews and the live records they cite. Findings I would flag:

- Fixture-only with no live record at all: suppress-voice-during-microphone-use (no evidence folder), add-idle-attention-reminders, and the F2/F3/F5/F6 reliability items (their field failures were never reproduced in the installed package).
- The three transferred live checks (6.1-6.3) and ANALYSIS-300 are still open, with no Claude Code, Codex or mixed live evidence.
- Release upgrade risk from adoption follow-up ANALYSIS-5800(b): updates and dev switches leave hooks on the older provisioned ingress until setup is run again (doctor reports `hookStart.ingressImage`). The release notes and task 2.1 upgrade harness should cover this.

Task 1.4: `adopt-native-sessions-from-hooks` is complete (29 of 29 boxes, trial 6.1 accepted 2026-11-24 on 0.1.9029, tasks 4.11/4.12 live-accepted on 0.1.9031, QA-001 to QA-066 and ANALYSIS-001 to ANALYSIS-061 returned, committed c90573a, archived 2026-11-24). `verify-signed-releases-and-update-policy`, `verify-codex-repository-subagents` and `sync-native-session-titles` are recorded in Part 5 of the ledger as later-release scope by the 2026-11-22 decision.

## Task 1.2: reconciliation of archived changes with the composed specs

Method: parsed the archived delta specs and the current `openspec/specs/*/spec.md`, compared requirement text and scenario names, and grepped the composed specs for the newer requirements. I did not edit any spec.

follow-openspec-session-titles (`session-identity` delta, 5 requirements):

- All 5 requirements survive in `openspec/specs/session-identity/spec.md` under the same names. "Metadata updates preserve attention episodes" is byte-identical.
- "App-owned names and optional change association": superseded in a deliberate way. The role-prefixed title (`PM: ttyd-download-chips`) was replaced by the role-free policy; the old scenario "Provider role and work subject" became "Legacy role input and work subject" (title without a role, announcements without a role). This matches the archive notes ("newer role-free naming rules supersede the older role-prefixed scenario"). The 13 other scenarios remain.
- "Shared readable session palette": main has 8 scenarios against 5 archived, all 5 archived scenarios still present; extended by distinct-session-colors (series, live-only allocation, perceptual distance).
- "Git branch and worktree context" (7 and 7 scenarios) and "Bundled and optional terminal presentation" (9 and 9): same scenario set; only ordering of the "Codex does not support the requested footer" scenario differs. The footer restriction is scoped to the native surface only, as the parity rules require.
- The dedicated authenticated Codex runtime, exact-conversation native names and user-rename pause and resume survive in `session-tracking` ("Non-owning access to existing provider runtimes") and `session-identity` (provider-owned terminal titles, native adoption requirements) and `application-distribution` (runtime manager coexistence).

publish-github-with-session-safe-updates (3 delta specs):

- `application-distribution`: all 9 requirements and all scenarios are byte-identical in the main spec.
- `standalone-setup` "Independent tray lifetime and safe management": identical.
- `session-tracking` "Lifecycle reconciliation and restart": 8 archived scenarios, 11 in main, none missing. The text was refined by later changes (directly attached agent versus independent runtime, terminal closes while backend work continues, loaded idle thread is not a working session), preserving the original intent that inactivity alone never proves an end.

Newer requirements preserved in the composed specs: role-free identity (session-identity), checkout (launch-repository-bounded: "Git branch and worktree context", "Provider parity for session identity and checkout" in session-identity and standalone-setup), native adoption (session-tracking "Hook-triggered adoption of native sessions", "Hook tray start from the user's shell", session-identity "Identity continuity during native adoption" and "Presentation ownership for adopted terminals", standalone-setup "Native-command integration without dedicated profiles"), distinct colors (session-identity "Shared readable session palette"), bounded state (session-tracking "Bounded monitoring state").

Shared policy with safe native transports: provider-parity requirements exist in session-identity (2), session-tracking (3 plus "One product with shared provider features"), standalone-setup (2), attention-notifications (2), attention-reminders, microphone-aware-audio, terminal-navigation and repository-agent-workflow. The safe native transport is stated in attention-notifications ("existing safe native transport and episode isolation") and the Codex runtime ownership rules in session-tracking.

Recommended spec edit: none required for the reconciliation. Two observations the orchestrator may want, both optional and not blocking:

1. `openspec/specs/application-distribution/spec.md` and `openspec/specs/development-builds/spec.md` contain no requirement titled as provider parity. Both are provider-neutral (update flow, packaging), and `session-tracking` requires updates to preserve both providers' sessions ("Session-preserving update completion" names running Claude and managed Codex sessions in its scenario), so I treat this as covered; the capability matrix in task 5.3 should state it explicitly.
2. `openspec/specs/development-builds/spec.md` scenario "Refuse an unmarked payload" names the unmarked dev build `0.1.10`. Because the candidate is 0.1.11, that text stays correct and needs no edit; it would only need one if the 0.1.10 trial payload were ever resolved on the maintainer's machine.

## Limitations

- No live or installed-package check was run; this task is evidence and version work.
- The user's installation was not inspected, so whether a trial 0.1.10 payload is still present is unknown.
- `docs/handoffs/next-release-plan.md` still states the previous current-version facts and belongs to task 4.5.
