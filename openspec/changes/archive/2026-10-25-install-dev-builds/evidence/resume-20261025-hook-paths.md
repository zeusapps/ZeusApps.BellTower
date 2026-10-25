# Local hook path observation — 2026-10-25

Read-only inspection on user-authorized resumption. Raw provider settings and decoded commands were kept in memory and not copied into evidence.

- Claude Code: `%USERPROFILE%/.claude/settings.json`, 12 configured event entries.
- Codex: `%USERPROFILE%/.codex/hooks.json`, 10 configured event entries.
- Every inspected hook uses an encoded PowerShell command. Decoded command classification identifies `bell-tower.exe`, no `bell-tower-ingress`, no direct versioned payload path; configured timeout is three seconds.
- Both clients therefore still use the legacy desktop-launcher hook path. Building the NativeAOT helper did not migrate these settings. This is a local configuration observation, not a reproduced work-machine timeout or proof of its sole cause.
- No setting, selection, tray or user process was changed. No hook was invoked during inspection.

Method: read the hooks JSON, iterate event/matcher/hook entries, decode `-EncodedCommand` with UTF-16LE in memory, emit only event names, timeout and boolean path classifications. Neither decoded command contents nor user-private values were emitted.

A follow-up read-only preview using selected 0.1.10 succeeded (exit 0, no stderr). Selection, integration ownership and both provider settings hashes were unchanged afterward. The selected release declares capture consumer 1 and includes `bell-tower-ingress.exe`; helper SHA-256 matches its release inventory, and `--protocol-info` returned protocol/minimumConsumer/maximumConsumer 1.

`setup --clients both` previews three files (integration ownership, Claude settings, Codex hooks), replacing 22 owned legacy entries and installing 22 native entries. No desktop/profile/footer change was requested. The helper would be provisioned at an immutable hash-named location. Summary: [preview](resume-20261025-hook-preview.txt). User authorization for real `setup --apply` was requested asynchronously; no apply has run.

## Authorized hook migration — 2026-10-25

User explicitly approved: "Apply the hook migration now (Recommended)". Ran installed 0.1.10 `setup --clients both --data-dir <existing application data> --apply` after rechecking selection, capture contract and helper hash. Exit 0/no stderr; three planned files configured. All 12 Claude and 10 Codex Bell Tower-owned hooks now reference native ingress; all retain timeout 3 and no encoded PowerShell hooks remain. Selection bytes unchanged; no tray restart or user process stop.

Backups: `%LOCALAPPDATA%/ZeusApps.BellTower/integration-backups/20261025-073807-6fcc162c` (three files). [Apply result](resume-20261025-hook-migration.json).

Configured-command checks used actual Git Bash/Claude and cmd/Codex commands with only their final data-directory argument changed to disposable artifacts. Individual and mixed pair: 4/4 exit 0, neutral response, empty stderr, 74–109 ms, four durable captures. [Corrected smoke result](resume-20261025-native-hook-check-2.json). Initial harness accidentally replaced the data-directory prefix in the helper image path too, causing file-not-found; [initial failed harness result](resume-20261025-native-hook-check.json) is retained, and is not a product timing failure. No synthetic event was delivered to live data. This checks the installed 0.1.10 helper, not final dev-package acceptance or authenticated field behavior. Existing clients may need to reload hooks or restart.
