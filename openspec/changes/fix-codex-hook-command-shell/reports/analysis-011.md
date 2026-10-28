# Analysis report: fix-codex-hook-command-shell / 011

## Review target

- Reviewer / role: analyze_fallback_008 / code-analyzer.
- Client / version / mode / model (resolved ID): Codex host version unavailable / delegated Default-mode light static recheck / assigned `gpt-6.1-sol`, high effort; runtime model self-inspection unavailable.
- Date: 2026-10-28 UTC.
- Change root / assignment: `openspec/changes/fix-codex-hook-command-shell`; recheck canonical ORCH-001 / duplicate QA-500 only. New allocation ANALYSIS-500 through ANALYSIS-549; no suggestions. Analysis010 produced no file because automatic approval review hit its usage limit; its allocation is not reused.
- Task and requirement references: [task5.1](../tasks.md), [Completion012](../evidence/completion-docs-012.md), [QA005](qa-005.md), [Analysis005](analysis-005.md).
- Scope and relevant neighboring paths: corrected benchmark review-status sentence at `docs/hook-ingress.md:64`, linked reviews and completion record. No runtime/source implementation review or changes.
- Baseline / HEAD: `aadbfb366bd24906a8e477d09957d08c5ac6bf3d`.
- Implementation identity: standard22-path manifest is unchanged from the exact list in [Analysis009](analysis-009.md#review-target). Tracked diff SHA-256 `659f1706b2bcbd06dbee022b256f2518d098a3dd7f2581a3b35ca28b6639e3c5`; untracked reporter `b637c3ea13bc6aa75857a7cb0eaf09b0b191a349df9a78007dd1d2b2f6f92611`, probe `3e6a758b76fe766d46b142ea9d9adb0c4a81ce6d4beecff907c24a0919657a6e`, reporter tests `fd6def19a0c117acd90bb173695f210ea520db165210fb348d6e19d6080cc7eb`.
- Before/after command: `./scripts/review-identity.ps1 -Baseline aadbfb3 -Paths src tests docs/compatibility.md docs/follow-ups.md docs/hook-ingress.md docs/installation.md tools/BellTower.Ingress.Bench`.
- Before/after summary: `head=aadbfb3 baseline=aadbfb3 files=22 diff=659f1706b2bc untracked=3/cce5a39616ce`; matched, including full hashes and manifest.
- Reviewed file hashes: hook-ingress `CC5C538E0E6A7490E13CDFC1493EFAE15D9813F041CD1732B675325BD77061EB`; Completion012 `A31C9772D75D872EB4C9B148943F0F9556B5A0EE4FCAC1C173BDACD7AFB2FD6C`.
- Prior reports / rechecked findings: [Analysis009](analysis-009.md), [QA011](qa-011.md), [QA012](qa-012.md); ORCH-001 / QA-500.

## Analysis performed

| Area | Method / command | Evidence | Outcome / limitation |
| --- | --- | --- | --- |
| Exact correction | Read paragraph and Completion012; reverse the single corrected passage in memory and hash reconstructed UTF-8 bytes | Prior doc hash reconstructs as `3CDD064D129A5091E79DDDCAD9DF57D26384C4172E84D4FE9A4FAA71969AD3BD`, matching Analysis009 | Passed; all other text and numeric values unchanged |
| Factual review status | Inspect linked QA005/Analysis005; verify both link targets exist | Both independently recomputed literal-echo trial002 statistics and reviewed arithmetic/guards | Passed; completed-review claim substantiated |
| Acceptance boundaries | Read corrected paragraph and linked reviews | Synthetic benchmark remains distinct from authenticated native-provider/user acceptance; negative estimate and historical/raw guards retained | Passed; no broadened acceptance or new measurement claim |
| Identity / hygiene | Before/after identity and file hashes; `git diff --check`; targeted scan of this owned report | Matching source/evidence target; no whitespace errors or targeted personal/secret patterns | Passed |
| Build / tests / native probes | Not run | One factual documentation correction; QA011 already reports matrix6/6 and default1005/1005 | Not performed; unchanged functional evidence carried forward |

## Findings

**No findings.** No ANALYSIS-500 through ANALYSIS-549 IDs assigned.

## Re-verification

| Finding ID | Corrected target | Independent evidence | Outcome |
| --- | --- | --- | --- |
| ORCH-001 / QA-500 duplicate | `docs/hook-ingress.md:64`, hash above | Obsolete pending-review claim replaced by accurate links to independent arithmetic/guard verification; synthetic acceptance limitation preserved | Verified correction; orchestrator owns ledger status |
| ANALYSIS-350 and QA-003 | Unchanged functional/evidence paths | Analysis009 verified the inference correction and genuine cmd capture/current command reproduction; QA011 independently audited native evidence | Carried forward, not repeated |

## Assessment and limitations

The sole assigned discrepancy is corrected. The benchmark numbers and interpretation remain unchanged, and the new wording accurately records completed independent review without implying authenticated native-provider or user acceptance.

Analysis009's native cmd proof, narrow local Windows Bash non-applicability, external-host Bash limitation and ANALYSIS-350 correction remain applicable. QA011's default1005/1005 and matrix6/6 results carry forward at their original scope; no build, tests or native process was repeated. The installed0.1.9018 package remains unchanged, and later passing tests do not erase or diagnose historical failures.

Only this assigned report was written. Product identity and reviewed hashes matched before/after. No source, tests, settings, installation, plans, ledger or live session was changed. No issue remains within this bounded recheck; task/ledger reconciliation and whole-change completion remain the orchestrator's responsibility.
