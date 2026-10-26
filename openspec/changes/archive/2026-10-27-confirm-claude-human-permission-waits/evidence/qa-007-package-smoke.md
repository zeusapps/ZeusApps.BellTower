# QA 007 packaged smoke evidence

Date: 2026-10-26. All runtime files and data were isolated under ignored `artifacts/permission-package-qa007/`. No installed provider settings, production data, user agents, approvals, or audio were used.

## Candidate integrity

- Candidate: `artifacts/dev-builds/bell-tower-0.1.9012-win-x64.zip`.
- SHA-256: `f3e69b18f5ae8d414f23d8cd6f8812856409b8a8846cd2e652c1439cd609fcad`; computed hash, `.zip.sha256` sidecar, and release descriptor agree.
- The independent [Analysis 007 package audit](analysis-007-package-audit.md) verifies all declared payload hashes, the archive inventory, release contract, file version resources, packaged docs/VSIX/helper linkage, and the unchanged Capture v1 contract.
- The comparison archive `0.1.9011` also matched its sidecar and embedded descriptor hash. Its actual `bell-tower-ingress.exe` was extracted from that archive for the legacy-envelope check.

## Packaged CLI and protocol

Both extracted version payloads ran from the isolated scratch tree with `DOTNET_BUNDLE_EXTRACT_BASE_DIR` directed to its ignored bundle cache.

| Package | `--version` | `doctor --data-dir <scratch>` | ingress `--protocol-info` |
| --- | --- | --- | --- |
| 0.1.9011 | Passed; reports package version and dev marker | Passed; legacy report has no human-wait coverage section | Passed; protocol/minimum/maximum consumer are 1/1/1 |
| 0.1.9012 | Passed; reports package version, source marker and build time | Passed; explicit disposable data reports configured Claude/Codex coverage and no observed captures | Passed; protocol/minimum/maximum consumer are 1/1/1 |

Doctor commands used separate explicit scratch directories. The package application executable was copied byte-for-byte into a scratch runner directory for the bounded consumer runs, so `Installation.ForPayload` did not identify an installed payload or start update discovery.

## Actual packaged ingress and consumer

The 0.1.9011 native Claude ingress accepted fixture `PermissionRequest` and `Notification(permission_prompt)` inputs and wrote Capture v1 envelopes. The request envelope had no operation ID, but retained an operation fingerprint; the notification had neither an operation ID nor fingerprint. Both lacked `adapterRevision`, `humanPrompt`, `questionTool`, `providerDelay`, and `permissionMode`. This is the real older-helper wire shape, not a current capture with fields stripped. The current 0.1.9012 Claude helper emitted revision 1 and marked the notification as `humanPrompt`; its current Codex helper emitted revision 1 for `PermissionRequest`.

The current packaged consumer ran only against explicit disposable stores as `tray --silent --duration-ms 1500 --data-dir <scratch>` and exited on its own with code 0. The data was synthetic; this was not a live-provider or user-session acceptance run.

| Sequence | Result after current packaged consumer |
| --- | --- |
| Actual 0.1.9011 Claude request only | One provisional Permission wait |
| Actual 0.1.9011 Claude request, then `permission_prompt` after 6.3 s | One confirmed Permission wait |
| Same legacy wait, correlated `PostToolUse`, then late `permission_prompt` | Resolution cleared the wait; late prompt left zero waits |
| Current 0.1.9012 mixed Claude/Codex permission inputs | Claude became confirmed after its prompt; Codex stayed provisional without an owning runtime |
| Current Claude `AskUserQuestion` + approval hooks + prompt, mixed with Codex `request_user_input` | Claude remained one confirmed Question, not a Permission; Codex remained a Question; the concurrent Codex Permission remained provisional |
| Current Claude request, correlated `PostToolUse`, then delayed `permission_prompt` | Zero waits; no resurrection |

The provider-delay gap was honored: prompts were emitted at least 6.3 seconds after their corresponding Claude request. An earlier sub-five-second diagnostic attempt intentionally did not serve as acceptance evidence.

## Carry-forward evidence and limits

- [QA 006](../reports/qa-006.md) executed the full Release suite: 979/979 passed on the same frozen source identity. Its fixture coverage includes reducer, provider, persistence, silent restore, question, mixed-provider, and lifecycle regressions.
- [Analysis 006](analysis-006-probe-output.txt) independently retained 62 passing fake-runtime assertions for the candidate-scope correction.
- [Implementation 007](implementation-007.md) reports the Release build and scoped developer formatting on this identity; formatting was the developer's regular scoped formatter run, not a QA verify-only invocation.
- [Implementation 009](implementation-009-tui-contract.md) records the bounded Codex 0.164.0 interactive TUI Plan probe. The positive control offered and opened `request_user_input`; the tested fixture-model tool list omitted `request_user_input_async`. The unresolved on-request permission dialog produced `PreToolUse` followed by `PermissionRequest` without a tool ID; no Notification event was observed during the 26.7-second unanswered interval. This does not establish auto-versus-human resolution behavior or a provider-wide restriction.
- No authenticated live human/automatic approval trial was run for either provider. Tasks 3.4 and 4.1 remain incomplete; task 4.2 remains incomplete because standalone Codex human-confirmation capability has not been proven available or restricted.
