# Bell Tower 0.1.11 release notes (draft)

Status: draft prepared for the release commit. Nothing is committed, tagged or published; publication needs the maintainer's separate authorization.

Version 0.1.10 was skipped. It was never published (the number belonged to legacy unmarked trial payloads), so the previous stable release is 0.1.9 and this release follows it directly.

## What is new since 0.1.9

For Claude Code and Codex alike, through shared policy:

- **A verified worktree is kept after a branch switch.** When the working checkout that session work verified changes branch, Bell Tower keeps that checkout and shows its new branch and worktree (labeled "retained") instead of falling back to the launch directory. The branch-bound automatic subject is still cleared. New work, a pin, provider evidence, Clear checkout and Reset release it.
- Native adoption of plain `claude`, `codex --no-daemon` and shared-backend `codex` conversations through hooks, a hook-started tray that stays stopped after your Exit, distinct session colors, local idle reminders, microphone-aware speech, opt-in titles and colors for adopted native sessions, the Codex hook command fix for PowerShell, and bounded ended-session state with a doctor report.

## Upgrading: required steps

1. Verify the download (see "Verify the download"). Then either let the installed tray update (or run `bell-tower update`), or for a manual install extract the whole ZIP into a stable directory.
2. **Rerun `bell-tower setup --host-bridge --apply` after upgrading.** There is no automatic re-provision in this release. Until you do, the owned hook commands keep naming the 0.1.9 ingress image: Codex hooks fail outright wherever Codex runs them through PowerShell, and the post-0.1.9 hook features (native adoption, hook-started tray) stay inactive. `doctor` reports the state as `hookStart.ingressImage` `stale` with this advice. Codex may ask you to trust the changed hooks (`/hooks`); review and accept.
3. **Plain `codex --no-daemon` conversations that were open during the upgrade must be resumed or restarted after the setup rerun.** Conversations 0.1.9 registered keep their old registration until then.

## Known limitations and regressions

- **Known 0.1.11 regression (Codex).** A Bell Tower-launched Codex conversation whose first hook cannot identify the Codex process is registered as shared and loses its later events (ANALYSIS-265; with 0.1.9 a later hook repaired it). Workaround: restart the conversation. A follow-up is planned. Claude Code is not affected; a Bell Tower-launched Claude conversation is repaired by its later hooks.
- **Manual rollback after a 0.1.11 setup (Codex).** After you manually roll back to an older version following a 0.1.11 setup, Codex hooks are lost until you rerun the older version's `setup --host-bridge --apply` and then resume or restart the open Codex conversations (ANALYSIS-240). The automatic rollback of a failed update is not affected: an update never rewrites hook commands, so it never leaves the hooks on the candidate.
- **Rollback to 0.1.9 and retained checkouts.** 0.1.9 reads all 0.1.11 identity data, but it does not know the retained checkout. Its first pass after a rollback records the launch checkout, so upgrading again does not restore the retained state, and conversations 0.1.9 already moved to the launch checkout before an upgrade lose that evidence until new work or a pin (ANALYSIS-186). This is expected, not a regression of the branch fix.
- **Codex explicit native names remain an open parity gap.** A Codex name set outside Bell Tower is read only for Bell Tower-owned runtimes and a shared backend of the resolved Codex home, so other Codex conversations have no explicit native-title tier. Claude Code reads its custom title. This is unfinished work, not a proven provider restriction; a follow-up tracks Codex native-name provenance.
- **Publisher signing is deferred.** Updates are verified by SHA-256 only and are **not publisher-signed**. A signature, and the managed update-check control, move to a later release (decision 2026-11-22).
- Maintainer manual testing: the live checks (foreground activation, resumed identities, real approvals, optional real microphone, hook captures, the two-session and five-conversation desktop trials) were tested by the maintainer on the installed build. That is manual testing with no recorded artifacts, not automated evidence.
- Unverified in this release: fork-time identity parity (ANALYSIS-300, an open parity gap between Claude Code and Codex, not a proven restriction) and the installed-harness soak, bridge-fault, alert and audio scenarios that were descoped for this release (covered by the unit suite and the maintainer's own testing). The [capability matrix](compatibility.md#session-identity-capability-matrix) lists each as unverified or unfinished.

## Verify the download

Download the ZIP and its `.sha256` from the official release, compare `Get-FileHash -Algorithm SHA256 bell-tower-0.1.11-win-x64.zip` with the `.sha256` file, extract the whole ZIP into a stable user-writable directory (for example `%LOCALAPPDATA%\Programs\ZeusApps.BellTower`), then run `bell-tower --version` and `bell-tower --distribution-info` (both report 0.1.11). The hash proves the file matches what was published; it is not a publisher signature.
