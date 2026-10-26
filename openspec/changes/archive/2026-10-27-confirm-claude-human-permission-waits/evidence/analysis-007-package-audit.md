# Analysis 007 package inventory audit

Audited the actual ZIP and sidecars for development candidate `0.1.9012` without executing product code or touching the installed application. All checks used default workspace permissions.

| Check | Observed result |
| --- | --- |
| Archive | `artifacts/dev-builds/bell-tower-0.1.9012-win-x64.zip`, 108031413 bytes |
| SHA-256 | `f3e69b18f5ae8d414f23d8cd6f8812856409b8a8846cd2e652c1439cd609fcad` |
| Sidecar checksum / descriptor archiveHash | Both match independently computed ZIP hash |
| ZIP entries | 27; no duplicate names, rooted paths, backslash paths or parent traversal |
| Declared version-payload files | 23; all present and each SHA-256 matches the release contract |
| Undeclared version-payload files | Zero, excluding the expected embedded `release.json` |
| Embedded release contract | Semantically identical to the sidecar descriptor's contract |
| Contract | version `0.1.9012`, runtime `win-x64`, self-contained; layout/bootstrap/state/runtimeRecords/shell/bridge/capture/attachmentHealth all `1` |
| Development build marker | channel `dev`, commit `754aed5`, dirty `true`, builtAt `2026-10-26T20:24:20Z`, baseVersion `0.1.8` |
| Binary version resources | Launcher, application and ingress: FileVersion `0.1.9012.0`; ProductVersion `0.1.9012-dev+754aed5.dirty.754aed525415d0cb3f47e8b6064e91fe1abdc618` |
| Source document linkage | All 19 packaged README/documentation hashes match the current source files |
| VSIX linkage | Version `0.1.9012`; packaged extension.js/service.js/focus.js all match current source hashes |
| PowerShell helper linkage | Packaged Invoke-BellTower.ps1 hash matches current source |

Methods: `Get-FileHash` on ZIP/current source; read-only `ZipFile.OpenRead` entry traversal and SHA256 over each declared entry stream; parse embedded and sidecar JSON; FileVersionInfo on the already-produced package-layout artifacts; read the nested VSIX archive and hash its script streams. No production operation manifest or data directory was read.

The file inventory covers the immutable version payload. The three bootstrap entries (`bell-tower.exe`, `installation.json`, `current.json`) and embedded release contract are also covered by the independently verified whole-archive checksum. This is a hash-integrity/development-package audit, not a signature or publisher-authentication claim.

Source linkage is consistent with the reviewed frozen dirty tree and the orchestrator's controlled build provenance. The build marker identifies its base commit and dirty state; it does not embed the full dirty-tree hash or establish a reproducible-build proof by itself. The reviewed implementation identity remains `head=754aed5 baseline=754aed5 files=62 diff=518bc5fdeb23 untracked=15/c0940d0a9277`.

Capture-v1 remains the declared contract: CaptureProtocol.Version is 1 and the packaged ingress source's protocol-info response advertises protocol/minimumConsumer/maximumConsumer 1. The ingress project requires NativeAOT and packaging fails rather than choosing a managed fallback. Shared HookAdapter emits human-wait hook fields; it does not obtain RuntimeApprovalScope or RuntimeApprovalRevision from provider input. Those scope/revision values are internal pump observations, with source-generated protocol metadata and additive snapshot fields. CaptureConsumer retains legacy normalization and unknown-field preservation. QA owns actual packaged CLI and current/older-helper smoke execution; this audit does not duplicate it.

Installing this candidate and verifying its version are separate from live approval-flow acceptance. The earlier Analysis 006 simulated checks and QA 006 full suite remain applicable to the unchanged source, with their original provenance and limitations.
