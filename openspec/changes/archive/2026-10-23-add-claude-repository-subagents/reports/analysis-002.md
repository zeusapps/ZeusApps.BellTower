> **Persisted by the orchestrator, verbatim, under the ORCH-001 fallback** (spec "Durable change-local reports and screenshots", design D11).
> - Reviewer: native Claude Code `code-analyzer` subagent (AN-R2), Claude Code 2.1.309, auto mode, claude-opus-5-5 (role default `opus`, `effort: high`).
> - The reviewer's single self-write attempt on 2026-10-23 was refused with: "Subagents should return findings as text, not write report files. Include this content in your final response instead."
> - The reviewer returned the full text below, which the orchestrator saved without changing its content. Ledger: ORCH-001 in [findings](findings.md).

# Analysis report: add-claude-repository-subagents / analysis-002

## Review target

- Reviewer / role: native Claude Code `code-analyzer` subagent (AN-R2) / code-analyzer
- Client / version / mode / model (resolved ID): Claude Code 2.1.309 / native project subagent from `.claude/agents/code-analyzer.md`, auto mode, Windows 11 Pro 10.0.26300 / `claude-opus-5-5`. Role default is `opus` with `effort: high`, no override. I cannot observe the effort setting from inside the subagent.
- Date: 2026-10-23T21:13Z to 2026-10-23T21:20Z
- Change root / assignment: `D:\Code\ZeusApps.BellTower\openspec\changes\add-claude-repository-subagents` / AN-R2
- Task and requirement references:
  - Tasks 1.1, 1.2, 2.1–2.6 and 5.1 ([tasks.md](../tasks.md))
  - Design D1, D2, D7, D10 and D11 ([design.md](../design.md))
  - Requirements "Native Claude Code and Codex workflow adapters", "Per-role model selection", "Shared entry point for both clients" and "Durable change-local reports and screenshots" ([spec](../specs/repository-agent-workflow/spec.md))
- Scope: `.claude/agents`, `.codex/agents`, `.agents`, `docs/agent-workflow.md`, `AGENTS.md`, `scripts/review-identity.ps1`, `tests/BellTower.Tests/Scripts`
- Neighboring files read: `scripts/format.ps1`, `docs/development.md` (commands section), the [findings ledger](findings.md)
- Baseline commit: `b3e007e4c38a7893886e908998abbd5dfd5844a3`
- Implementation identity (before), from `./scripts/review-identity.ps1 -Baseline b3e007e4c38a7893886e908998abbd5dfd5844a3 -Paths .claude/agents .codex/agents .agents docs/agent-workflow.md AGENTS.md scripts/review-identity.ps1 tests/BellTower.Tests/Scripts`, run from the repository root at 21:13:02Z:
  - HEAD: `b3e007e4c38a7893886e908998abbd5dfd5844a3`
  - Untracked (`?`): `.agents/roles/project-facts.md`, `.claude/agents/{code-analyzer,developer,qa}.md`, `scripts/review-identity.ps1`, `tests/BellTower.Tests/Scripts/ReviewIdentityScriptTests.cs`
  - Modified (`M`): `.agents/roles/{code-analyzer,developer,qa}.md`, `.agents/templates/{analysis-report,findings,qa-report}.md`, `.agents/workflows/openspec-delegation.md`, `.codex/agents/{code-analyzer,developer,qa}.toml`, `AGENTS.md`, `docs/agent-workflow.md`
  - Tracked diff SHA-256: `e452a79d95f4f38288afb5401f5248cadc20e9d4a6bcfca96ea854de0eb72ab1`
  - Untracked file hashes:
    - `f444f610eb1611d6effa909234af697effcb33a12c7b19821e3fed5c5857e46a` project-facts.md
    - `150679d7ea55b85132d56f839d3a0d19a5bba171a69dc279ba24384b8e039be0` .claude/agents/code-analyzer.md
    - `69b1b966b4d8d8d0b66e30d06af66387a0d8671cd2bba86a178c087d755fe196` .claude/agents/developer.md
    - `9dea9aa5cf20345f0339ba6941b629d2684491885eaa0df47014cd4284ffb633` .claude/agents/qa.md
    - `2ad8e116096318e4220cd63ed8c5ac7bb556222ec7d53d63b3ea4220208c316e` review-identity.ps1
    - `07e64c1651886ff42271fb4a93c339dab77b4b49a33b363340b319eeb6d8f92c` ReviewIdentityScriptTests.cs
  - Summary: `head=b3e007e baseline=b3e007e files=18 diff=e452a79d95f4 untracked=6/7f38333b238b`. This matches the orchestrator's recorded identity.
- Before/after identity: the after run at 21:20:14Z gives the same summary. `Compare-Object` of the full before and after outputs shows 0 differences. No unexpected changes.
- Prior reports / rechecked findings: [analysis-001](analysis-001.md), rechecking ANALYSIS-002 to -014 against DEV-002, DEV-003 and DEV-004. ANALYSIS-001 is a duplicate of QA-001, and ANALYSIS-015 was resolved in planning, so neither is rechecked here.
- Report write (ORCH-001 regression record, design D11): my single self-write attempt was refused with "Subagents should return findings as text, not write report files. Include this content in your final response instead." The report was returned inline under the fallback.

## Analysis performed

| Area | Method / command | Evidence | Outcome / limitation |
| --- | --- | --- | --- |
| Identity helper behavior | Ran the script in-process, via `pwsh -File`, from `docs/`, with an unmatched path, an outside path, case-mismatched paths, and under PowerShell 5.1 | Results quoted in the re-verification table and ANALYSIS-017 | ANALYSIS-002 to -004 verified. A case-mismatched path still produces a wrong identity (ANALYSIS-017). |
| Hash stability across git settings | Child `pwsh` runs with 13 diff settings injected through `GIT_CONFIG_COUNT`/`KEY`/`VALUE`, first together and then one at a time | Settings named in ANALYSIS-005 have no effect. `diff.algorithm`, `diff.context`, `diff.interHunkContext` and `diff.suppressBlankEmpty` each change the diff hash. | ANALYSIS-005 verified for what it named. Residual raised as ANALYSIS-019. |
| Identity helper performance | `Measure-Command`, 3 runs on the 18-file scope | 312, 291, 296 ms (round 1: 385 ms) | No concern |
| Identity test | `dotnet test tests/BellTower.Tests -c Release --no-build --no-restore --filter FullyQualifiedName~ReviewIdentityScriptTests` | Passed 6/6, 21 s, exit 0. The test DLL (20:57:09Z) is newer than the test source (20:56:58Z), and the script is read at run time, so the no-build run covers the current source. | ANALYSIS-006 verified. Runtime grew from 5 s to 21 s (about 18 `pwsh` launches); observation only, no finding. |
| Formatting | `dotnet format ZeusApps.BellTower.slnx --no-restore --verify-no-changes`, then the documented scoped form `dotnet format --verify-no-changes --include tests/BellTower.Tests/Scripts/ReviewIdentityScriptTests.cs` | Whole solution: exit 2, 16 unrelated files under `src/BellTower`. Scoped: exit 0 in 8.6 s, and it found `ZeusApps.BellTower.slnx` without an argument. | Scoped `.cs` file is clean. The whole-solution result shows that the developer's mutating command would rewrite 16 unrelated in-flight files (ANALYSIS-018). |
| Duplication | `grep` for spawn and commit in adapters, model IDs, the identity usage text, fallback text, marker procedure, and change-local paths. Byte comparison of Claude and Codex adapter bodies and descriptions. | Adapter bodies and descriptions are identical across clients. Model IDs appear only in adapters. The identity usage text is copied 5 times. The marker procedure is still restated in the findings template. | ANALYSIS-007 to -011 and -013 verified. ANALYSIS-012 is partly fixed. New ANALYSIS-020 and -021. |
| Entry-point loading | Compared the `AGENTS.md` text in this subagent's starting context with the file on disk | My starting context holds the baseline `AGENTS.md` text, with the Claude deferral and the unqualified "Read the workflow before repository work". It does not hold the trimmed text on disk. | ANALYSIS-016 |
| Context cost | `wc -c` of baseline (`git show b3e007e:<path>`) and working-tree files. All scoped files are LF, so the bytes compare directly with round 1. | Table below | Specialist launches are 39–50% smaller than baseline and 1–8% larger than in round 1 |
| Architecture | Compared `project-facts.md` with `docs/development.md` lines 33–47 | Build, test and format commands match. The scoped reviewer format command and the developer caveat are additions not in development.md. | No drift, apart from ANALYSIS-018 |
| Parity | Compared every rule and default across `.claude/agents` and `.codex/agents`, and checked shared policy for wording that only fits one client | Effort is `high` on both. One Claude-only alias remains in shared policy. | ANALYSIS-014 verified. New ANALYSIS-022. Codex behavior was not run (task 4.1). |
| Product architecture | Not applicable | No product source in scope | Not applicable |

### Per-launch instruction bytes

What each launch loads:

- Specialists: adapter file + auto-loaded `AGENTS.md` + role + `project-facts.md` + template (reviewers only).
- Baseline specialists: adapter + `AGENTS.md` + workflow + role + template, the same accounting as round 1.
- Orchestrator: `AGENTS.md` + workflow + findings template.

| Launch | Baseline b3e007e (Codex) | Round 1, DEV-001 (Claude / Codex) | Now on disk (Claude / Codex) | Change vs baseline | Change vs round 1 |
| --- | --- | --- | --- | --- | --- |
| developer | 17222 | 8484 / 8519 | 8592 / 8724 | -50.1% / -49.3% | +108 / +205 (+1.3% / +2.4%) |
| qa | 20713 | 11654 / 11676 | 12394 / 12526 | -40.2% / -39.5% | +740 / +850 (+6.3% / +7.3%) |
| code-analyzer | 19907 | 11087 / 11144 | 11903 / 12057 | -40.2% / -39.4% | +816 / +913 (+7.4% / +8.2%) |
| orchestrator | 17889 | 17842 | 17320 | -3.2% | -522 |

Where the change since round 1 comes from:

- `AGENTS.md`: 3582 → 2642 bytes (-940)
- Adapters: about -105 bytes each
- Role plus `project-facts.md` compared with round 1's self-contained role:
  - developer: 4450 → 5603 (+1153)
  - qa: 4837 → 6633 (+1796)
  - code-analyzer: 4467 → 6326 (+1859)
  - `project-facts.md` is 3010 bytes. It is the union of the three old per-role sections plus new text, so each role now loads facts aimed at the others. Example: the developer loads 715 bytes of formatter guidance, about half of which is for reviewers.
- DEV-004 additions:
  - Fallback bullet: 329 bytes in each reviewer role
  - Formatter split: 715 bytes in `project-facts.md`, about +370 bytes net for every role
  - Workflow fallback paragraph: 523 bytes, orchestrator only
  - TOML comment: 95 bytes in each Codex adapter

The real cost in this session is higher. Because of ANALYSIS-016, native Claude subagents receive the baseline `AGENTS.md` (3585 bytes) instead of the 2642 bytes on disk. That gives developer 9535, qa 13337 and code-analyzer 12846 bytes.

The Claude figures above are upper bounds. In this run my starting prompt contained only the adapter body, not its frontmatter. The bodies are 120 / 182 / 199 bytes against full files of 347 / 438 / 503 bytes. That comes from one observation. Bytes are not tokens.

## Findings

### ANALYSIS-016: Native Claude subagents receive the AGENTS.md text from session start, not the file on disk

- Severity: warning
- Related task / requirement: 2.4, 3.5, 4.4, 5.1; D3, D7; spec "Shared entry point for both clients"
- Locations: `AGENTS.md:15-17` on disk, compared with the copy injected into this subagent's starting context
- Observation:
  - This subagent started with the baseline `b3e007e` text of the "Delegated OpenSpec workflow" section. That text includes "The repository's native multiagent configuration currently targets Codex. The user explicitly deferred Claude Code configuration…" and the unqualified "Read `.agents/workflows/openspec-delegation.md` before repository work."
  - The file on disk has the trimmed DEV-003 paragraph, which tells specialists not to read the workflow.
  - The ledger records that this orchestrator session was not restarted after DEV-001 edited `AGENTS.md`. The likely mechanism is that subagents get the parent session's instruction snapshot from session start; this is a hypothesis.
  - Effects:
    - In this session, every native specialist is told the Claude deferral is still in force and is told to read the 11 KB workflow, which is what D7 removes.
    - The ANALYSIS-009 saving (-943 bytes) does not reach subagents.
    - Task 3.5 evidence ("role files were sufficient", QA-R2) and the round-1 observation for task 4.4 ("AGENTS.md was loaded") were collected under the stale text. They prove the file was present, not that the current content was loaded.
  - The role files are read from disk at run time, so they are current. Only the auto-loaded entry point is stale.
- Proposed improvement and benefit:
  - Restart the Claude orchestrator session before the remaining acceptance rounds (5.1, the 3.5 rerun, 4.4).
  - Make task 4.4 check content, not just presence: a native subagent quotes a phrase that exists only in the current `AGENTS.md`.
  - In `docs/agent-workflow.md` "Start and activate", say that edits to `AGENTS.md` also need a session restart before subagents see them.
  - The Codex equivalent is unverified. Codex also reads `AGENTS.md` at session start, so task 4.1 should run the same content check.
- Behavior-preservation risks: none; this is process and documentation.
- Evidence type: measured, one observation in this subagent (its injected context compared with `git diff b3e007e -- AGENTS.md`). The mechanism is a hypothesis.
- Verification / measurement plan: after a restart, launch one native specialist and ask it to quote the first sentence of the "Delegated OpenSpec workflow" section. It must match the file on disk.
- Relationship: affects the evidence for ANALYSIS-009 and task 3.5. New.

### ANALYSIS-017: A case-mismatched path passes the existence check and produces a wrong identity

- Severity: warning
- Related task / requirement: 2.3; D10 ("a path that matches nothing … fails the run")
- Locations: `scripts/review-identity.ps1:108-110` (existence check uses `Test-Path`) and `:117`, `:122`, `:131` (case-sensitive literal pathspecs). The checkout has `core.ignorecase=true`.
- Observation:
  - `Test-Path` is case-insensitive on NTFS, but git's literal pathspecs are case-sensitive, so the two disagree.
  - `-Paths agents.md` prints `files=0 diff=e3b0c44298fc`, a clean target, with exit 0. `AGENTS.md` is modified.
  - `-Paths .AGENTS/roles` prints `files=4 diff=e3b0c44298fc untracked=4/6781f5428fea`. It lists the three tracked, modified role files as untracked (`? .AGENTS/roles/qa.md`, …) and hashes them as untracked. The correct output for `.agents/roles` is `files=4 diff=abc1e037c2ab untracked=1/557d2e296add`.
  - Before and after runs with the same wrong case still match each other, so the reviewer-mutation check passes without testing the tracked diff. This is the same failure ANALYSIS-002 described, through a path variant the fix does not cover.
- Proposed improvement and benefit: canonicalize the case of each existing path component from the file system before passing it to git. For example, for each segment, use `Get-ChildItem -LiteralPath <parent> -Force` and pick the entry whose name matches case-insensitively. Or fail when the requested spelling differs from git's (`git ls-files` returns no entry, but `Test-Path` succeeds). Either way, a typo cannot pass as a clean or reclassified target.
- Behavior-preservation risks: correctly cased paths give the same output. Recorded identities do not change.
- Evidence type: measured (outputs above, this checkout).
- Verification / measurement plan: add a test in the disposable repository with `core.ignorecase=true` that runs the script with a wrong-case file and a wrong-case directory. Expect either the same output as the correct case or a non-zero exit. The test only reproduces on case-insensitive file systems, which is the project's Windows target.
- Relationship: residual of ANALYSIS-002, which is verified for the cases it named.

### ANALYSIS-018: The developer's formatter instruction rewrites unrelated in-flight files

- Severity: warning
- Related task / requirement: 2.1; DEV-004 formatter split (QA-004); `developer.md:8` ("Never format … files outside your assigned paths")
- Locations: `.agents/roles/project-facts.md:10`; `scripts/format.ps1:7` (whole solution)
- Observation:
  - `project-facts.md:10` tells the developer to run `./scripts/format.ps1`, then "check `git status` afterward and keep unrelated files untouched; if it would change files outside your scope, fix style by hand".
  - The developer cannot know beforehand whether the formatter will touch other files. Once it has, `developer.md:8` forbids reverting them.
  - Measured now: `dotnet format ZeusApps.BellTower.slnx --no-restore --verify-no-changes` reports 16 unrelated files under `src/BellTower` (for example `Core/Palette.cs`, `Sessions/SessionCatalog.cs`, `Tray/OverviewForm.cs`). The mutating run would rewrite every one of them in the user's in-flight work.
  - The reviewers' scoped `--include` form added by DEV-004 is the safe pattern, but the developer does not have it.
- Proposed improvement and benefit:
  - Give the developer the same split: on a dirty tree, run `dotnet format ZeusApps.BellTower.slnx --no-restore --include <own changed files>`; use `./scripts/format.ps1` only on a clean tree or when the assignment covers the whole solution.
  - Optionally give the reviewer form the same explicit `ZeusApps.BellTower.slnx --no-restore` arguments that `format.ps1` uses. It works without them today (exit 0, 8.6 s, solution found automatically), but explicit arguments avoid an implicit restore and keep it identical to the script.
  - This also removes the after-the-fact `git status` caveat, about 150 bytes of each launch.
- Behavior-preservation risks: none for the developer's own files; the same formatter runs on fewer files.
- Evidence type: measured (whole-solution verify, exit 2, 16 files; scoped verify exit 0).
- Verification / measurement plan: on the current dirty tree, the developer command from `project-facts.md` changes only the files passed to `--include` (`git status` before and after, in a disposable copy).
- Relationship: new. QA-004 owns the reviewer half.

### ANALYSIS-019: Four git settings still change the diff hash, and the output label misstates the pinned arguments

- Severity: suggestion
- Related task / requirement: 2.3; D10 ("user git configuration cannot change the bytes"); 4.1 (hand-off between clients)
- Locations: `scripts/review-identity.ps1:42` (`$gitOptions`), `:131` (hashed diff), `:9` and `:141` (description and label)
- Observation:
  - With the same tree, each of these settings changes the `diff=` hash:
    - `diff.algorithm=patience` → `3aab9c03a0ea`
    - `diff.context=7` → `27964659b821`
    - `diff.interHunkContext=5` → `cc64af9147ff`
    - `diff.suppressBlankEmpty=true` → `f6e22b66be80`
  - The untracked digest is unaffected. The settings ANALYSIS-005 named (`diff.noprefix`, `diff.mnemonicPrefix`, `diff.relative`, `diff.renames`, `color.diff`, `color.ui`, `diff.external`) no longer have any effect.
  - The label on every run says the hash covers `--binary --no-renames --no-color --src-prefix=a/ --dst-prefix=b/`. The command actually adds `--no-ext-diff --no-textconv`. The test pins the incomplete label (`ReviewIdentityScriptTests.cs:149`).
  - D10 claims that git configuration cannot change the bytes, which is not yet true. This matters only when identities are compared across machines or accounts. Claude and Codex on one machine share the same config.
- Proposed improvement and benefit:
  - Add `-c diff.algorithm=myers -c diff.context=3 -c diff.interHunkContext=0 -c diff.suppressBlankEmpty=false -c diff.indentHeuristic=true` to `$gitOptions`, or the equivalent `--diff-algorithm=myers -U3 --inter-hunk-context=0` diff flags.
  - Shorten the label to `Tracked diff SHA-256: <hash>` and keep the full argument list in `.DESCRIPTION`. That removes about 120 bytes of each run's output and stops the label and the code from drifting apart.
  - Optional, more robust: hash a structural identity instead of patch text. That is the sorted name-status list plus each path's baseline blob ID and current content SHA-256, which is independent of all diff settings.
- Behavior-preservation risks: pinning defaults does not change the hash in this checkout. Changing the label or switching to a structural identity changes the output format. Record that in the round that adopts it, as DEV-002 did.
- Evidence type: measured (child `pwsh` runs with `GIT_CONFIG_COUNT` injection).
- Verification / measurement plan: extend the rename test to inject the four settings through `GIT_CONFIG_COUNT` and assert an unchanged `diff=` line.
- Relationship: residual of ANALYSIS-005, which is verified for the settings it named.

### ANALYSIS-020: The identity-helper invocation is copied five times

- Severity: suggestion
- Related task / requirement: 2.1, 2.2, 2.6; spec "Each rule SHALL have one authoritative location"
- Locations: `.agents/roles/developer.md:22`, `qa.md:18`, `code-analyzer.md:19`, `.agents/workflows/openspec-delegation.md:35`, `docs/agent-workflow.md:43`
- Observation:
  - All five carry the full invocation plus the same 88-byte note "(space-separated paths, relative to the current directory; run from the repository root)".
  - The QA-001 fix had to touch all five, and the next change to the invocation will too.
  - The command is a project fact like build and test, and `project-facts.md` exists for exactly that.
  - Related: the two reviewer roles also share byte-identical bullets (fallback, no-spawn, commit, parent permissions, client and model record). The spec places those in role files, and they are identical, so I do not raise them.
- Proposed improvement and benefit:
  - Add one "Review identity" bullet with the invocation to `project-facts.md` Commands.
  - In the roles, keep only the role-specific action, such as "record the identity before and after (project facts)" or "give the summary line".
  - Keep the orchestrator copy in the workflow if wanted, since `project-facts.md` is specialist-facing; docs can link.
  - Saves about 200 bytes per specialist launch and leaves 2 copies instead of 5.
- Behavior-preservation risks: low; the command text does not change.
- Evidence type: source observation (grep, 5 matches).
- Verification / measurement plan: `grep -rn "review-identity.ps1 -Baseline" .agents docs` finds the facts file and at most the workflow.
- Relationship: new; ANALYSIS-001 and QA-001 caused the five edits.

### ANALYSIS-021: Durable files point at this change's paths and tasks, and will go stale at archive

- Severity: suggestion
- Related task / requirement: 4.1, 5.4; D11 (ORCH-001); spec "Durable change-local reports" (links usable after archive)
- Locations:
  - `.agents/workflows/openspec-delegation.md:51`: `[design D11](../../openspec/changes/add-claude-repository-subagents/design.md)`
  - `.codex/agents/{developer,qa,code-analyzer}.toml:3`: `# Documented, not yet natively verified (pending task 4.1 of add-claude-repository-subagents).`
  - `docs/agent-workflow.md:26`: restates the pending status
- Observation:
  - Archive moves changes to `openspec/changes/archive/<date>-<name>/` (see the existing `2026-10-23-introduce-repository-subagents`), so the workflow link will break.
  - The TOML comments and the docs sentence are status markers in three durable places, plus the QA-002 ledger entry, and no task says to update them.
  - Tasks 4.1 and 5.4 do not mention any of these, so they are likely to outlive their truth.
- Proposed improvement and benefit:
  - Move the ORCH-001 restriction evidence into a durable home at sync time, such as the main `repository-agent-workflow` spec or a short inline record in the workflow, and link that.
  - Add "update or remove the Codex pending comments and the docs sentence" to task 4.1's verification.
  - Optionally, drop the docs restatement, so the TOML comment is the only marker.
- Behavior-preservation risks: none.
- Evidence type: source observation.
- Verification / measurement plan: after archive, `grep -rn "openspec/changes/add-claude" .agents docs .codex` finds nothing, and the restriction link resolves.
- Relationship: new; related to QA-002 and ORCH-001.

### ANALYSIS-022: The docs-only analyzer override names a Claude alias only

- Severity: suggestion
- Related task / requirement: 2.2; D11 (R12); AGENTS.md parity ("Include both providers in proposals, specs, designs, tasks")
- Locations: `.agents/workflows/openspec-delegation.md:68` ("may run the analyzer on `sonnet` as a recorded override")
- Observation:
  - This is the only model alias left in shared policy, and it names only Claude's.
  - A Codex orchestrator has no stated equivalent and would have to guess, or leave the analyzer on `gpt-6.1-sol`/high for config-only targets.
- Proposed improvement and benefit:
  - Say "on the developer/QA default model (Claude `sonnet`, Codex: the model in `.codex/agents/qa.toml`)", or just "the cost-efficient tier", so both clients get the same rule.
  - Keep concrete values in the adapters, as ANALYSIS-008 intends.
- Behavior-preservation risks: none.
- Evidence type: source observation.
- Verification / measurement plan: grep shows the override sentence names a tier or both clients.
- Relationship: follow-up to ANALYSIS-008 (verified).

## Re-verification

All rows are against `files=18 diff=e452a79d95f4 untracked=6/7f38333b238b`.

| Finding ID | Independent evidence | Outcome |
| --- | --- | --- |
| ANALYSIS-002 | An unmatched path throws "Path 'typo.md' matches nothing in the working tree, the index, or baseline b3e007e." From `docs/`, `-Paths agent-workflow.md` gives `files=1 diff=3706077c3dca` (round 1: `files=0`). `../x` is rejected as outside the repository. Tests `FailsWhenAPathMatchesNothing…` and `ResolvesPathsRelativeToTheCallerDirectory…` pass. | verified for the reproductions named. Case-mismatch residual raised as ANALYSIS-017. |
| ANALYSIS-003 | `$x = ./scripts/review-identity.ps1 …` captures 17 lines. `Compare-Object` of the space form and the comma form shows 0 differences. A `pwsh -File` child with `2>&1` returns 17 lines and 0 error records, so there is no CRLF warning noise (round 1: 12–24 lines). | verified |
| ANALYSIS-004 | `powershell.exe` 5.1 stops with "…contained a "#requires" statement for Windows PowerShell 7.0…" (`review-identity.ps1:1`). | verified |
| ANALYSIS-005 | `diff.noprefix`, `diff.mnemonicPrefix`, `diff.relative`, `diff.renames=copies`, `color.diff`/`color.ui=always` and `diff.external` injected together or one at a time leave `diff=` unchanged. The manifest and hashed diff both use `--no-renames`, and the rename test shows `A`+`D`. | verified for the settings named. Four other settings still change the hash (ANALYSIS-019). |
| ANALYSIS-006 | 6 facts cover the space, comma and positional forms, in-process capture with `Compare-Object`, `A` status, the unmatched path, subdirectory resolution, renames, and isolated `GIT_CONFIG_GLOBAL`/`NOSYSTEM`. Passed 6/6 in 21 s on a current binary. | verified. Remaining gaps belong to the new ANALYSIS-017 and -019. |
| ANALYSIS-007 | `grep -i "spawn\|commit"` on all six adapters finds nothing. The Claude body and Codex `developer_instructions` are byte-identical for each role, and so are the descriptions. | verified |
| ANALYSIS-008 | Model IDs and aliases in `AGENTS.md`, `.agents` and `docs/agent-workflow.md` appear only in the docs-only `sonnet` override sentence (`openspec-delegation.md:68`). | verified. Parity wording follow-up in ANALYSIS-022. |
| ANALYSIS-009 | `AGENTS.md` is 3585 → 2642 bytes (-26%). Lines 1–14, the parity section, are byte-identical to baseline. The workflow instruction is addressed to the orchestrator, and specialists are told not to read it. | verified in source. Native Claude subagents in this session still receive the baseline text (ANALYSIS-016). |
| ANALYSIS-010 | One safety list in `project-facts.md:26-33`. It is the union of round 1's three lists: setup, exit, voice and sound; data directory; tray and sessions; `~/.claude`, `~/.codex` and personal config; approvals, audio and production messages; isolated data, fakes and silent sinks; live-only checks recorded as not performed. No role keeps its own copy. | verified |
| ANALYSIS-011 | `.agents/roles/project-facts.md` exists. All six adapters name it, and every role links it under "Role-specific checks". The design and spec state the user's decision. | verified |
| ANALYSIS-012 | Overlaps removed: states and transitions, now only in workflow `:59`, with the template linking (`findings.md:47`); reservation rules (`findings.md:20` links); the restriction list, now a link to AGENTS.md (`:84`); identity and report-acceptance rules rephrased as orchestrator checks (`:35`, `:53`). One overlap remains: the marker procedure is restated in `.agents/templates/findings.md:7` ("Read it before assigning work; if it names a different active session, ask the user…") as well as in workflow `:75`. The workflow is 11288 bytes, larger than baseline, because model-selection, commit, mixed-client and fallback policy were added, so round 1's estimated 1–1.5 KB saving did not happen. | still failing, minor: 5 of 6 overlaps removed. The fix is to cut `findings.md:7` to "Advisory marker; see the [workflow](…#mixed-client-operation)". |
| ANALYSIS-013 | `qa.md:8` and `code-analyzer.md:9` keep only the allowed-writes list plus "Request any other change … through findings". The behavioral-not-enforced point now appears once per role (`qa.md:12`, `code-analyzer.md:13`). | verified |
| ANALYSIS-014 | `.claude/agents/code-analyzer.md:5` has `effort: high`, and `.codex/agents/code-analyzer.toml:5` has `model_reasoning_effort = "high"`. Design D2 records the user decision. Effort is not observable from inside this subagent, and the ledger row records "opus, effort high". | verified in source. Native effect not observed. |

DEV-004 additions:

- Fallback text (`qa.md:10`, `code-analyzer.md:11`, `openspec-delegation.md:51`):
  - The two role bullets are byte-identical (329 bytes each).
  - The workflow paragraph matches the spec scenario: header names reviewer, client, version, mode, model and the exact refusal message, and the fallback is limited to the refused write.
  - It worked in this round: the write was refused, and I returned the report inline without a retry.
  - The only problem is the change-local link (ANALYSIS-021).
- TOML comments: present next to `model` in all three files, labeled as pending task 4.1. Staleness is covered in ANALYSIS-021.
- Formatter split: the reviewer form works (see Analysis performed). The developer form is ANALYSIS-018.

## Assessment and limitations

- Coverage: every scoped file was read in full at the identity above. The `.agents/skills` copies are in scope but unchanged from baseline (not in the manifest), so I did not review them.
- Rechecks: ANALYSIS-002 to -011, -013 and -014 are verified. ANALYSIS-012 is partly fixed and still failing for one minor overlap.
- New findings: three warnings and four suggestions.
  - ANALYSIS-016 matters most for acceptance. Native subagents in this session run on the baseline `AGENTS.md`, so tasks 3.5 and 4.4 and the 5.1 final rounds should be rerun or confirmed after a session restart.
  - ANALYSIS-017 is the last silent wrong-identity path I found in the helper.
  - ANALYSIS-018 is a safety issue for dirty trees like the current one, where 16 unrelated files would be reformatted.
- Context cost:
  - Specialist launches are 39–50% below baseline. They grew 1–8% over round 1, because the shared facts file is a union loaded by every role and DEV-004 added the fallback and formatter text.
  - ANALYSIS-020 and the developer half of ANALYSIS-018 would recover about 350 bytes per launch.
  - The orchestrator's load is 3% below baseline.
  - All figures are bytes, not tokens.
- Not measured or not performed:
  - Codex behavior of any kind (task 4.1)
  - Whether Claude honors `effort: high`
  - Cross-machine identity comparison. The injection test stands in for it.
  - The full test suite. I ran the 6 identity tests only.
  - `openspec validate --strict`. It is not in my assignment, and QA-R3 covers task 5.1 checks.
- ORCH-001 regression record: the native analyzer's single report write was refused again with the same message, and the fallback was used.
- Unexpected writes: none. The only other outputs are test and format temporary data, plus `id-before.txt` in my scratchpad outside the repository.
- Decisions needed:
  - Whether to restart the orchestrator session before the remaining acceptance rounds (ANALYSIS-016)
  - Whether ANALYSIS-019 should pin defaults (no format change) or switch to a structural identity (format change)
