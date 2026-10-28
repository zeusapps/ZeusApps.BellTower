# Authorized development installation and hook migration

The user authorized finishing the shell work, installing the final reviewed candidate and migrating hook configuration by replying "yes. go ahead". Installation followed independent [QA 007](../reports/qa-007.md) and [Analysis 007](../reports/analysis-007.md). This is a development trial, not completed release acceptance.

## Candidate and switch

- Version: `0.1.9018 dev aadbfb3 dirty`.
- Source binding: `head=aadbfb3 baseline=aadbfb3 files=21 diff=959d86f7d758 untracked=3/cce5a39616ce`; full source manifests in ignored `artifacts/codex-hook-shell/continuation-002/`.
- ZIP SHA-256: `fe5e164d189c6e423d6d79c2115a291bbd81f64c6c7cd8716cf540910f327a79`.
- Ingress SHA-256: `82cc115629b60f2d34ace16f91d72009e6eab1e8b28d80eb0ce796d150ad5707`.
- Final `install-dev.ps1 -Action Preflight` passed package integrity, actual application startup, data compatibility and tray ownership checks against original `0.1.9`. The existing installation/data paths were resolved from prior installation records and matched the data root referenced by owned provider hooks.
- `-Action Switch` completed with state `running-candidate`, selected `0.1.9018`, rollback version `0.1.9`, and tray restarted. Agent sessions were preserved by the verified installer. The stable launcher and payloads were managed by that installer; no manual edits or stale/skip switches were used.
- The installed launcher reports running and selected `0.1.9018 dev aadbfb3 dirty`. Both provider settings retained their saved hashes immediately after Switch.

Private operation record: `artifacts/dev-builds/operations/bell-tower-dev-d7476e312ab142ada00b73ef82dbd87d/operation.json`. It contains the actual installation/data paths, snapshots and recovery state. Its directory also holds private migration preview/apply/doctor and integrity records. Personal paths and settings contents are not copied into this evidence.

## Hook migration and verification

The installed launcher previewed and then applied `setup --clients both --migrate-edited-hooks --apply` with the existing selected data directory. Preview selected PowerShell through the isolated native Codex probe. Apply verified both providers and the exact retained helper before provider settings writes.

| Check | Result |
| --- | --- |
| Preview/apply | Both exit 0; three configuration/ownership files, 22 unchanged owned handlers replaced, zero explicitly edited-handler replacements |
| Claude | 12 owned hooks before and after |
| Codex | 10 owned hooks before and after |
| Other settings | Non-hook JSON hashes preserved for both providers |
| Unowned hooks | None present; before/after unowned-hook fingerprints match |
| Backups | Three original-file backups in the selected data root's integration backup area |
| Repeat preview | Exit 0, zero planned provider-file changes; provider bytes remain unchanged |
| Doctor | Exit 0, version 0.1.9018; both providers configured; zero known-broken owned commands |
| Codex shell | Expected PowerShell, current PowerShell, status matches |
| Live delivery | Doctor has historical delivery receipts, including an older Codex receipt; no fresh user-operated Codex/mixed/trust acceptance inferred |

Only provider hooks and their ownership were migrated; the command did not request desktop setup or status-line installation. Existing settings outside hooks were verified unchanged. No agent, terminal or editor was stopped for migration.

## Recovery and remaining acceptance

Rollback to `0.1.9` is available through `scripts/install-dev.ps1 -Action Rollback`, passing the saved operation manifest and its recorded installation/data paths. Rollback was not run. Provider originals are separately preserved by setup's backups.

QA's serial-collection full suite passed 1004/1004; its default parallel run passed 1003/1004 with a three-second legacy PowerShell launcher timeout, which passed 1/1 in isolation. The precise parallel failure cause remains unresolved. Neither the failed mixed-harness startup nor its audited diagnostic mistake counts as mixed acceptance; see [native investigation 004](native-investigation-004.md).

Native Codex cmd/Bash turn dispatch and the user-operated post-migration Claude/Codex mixed-session/chip/trust trial remain unverified. No task is marked complete from this installation alone.
