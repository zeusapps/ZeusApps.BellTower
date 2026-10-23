> **Persisted by the orchestrator, verbatim.** The native Claude Code `code-analyzer` subagent (AN-R1, Claude Code 2.1.309, auto mode, claude-opus-5-5) tried to write this report twice on 2026-10-23. Both writes were refused with: "Subagents should return findings as text, not write report files. Include this content in your final response instead." The second refusal came after a project `Edit(...)` allow rule was added, so it is not a permission-rule or classifier decision; the source was not found in local hooks or plugins. Native QA writes succeeded in the same session. On the user's decision (2026-10-23), the orchestrator saved the returned report text below without changes to its content. See ORCH-001 in [findings](findings.md).

# Analysis report: add-claude-repository-subagents / analysis-001

## Review target

- Reviewer / role: native Claude Code `code-analyzer` subagent (AN-R1) / code-analyzer
- Client / version / mode / model (resolved ID): Claude Code 2.1.309 / native project subagent from `.claude/agents/code-analyzer.md`, auto mode, Windows 11 / `claude-opus-5-5` (role default `opus`, no override)
- Date: 2026-10-23T20:48:00Z
- Change root / assignment: `D:\Code\ZeusApps.BellTower\openspec\changes\add-claude-repository-subagents` / AN-R1
- Task and requirement references:
  - Tasks 1.1, 1.2 and 2.1–2.6 ([tasks.md](../tasks.md))
  - Design D1, D2, D3 and D7–D11 ([design.md](../design.md))
  - Requirements "Native Claude Code and Codex workflow adapters", "Per-role model selection" and "Shared entry point for both clients" ([spec](../specs/repository-agent-workflow/spec.md))
- Scope: `.claude/agents`, `.codex/agents`, `.agents`, `docs/agent-workflow.md`, `AGENTS.md`, `scripts/review-identity.ps1`, `tests/BellTower.Tests/Scripts`
- Neighboring files read: `docs/development.md`, `scripts/format.ps1`
- Baseline commit: `b3e007e4c38a7893886e908998abbd5dfd5844a3`
- Implementation identity (before), from `./scripts/review-identity.ps1 -Baseline b3e007e4c38a7893886e908998abbd5dfd5844a3 -Paths .claude/agents,.codex/agents,.agents,docs/agent-workflow.md,AGENTS.md,scripts/review-identity.ps1,tests/BellTower.Tests/Scripts`:
  - HEAD: `b3e007e4c38a7893886e908998abbd5dfd5844a3`
  - Untracked (`?`): `.claude/agents/{code-analyzer,developer,qa}.md`, `scripts/review-identity.ps1`, `tests/BellTower.Tests/Scripts/ReviewIdentityScriptTests.cs`
  - Modified (`M`): `.agents/roles/{code-analyzer,developer,qa}.md`, `.agents/templates/{analysis-report,findings,qa-report}.md`, `.agents/workflows/openspec-delegation.md`, `.codex/agents/{code-analyzer,developer,qa}.toml`, `AGENTS.md`, `docs/agent-workflow.md`
  - Diff SHA-256: `cf1dd0d939840040a8f3e8e01863f1475051863f3e9d527dc90d0a19d8e82977`
  - Untracked file hashes:
    - `5ab56df3a7b7aba3583739342231353a4d051d420b382240b3f5ad4fa4faab50` code-analyzer.md
    - `3f0c1fea201e8b760864b7fc94191cb05ce028f6e38c374352e87d61b85e71df` developer.md
    - `6db1a8b3f388e1e46cb505ab6f25a04849b29eef50fe67e32fb3a62926d8b9b5` qa.md
    - `03ee3904e58a3d9f239237230d83eca5ca6c98913c37ceba91a0cc7dee2cb38c` review-identity.ps1
    - `434df0d0d2119397a3211f8acfb9d4d8c036103fc96e2ed3e605d0451ac116bd` ReviewIdentityScriptTests.cs
  - Summary: `head=b3e007e baseline=b3e007e files=17 diff=cf1dd0d93984 untracked=5/3b2823125e0b`
- Before/after identity: the after run is identical in manifest, hashes and summary, and matches the orchestrator's recorded identity. No unexpected changes.
- Prior reports / rechecked findings: none

## Analysis performed

| Area | Method / command | Evidence | Outcome / limitation |
| --- | --- | --- | --- |
| Context cost | `wc -c` at baseline (`git show b3e007e:<path>`) and in the working tree | Measurements below | Bytes, not tokens. Specialist launches are 44–51% smaller. |
| Entry-point loading | This subagent's own starting context | `AGENTS.md` (and the user's global rules) were already loaded when this native subagent started | One live observation; useful for task 4.4, not a replacement for it |
| Duplication | Read every scoped file; `grep -rnoi` for commit, spawn, enforce, before and after, immutable, fixture, awaiting-verification, advisory, model IDs, restriction, reuse | Commit rule appears in 12 places, no-spawn rule in 10 | ANALYSIS-007 to -012 |
| Role clarity | Compared the three roles side by side | Safety lists differ; reviewer write rules are stated twice | ANALYSIS-010, -011, -013 |
| Adapter parity | Compared Claude frontmatter with Codex TOML keys | Codex sets `model_reasoning_effort = "high"`; Claude sets no `effort` | ANALYSIS-014 |
| Script design | Read the script; tried argument forms, a subdirectory, an unmatched path, output capture, PowerShell 5.1, and timing | Results quoted in findings; 385 ms for the 17-file scope | ANALYSIS-001 to -005 |
| Test design | Read the test; filtered `dotnet test --no-build` | Passed 1/1 in 5 s | ANALYSIS-006. I did not confirm the binaries were built from the current source. |
| Formatting | `./scripts/format.ps1 -Verify`, then `dotnet format --verify-no-changes` to list the files | 16 files, all in `src/BellTower` (unrelated in-progress work) | Scoped `.cs` file is clean |
| Match with `docs/development.md` | Compared role Project facts with development.md lines 15–47 | Commands and layer list match | No drift. Roles leave out development.md's "after restoring dependencies" step; not raised as a finding. |
| Product architecture | Not applicable | No product source in scope | Not applicable |

### Measurements

File sizes in bytes, baseline b3e007e → current:

- `AGENTS.md`: 3585 → 3582
- Workflow: 11228 → 10569
- Roles:
  - developer: 1679 → 4450
  - qa: 2419 → 4837
  - code-analyzer: 2008 → 4467
- Templates:
  - qa-report: 2621 → 2681
  - analysis-report: 2223 → 2432
  - findings: 3076 → 3691
- Codex TOMLs (dev / qa / analyzer): 730 / 860 / 863 → 487 / 576 / 663
- Claude adapters: none → 452 / 554 / 606
- `docs/agent-workflow.md`: 9194 → 3843

Instruction bytes loaded per launch. Baseline counts adapter + AGENTS + workflow + role + template; current counts adapter + auto-loaded AGENTS + role + template.

| Launch | Baseline (Codex) | Current (Claude / Codex) | Change |
| --- | --- | --- | --- |
| developer | 17222 | 8484 / 8519 | -51% |
| qa | 20713 | 11654 / 11676 | -44% |
| code-analyzer | 19907 | 11087 / 11144 | -44% |
| orchestrator (AGENTS + workflow + findings template) | 17889 | 17842 | ~0% |

## Findings

### ANALYSIS-001: The documented multi-path form of `-Paths` fails

- Severity: warning. Related: 2.3, D10.
- Locations:
  - `scripts/review-identity.ps1:1-5`: `[Parameter(ValueFromRemainingArguments = $true)] [string[]]$Paths`
  - Usage text in `.agents/roles/qa.md:26`, `code-analyzer.md:27`, `developer.md:31`, `.agents/workflows/openspec-delegation.md:35`, `docs/agent-workflow.md:43`
- What I observed:
  - In-process `-Paths AGENTS.md docs/agent-workflow.md` fails with "A positional parameter cannot be found that accepts argument 'docs/agent-workflow.md'". `pwsh -File` gives the same error, and the Bash pipeline still exits 0.
  - `pwsh -File ... -Paths "AGENTS.md,docs/agent-workflow.md"` treats the string as one literal path and prints `files=0 diff=e3b0c44298fc`, even though AGENTS.md is modified.
  - Only two forms work: an in-process comma list, or positional paths after `-Baseline`. The test uses the positional form only (`ReviewIdentityScriptTests.cs:80-81`).
- Proposed fix: pick one form that works in every shell and use it in all five usage texts. Either document `-Baseline <c> <path> <path>...`, or split comma-joined values inside the script.
- Risk: none for identities already recorded.
- Evidence type: measured.
- How to verify: run the documented form in-process, via `pwsh -File`, and from Bash; the summaries should match. Cover it in the test (ANALYSIS-006).

### ANALYSIS-002: Wrong or unmatched paths silently report a clean target

- Severity: warning. Related: 2.3, D10.
- Location: `scripts/review-identity.ps1:45` (`Push-Location $root`) and lines 49-60.
- What I observed:
  - Paths are resolved against the repository root, not the caller's directory. From `docs/`, `-Paths agent-workflow.md` prints `files=0` even though the file is modified.
  - A path that does not exist also prints `files=0` with exit 0.
  - When this happens, the before and after runs match trivially, so the check that the reviewer changed nothing passes without testing anything.
- Proposed fix: exit non-zero when a path matches nothing in the working tree, the index or the baseline. A `Test-Path` check per path, or `git cat-file -e <base>:<path>` for deleted files, is enough. Otherwise resolve paths relative to the caller, or state that they are root-relative.
- Risk: an intentionally empty scope would need an explicit allowance.
- Evidence type: measured.
- How to verify: add a test that an unmatched path fails and a test run from a subdirectory.

### ANALYSIS-003: The output cannot be captured or compared within PowerShell

- Severity: warning. Related: 2.3, D10 ("run it before and after and compare").
- Locations:
  - `scripts/review-identity.ps1:87`: `[Console]::Out.Write(($lines -join "`n") + "`n")`
  - Lines 22-29: the diff child process's stderr is not redirected.
- What I observed:
  - `$x = ./scripts/review-identity.ps1 ...` captures 0 lines. `| Select-String` and `| Out-Null` do not filter the output either.
  - The child's "LF will be replaced by CRLF" warnings (12 lines in this checkout) get past `2>$null`. Each run therefore prints 12-24 lines of noise.
- Proposed fix: emit lines with `Write-Output` and redirect or discard the child's stderr, or pass `-c core.safecrlf=false`. `$a`/`$b` plus `Compare-Object` then work, and reviewers spend less context on noise.
- Risk: the hashes do not change.
- Evidence type: measured.
- How to verify: `$a=<script>; $b=<script>; Compare-Object $a $b` shows no differences and no warning lines.

### ANALYSIS-004: The script needs PowerShell 7 but does not say so

- Severity: suggestion. Related: 2.3.
- Location: `scripts/review-identity.ps1:25` (`ArgumentList`) and line 84 (`SHA256::HashData`).
- What I observed: under `powershell.exe` 5.1 the script fails with "You cannot call a method on a null-valued expression".
- Proposed fix: add `#Requires -Version 7.0`, and optionally mention `pwsh` in `docs/agent-workflow.md:43`.
- Evidence type: measured.

### ANALYSIS-005: The hashed diff is not pinned against git settings

- Severity: suggestion. Related: 2.3, D10 (stable output), 4.1 (hand-off between clients).
- Location: `scripts/review-identity.ps1:52` and line 66.
- What I observed:
  - The manifest uses `--no-renames` but the hashed `git diff --binary` does not. With the default `diff.renames=true`, the hash represents renames while the manifest lists a delete plus an add.
  - User git settings such as `diff.renames`, `diff.noprefix`, `diff.mnemonicPrefix`, `diff.relative` and `color.diff=always` can change the hashed bytes for the same tree. That matters when one environment compares against another's recorded identity. Runs in the same environment are not affected.
  - This checkout has no such settings, so nothing is wrong here today.
- Proposed fix: add `--no-renames --no-color --src-prefix=a/ --dst-prefix=b/` to the hashed diff.
- Risk: identities recorded before the change will not match after it; note this in the round that adopts it.
- Evidence type: source reading; the differences between environments are a hypothesis, not measured.
- How to verify: add a test with a renamed file and `diff.noprefix=true`, and assert the hash does not change.

### ANALYSIS-006: The test skips the documented form, added files and failure cases

- Severity: suggestion. Related: 2.3.
- Location: `tests/BellTower.Tests/Scripts/ReviewIdentityScriptTests.cs:30-83`.
- What it already does well: covers modified, staged, untracked, deleted and out-of-scope files, stability across two runs, and checks that the diff and untracked hashes respond independently. It passed in 5 s.
- Gaps:
  - It never uses the documented `-Paths` form, so it misses ANALYSIS-001.
  - It has no added-file (`A`) case.
  - It has no unmatched-path case (ANALYSIS-002).
  - The disposable repository inherits the user's global git config, such as `core.hooksPath` or `diff.*` settings.
- Proposed fix: cover all of the above. Set `GIT_CONFIG_GLOBAL` and `GIT_CONFIG_NOSYSTEM` to empty or disposable values in `Execute`.
- Cost: about 0.4-1 s per extra `pwsh` run.

### ANALYSIS-007: The adapters repeat role rules, against D1 and the spec

- Severity: warning. Related: 1.1, 1.2; D1 ("Ownership, no-nesting, and return rules live in the role file, not the adapter"); spec ("Neither client's adapter SHALL duplicate or diverge from the shared role policy").
- Locations: `.claude/agents/{developer,qa,code-analyzer}.md:7`, `.codex/agents/developer.toml:5`, `qa.toml:5`, `code-analyzer.toml:6`.
- What I observed:
  - All six adapters end with `Do not spawn subagents, and do not commit.`, which repeats the role files and `AGENTS.md:19`. They also summarize what the role file contains.
  - The wording already differs between clients: the Claude analyzer says "The role file defines", while the Codex analyzer says "It defines", which reads ambiguously after the template clause.
- Proposed fix: make each body a pure pointer ("Follow `.agents/roles/<role>.md` from the repository root, and write the report in the `.agents/templates/<report>.md` format."), with identical text for both clients. If the extra reminder is wanted as a safety layer, record it as a documented exception to D1. Saves about 150 bytes per launch.
- Risk: low.
- How to verify: `grep "spawn\|commit"` on the adapters finds nothing, and the bodies are identical across clients.

### ANALYSIS-008: Concrete model defaults are copied outside the adapters

- Severity: warning. Related: 2.2, 2.6, D2, D7; spec ("one authoritative location").
- Locations: `.agents/workflows/openspec-delegation.md:66`, which lists every value (`sonnet`, `opus`, `gpt-6-luna`, `gpt-6.1-sol` with `high`), and `docs/agent-workflow.md:26`, which lists the Claude values.
- What I observed: D2 makes the adapters the authoritative place for these values. The Codex IDs have not been probed natively, so they are the values most likely to change, and a change would need three edits.
- Proposed fix: describe the tiers in prose and link to `.claude/agents/` and `.codex/agents/` for the values. Keep the docs-only `sonnet` override, since that is policy rather than a default.
- How to verify: `grep -rn "gpt-6\|sonnet\|opus" AGENTS.md .agents docs/agent-workflow.md` finds only the override sentence.

### ANALYSIS-009: AGENTS.md repeats workflow and role rules and is loaded into every specialist

- Severity: suggestion. Related: 2.4, D7.
- Location: `AGENTS.md:17-21` (1.6 KB).
- Rules it repeats:
  - Developer ownership (`developer.md:7`)
  - Reviewer write limits and screenshots (`qa.md:9`)
  - Ledger ownership and fix routing (`openspec-delegation.md:55,59-60`)
  - Specialists must not commit (four other places)
  - What to do when delegation is unavailable (`openspec-delegation.md:9`)
- What I observed:
  - Claude loads AGENTS.md into native subagents (seen in this run), so every launch pays for these copies.
  - The section starts with an unqualified "Read `.agents/workflows/openspec-delegation.md` before repository work". The exemption for specialists comes two sentences later.
- Proposed fix: cut the section to three sentences:
  - "Orchestrator (main agent in Claude Code or Codex): read the workflow before repository work."
  - "Specialist subagents: follow your role file in `.agents/roles/`; do not read the workflow."
  - The sentence about skill authorization boundaries.
- Saving: about 1 KB per launch (9-12%). The parity section is not affected.
- Risk: a session without the workflow loses the summary; the workflow is mandatory for the orchestrator.
- How to verify: compare `wc -c` before and after, and confirm the parity section diff is empty.

### ANALYSIS-010: The three roles already give different live-instance safety rules

- Severity: warning. Related: 2.1, D7; spec scenario "A specialist needs the live Bell Tower instance".
- Locations: `.agents/roles/developer.md:24-25`, `qa.md:22`, `code-analyzer.md:20`.
- What I observed:
  - Only QA forbids replaying real approvals, playing physical audio and sending production messages, and only QA says to record live-only checks as not performed.
  - Only the developer names `~/.claude`, `~/.codex` and "other personal configuration". It says "prefer" silent audio sinks rather than forbidding physical audio.
  - The analyzer has the shortest list.
  - The developer and analyzer run checks too, so these are real gaps, not role tailoring.
- Proposed fix: use one identical bullet in all three roles, covering everything any role currently lists, plus "record a check that needs the live instance as not performed".
- Risk: none; this only tightens the rules.
- How to verify: the safety lines are byte-identical across the three roles.
- Relationship: an instance of ANALYSIS-011.

### ANALYSIS-011: Shared project facts are copied into all three roles, against "one authoritative location"

- Severity: suggestion, decision needed. Related: 2.1; spec "Shared entry point" (self-contained roles vs "Each rule SHALL have one authoritative location").
- Location: the Project facts sections of all three roles (about 1.3-1.6 KB each).
- What I observed:
  - For shared facts, the two spec requirements conflict, and the implementation resolved it by copying.
  - The copies have already drifted: the safety lists differ (ANALYSIS-010), QA has no layer list, and the parity wording differs.
- Options:
  - (a) Move the shared facts to `.agents/roles/project-facts.md` and have each adapter name it. Bytes per launch stay the same and there is one source, but the spec's "role file and template alone" wording needs amending.
  - (b) Keep the copies but make them byte-identical with a small comparison check, and amend the spec to allow the exception.
- Recommendation: (a), since it matches D7's intent.
- Relationship: covers ANALYSIS-010.

### ANALYSIS-012: The workflow and findings template still overlap with each other and the roles

- Severity: suggestion. Related: 2.2, 2.5; task 2.2 check ("each moved rule appears once").
- Overlaps found:
  - Finding states and transitions: workflow lines 57 and 62, and template line 47.
  - Reservation and no-reuse rules: workflow line 28 and template line 20.
  - Marker procedure: workflow lines 72-73 and template line 7.
  - Inside the workflow: line 51 and line 82 repeat each other on fixtures, native and live evidence.
  - Workflow line 82 repeats the restriction-evidence list from `AGENTS.md:10`.
  - Specialist-facing rules still in the workflow: line 35 (reviewers run the helper before and after; enforcement claims) and line 51 (no-findings reports; hypotheses are not measurements). These duplicate `qa.md:12,26,28` and `code-analyzer.md:8,13,27-28`.
- Size effect: the workflow shrank by only 659 bytes (5.9%), so the orchestrator's load is unchanged.
- Proposed fix:
  - Keep the state machine and allocation rules in the workflow, and reduce the template to field guidance plus links.
  - Rephrase lines 35 and 51 as orchestrator checks.
  - Replace the list at line 82 with a link to AGENTS.md.
  - Estimated saving: 1-1.5 KB of orchestrator context; not measured.

### ANALYSIS-013: Reviewer roles give their write limits as both a forbidden list and an allowed list

- Severity: suggestion. Related: 2.1.
- Locations: `.agents/roles/qa.md:8-9` and `code-analyzer.md:9-10`. The behavioral-not-enforced point also appears twice (`qa.md:9` and `:12`, and the same pair in the analyzer role).
- What I observed: the allowed list already implies the forbidden list.
- Proposed fix: keep the allowed list plus "Request any other change through findings." Saves about 250 bytes per reviewer launch.
- Risk: weaker models may benefit from the explicit forbidden list. Task 3.5 can confirm the trim does no harm.

### ANALYSIS-014: The analyzer's reasoning effort is set for Codex but not for Claude

- Severity: suggestion. Related: 1.1, 1.2, D2; parity.
- Locations: `.codex/agents/code-analyzer.toml:4` (`model_reasoning_effort = "high"`), `.claude/agents/code-analyzer.md` (no `effort`, although D2 lists it as a supported field), and `openspec-delegation.md:66` (documents `high` for Codex only).
- What I observed: analyzer depth differs between the clients, and no reason is recorded.
- Proposed fix: either add `effort: high` to the Claude frontmatter (check that 2.1.309 accepts it), or remove the Codex key and raise effort per assignment. Record the choice either way.

### ANALYSIS-015: Design D3 and Risks contradict D7

- Severity: suggestion. This is a planning artifact, so it is the orchestrator's to fix.
- Location: `design.md`. D3 says "Adapters also tell specialists to read `AGENTS.md` directly", and Risks says "adapters that read `AGENTS.md` explicitly". D7 says "Adapters no longer tell specialists to read `AGENTS.md`".
- What I observed: the implementation follows D7, so the mitigation Risks lists for a future `CLAUDE.md` without the import no longer exists.
- Proposed fix: name the import rule plus task 4.4 as the mitigation. Optionally add a check that fails when a `CLAUDE.md` exists without `@AGENTS.md`.

## Re-verification

None (first round).

## Assessment and limitations

- Coverage: all scoped files and their neighbors were read in full.
- Context cost: D7's goal of smaller specialist launches is met (-44% to -51% in bytes). The orchestrator's load is unchanged.
- Most important: ANALYSIS-001 to -003. The identity helper can report a target as clean when the paths were wrong, and its output cannot be compared within PowerShell. Every review round in both clients depends on this identity.
- Duplication findings (ANALYSIS-007 to -012) are drift risks. ANALYSIS-010 is the exception: the safety rules have already drifted.
- Not measured: token counts; differences between environments (ANALYSIS-005); Codex behavior (this round covered Claude only). I ran the script only through the PowerShell and Bash tools available in this session.
- For task 4.4: this native Claude Code 2.1.309 subagent had AGENTS.md loaded when it started.
- Unexpected writes: none. The only output is the normal test run output and the disposable test workspace.
- Decisions needed:
  - ANALYSIS-011: a shared facts file vs checked copies.
  - ANALYSIS-007: pure pointer vs a documented exception.
  - ANALYSIS-014: whether analyzer effort should match across clients.
  - Report persistence: the harness blocks subagent report writes, which conflicts with the role contract.
