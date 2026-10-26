# Analysis report: confirm-claude-human-permission-waits / 007

## Review target

- Reviewer / role: delegated Codex analyzer / code-analyzer.
- Client / version / mode / resolved model: Codex, local CLI previously observed as 0.164.0 (active host build unavailable), Default/workspace-write, `gpt-6.1-sol` high (role default).
- Date: 2026-10-26 UTC.
- Change root / assignment: `openspec/changes/confirm-claude-human-permission-waits`, Analysis 007 package/evidence audit. Report-only; no new implementation review cycle.
- Task / requirement references: remaining provider-contract evidence 1.1, 2A.5, 4.2; packaged compatibility and native/live acceptance 3.4/4.1; [attention-notifications](../specs/attention-notifications/spec.md) and [session-tracking](../specs/session-tracking/spec.md).
- Baseline and HEAD: `754aed525415d0cb3f47e8b6064e91fe1abdc618`.
- Before identity: `head=754aed5 baseline=754aed5 files=62 diff=518bc5fdeb23 untracked=15/c0940d0a9277`, from `./scripts/review-identity.ps1 -Baseline 754aed5 -Paths src tests docs README.md`. Full diff SHA-256: `518bc5fdeb23904a21470c7e627ef5c1352978e40d45d8974c56cd8012dc9f1b`.
- After identity: unchanged summary, full tracked hash, manifest and untracked hashes. [Retained identity](../evidence/analysis-007-target-after.txt). Change tracking, reports and evidence are outside this source identity.
- Package: `artifacts/dev-builds/bell-tower-0.1.9012-win-x64.zip`; SHA-256 `f3e69b18f5ae8d414f23d8cd6f8812856409b8a8846cd2e652c1439cd609fcad`.
- Scope: actual ZIP/sidecar/embedded contracts and payload inventories; current native ingress/consumer compatibility and source provenance; [provider evidence 008](../evidence/implementation-008-provider-coverage.md), [native evidence 009](../evidence/implementation-009-tui-contract.md) and its sanitized JSON. No production operation manifest or default data directory was read. No installation/session/configuration actions performed.
- Prior review carry-forward: [Analysis 006](analysis-006.md), 62 independent assertions on this identical source target; QA 006's 979-test result remains QA evidence, not execution by this analyzer. No full-suite or probe recompilation repeated. Reserved ANALYSIS-350–399 unused.

## Analysis performed

| Area | Method / evidence | Outcome / limitation |
| --- | --- | --- |
| Whole archive identity | Independently computed Get-FileHash; compare checksum and release descriptor | Passed: actual ZIP matches the assigned SHA-256 and both sidecars. |
| ZIP inventory and payload integrity | Read-only ZipFile.OpenRead; hash each declared entry stream; check duplicates and unsafe paths | Passed: 27 entries, all 23 declared payload files present with matching hashes; zero undeclared payload files, duplicate names or unsafe paths. Embedded release.json is the expected extra version entry. |
| Embedded/sidecar contracts | Parse and compare JSON | Passed: semantically identical. Version 0.1.9012, win-x64/self-contained; layout/bootstrap/state/runtimeRecords/shell/bridge/capture/attachmentHealth all 1. |
| Dirty-build/source linkage | Read marker and binary version resources; hash current sources corresponding to packaged docs/helpers/VSIX scripts | Passed for consistency: dev, base 0.1.8, commit 754aed5, dirty=true, builtAt 2026-10-26T20:24:20Z. Launcher/application/ingress version resources agree. All 19 README/docs files, three VSIX scripts and PowerShell helper match source. VSIX version is 0.1.9012. The marker does not encode the full dirty-tree hash or prove reproducible binaries independently. |
| Native helper and capture contract | Inspect ingress project, packaging guard, Program protocol-info and shared protocol/consumer source | Passed by inspection: NativeAOT is required and packaging fails rather than using a managed helper; capture remains version 1. Runtime approval scope/revision is generated internally by the pump, not taken from provider hook input. Shared protocol source-generation metadata includes additive fields; consumer retains legacy normalization. |
| Actual packaged CLI/older-helper behavior | Not executed by analyzer; QA 007 owns the isolated packaged smoke | Not performed here to avoid duplication. This source/byte audit is not a replacement for QA's actual execution. |
| Predecessor and provider field evidence | Read resumption-contract-check, evidence 008 and the newer evidence 009 | Existing predecessor inspection plus actual Claude 2.1.310 auto/default question-hook captures and Codex 0.164.0 standalone Plan permission capture now provide the bounded cross-provider field/timing comparison discussed below. |
| Async availability | Read evidence 009's native blocking positive control, offered schema and same-context async absence | Supports absence only for the tested standalone Plan TUI/custom-provider/fixture-model context. The empty-tool supplementary advertised-slug attempt is correctly excluded. Not evidence of universal version/catalog unavailability. |
| No-daemon signal/parity claim | Read native permission sequence/help and compare current fallback policy | Remains unverified beyond the captured mode. An unanswered prompt plus no Notification for 26.7 s does not prove no future or alternative human-status signal exists. Task 4.2 remains partial. |
| Installation/foreground/user acceptance | Orchestrator reports authorized switch and version/doctor checks; analyzer performs no production actions | Installation consistency is separately reported operational evidence. It does not prove real slow-auto versus human-wait UI/audio behavior. Tasks 3.4/4.1 retain their live acceptance gate. |
| Performance / signatures | No measurements or signature verification | Not performed. This is development-package hash integrity and evidence review, not publisher authentication or a performance result. |

[Detailed package observations and methods](../evidence/analysis-007-package-audit.md) retain the exact integrity results. All analyzer reads/writes used default workspace permissions. No escalation was requested.

## Findings

**No new findings.** No package-integrity, contract mismatch or source-target mutation was found. Existing acceptance gaps remain open; this report does not convert them into proven provider restrictions or approve overall feature completion.

## Re-verification

| Existing task / evidence | Reviewed target | Independent assessment | Outcome |
| --- | --- | --- | --- |
| ANALYSIS-002 | Same source identity as Analysis 006 | No source changes since the 62-assertion recheck; package marker and controlled-build provenance are consistent with that target | Carry forward the Analysis 006 simulated verification explicitly. This audit does not claim new native attribution trials. |
| 1.1 | Evidence 008/009 plus earlier Claude captures | Evidence 009 records actual native Codex 0.164.0 standalone Plan PreToolUse at +366 ms and PermissionRequest at +703 ms, with missing tool_use_id on the latter and no Notification observed while pending. Its private fake-provider model request still exercises the installed native hook path. Combined with retained Claude field/timing captures and predecessor inspection, the earlier missing native field comparison is addressed for these named contexts | Recommend complete for the tested versions/modes, while retaining separate 3.4/4.1 live acceptance. The absence window is an observation, not an unconditional schema guarantee. |
| 2A.5 | Evidence 009 native Plan TUI and earlier app-server/catalog records | Native request_user_input schema and question UI are a positive control in the same custom-provider/fixture-model Plan context where async is absent. The unsupported async operation is paired with the supported blocking fallback. No other model/catalog result is generalized | Recommend complete for this precise version/mode/configuration observation only. Do not describe all Codex 0.164.0 catalogs or surfaces as lacking the tool. |
| 4.2 | Evidence 008/009; shared fallback/diagnostics | No-daemon help proves this invocation avoids the shared background server. PermissionRequest occurs before the unanswered native approval UI; cancellation proves neither a human approval nor automatic-review resolution. No matched standalone auto/human resolution comparison or exhaustive signal-availability restriction was established | Still partial. The implemented silent unverified fallback is supported behavior; confirmed-signal parity is not proved and must not be labelled a hard provider-wide restriction. |
| 3.4/4.1 | Installed candidate 0.1.9012 | Orchestrator-reported installation/version agreement is not a user-operated slow-auto versus human approval trial. Mixed-session fixtures remain separate evidence | Live acceptance pending the actual user report. |

## Assessment and limitations

The development candidate passes this read-only integrity, inventory, metadata/source-consistency and protocol audit. Source identity is unchanged. Native evidence 009 resolves the earlier exact TUI surface gap in 008 for its explicitly tested configuration, and the excluded empty-tool control is handled correctly. It does not establish authenticated inference behavior or general provider capability restrictions.

Current broad diagnostic wording such as async being unavailable at 0.164.0 must be read subject to the narrow evidence and remaining capability gate; evidence 009 must not be used as support for a blanket model/catalog claim. The runtime-less confirmation gap remains open under 4.2. The earlier packaging failure note in tasks.md is historical and needs orchestrator reconciliation with the newly successful package/preflight/switch record; it is not current package evidence.

Candidate installation, production selection preservation and original-byte checks are orchestrator-owned operational observations; this analyzer did not independently access the production operation or reproduce it. Signing, native user acceptance and broader runtime-less signal discovery were not performed. Only the assigned Analysis 007 report/evidence were written; prior reports are unchanged. The orchestrator owns task reconciliation, remaining acceptance and completion decisions.
