# Field acceptance harness result

- Result: **PASSED** (61 of 61 checks passed; 0 harness failures; 1 expected-failure checks observed the known defect as predicted)
- Harness: BellTower.FieldAcceptance 1; started 2026-11-25 14:39:53Z, finished 2026-11-25 14:41:26Z
- Operating system: Microsoft Windows 10.0.26300
- Command: `scripts/field-acceptance.ps1 -Scenario upgrade-without-setup -FixtureVersion 0.1.11`
- Historical baseline: published release `v0.1.9` archive `bell-tower-0.1.9-win-x64.zip`, SHA-256 `2739659eb95e4d15ae0d4c8d9c35a69e4947a9b6191edaa5a71291560c29f062` (published `.sha256`: `2739659eb95e4d15ae0d4c8d9c35a69e4947a9b6191edaa5a71291560c29f062`; release descriptor: `2739659eb95e4d15ae0d4c8d9c35a69e4947a9b6191edaa5a71291560c29f062`; verified: yes). Independently matched the published .sha256, release descriptor, GitHub asset digest. Archive, .sha256 and release descriptor are the files published with the release.
- Candidate: **fixture-version candidate** version `0.1.11`, archive `bell-tower-0.1.11-win-x64.zip`, SHA-256 `b693b214590b0db66e2cd2748747f308eb62969584b8722179996ceb67390eca`, built with `scripts/package.ps1 -FixtureVersion 0.1.11 -OutputDirectory artifacts/update-acceptance/packages`.
- Evidence labels: **Fixture** is a guard rule or synthetic payload evaluated without installed packages; **InstalledHarness** is the real packaged launcher, payload and native ingress installed under disposable roots and driven through their commands and installed hook commands with fake provider processes; **Live** is a real provider on a real machine, which this harness never produces (none in this run).

## Production isolation

- Result: **no production change observed**. Before and after the run the harness compared the setup-owned provider, VS Code and Bell Tower files, the whole installation tree, the Bell Tower scheduled tasks and the running production Bell Tower processes (87 files fingerprinted; a production process that disappeared or appeared during the run is a change); directory totals are informational because the user's live tray and agents write there.
- Guarded locations: `%LOCALAPPDATA%\ZeusApps.BellTower`; `%LOCALAPPDATA%\Programs\ZeusApps.BellTower`; `%USERPROFILE%\.claude`; `%USERPROFILE%\.codex`; `%USERPROFILE%\.vscode`; `%APPDATA%\Code`
- Layout violations: none
- Environment violations: none
- PATH entries removed from child environments: 0
- Preflight sweep of harness-owned leftovers from earlier interrupted runs: none found
- info directory-total: `%LOCALAPPDATA%\ZeusApps.BellTower` files 827->827, bytes 142670558->142670556 (informational: live tray and agents write here)

## Checks

### guard-selftest

| Check | Provider | Evidence | Result | Detail |
| --- | --- | --- | --- | --- |
| legitimate-sandbox-accepted | harness | Fixture | pass | A sandbox under the harness artifacts base is accepted (0 violations). |
| sandbox-in-data-root | harness | Fixture | pass | A sandbox placed inside the production data root is refused. |
| sandbox-in-installation-root | harness | Fixture | pass | A sandbox placed inside the production installation root is refused. |
| sandbox-in-.claude | harness | Fixture | pass | A sandbox placed inside the production .claude is refused. |
| sandbox-in-.codex | harness | Fixture | pass | A sandbox placed inside the production .codex is refused. |
| command-production-launcher | harness | Fixture | pass | Running the production installation's launcher is refused. |
| command-production-data | harness | Fixture | pass | A sandbox command pointed at the production data directory is refused. |
| command-missing-data-dir | harness | Fixture | pass | A Bell Tower command without an explicit --data-dir is refused (it would default to the production data directory). |
| command-production-profile | harness | Fixture | pass | setup --home pointed at the user's own profile (which holds ~/.claude and ~/.codex) is refused. |
| environment-production-data | harness | Fixture | pass | A child environment naming the production data directory is refused. |
| environment-production-path | harness | Fixture | pass | A PATH entry inside the production installation is refused (and BuildEnvironment drops such entries from child environments). |

### upgrade-without-setup

| Check | Provider | Evidence | Result | Detail |
| --- | --- | --- | --- | --- |
| baseline-installed | harness | InstalledHarness | pass | Installed the published 0.1.9 archive under the disposable install root; launcher reports 0.1.9. |
| isolation-environment | harness | InstalledHarness | pass | Child processes get a sandbox USERPROFILE, HOME, APPDATA, LOCALAPPDATA, TEMP, CLAUDE_CONFIG_DIR, CODEX_HOME, BELLTOWER_DATA_DIR and a PATH that starts with the fake provider directory; 0 PATH entries inside production locations were dropped; every Bell Tower command also passes --data-dir. Two effects reach outside the sandbox: the on-demand tray-start task setup registers in the user's Task Scheduler, and, by product design, a tray started by a hook, which runs as a child of the desktop shell with the real profile environment (USERPROFILE, APPDATA, LOCALAPPDATA, TEMP and any global CODEX_HOME or CLAUDE_CONFIG_DIR). The harness exits that tray right after its probe and restarts it silently under the sandbox environment (the hook-started-tray-replaced check verifies both steps), and an instance that has both a hook-started tray and a shared-backend Codex session is refused and its trays ended. |
| baseline-setup | mixed | InstalledHarness | pass | 0.1.9 setup --clients both --apply wrote owned hooks into the fake provider homes (ingress image d166813f8a1e). |
| baseline-tray | mixed | InstalledHarness | pass | 0.1.9 tray started silently for the disposable data directory. |
| baseline-seed | claude | InstalledHarness | pass | 0.1.9 ingress accepted the hooks of two claude sessions (through Git Bash (bash.exe)) and 0.1.9 rename stored name, change association, working checkout and color pin #336699 for one; the other keeps its automatic name and palette. |
| baseline-seed | codex | InstalledHarness | pass | 0.1.9 ingress accepted the hooks of two codex sessions (through the command shell (cmd.exe), the modeled cmd fallback: the 0.1.9 command form does not parse under PowerShell) and 0.1.9 rename stored name, change association, working checkout and color pin #993366 for one; the other keeps its automatic name and palette. |
| baseline-data | mixed | InstalledHarness | pass | 4 identity stores written by 0.1.9 (claude:claude-pinned #336699, codex:codex-pinned #993366, claude:claude-auto #6b2bab, codex:codex-auto #58ab2b). |
| upgrade-activated | mixed | InstalledHarness | pass | The candidate payload was staged and activated by the real update transaction (state activated); the launcher now reports 0.1.11. The tray was stopped, so the update did not start one: no tray running. |
| setup-not-rerun | mixed | InstalledHarness | pass | setup was not rerun: the Claude Code and Codex hook settings are byte-identical and still name the 0.1.9 ingress image; the 0.1.9 image file is retained: True |
| candidate-tray | mixed | InstalledHarness | pass | The candidate tray started silently against the data the 0.1.9 tray wrote. |
| candidate-image-differs | mixed | InstalledHarness | pass | The candidate payload ships a different ingress image (64db9c9594b0) than the 0.1.9 image the hooks name (d166813f8a1e). |
| stale/doctor-hook-image | claude | InstalledHarness | pass | doctor hookStart.ingressImage.claude: status stale, recorded image d166813f8a1e, advice "rerun setup --host-bridge --apply; Codex may ask to trust the changed hooks". |
| stale/doctor-hook-image | codex | InstalledHarness | pass | doctor hookStart.ingressImage.codex: status stale, recorded image d166813f8a1e, advice "rerun setup --host-bridge --apply; Codex may ask to trust the changed hooks". |
| stale/doctor-availability | mixed | InstalledHarness | pass | doctor hookStart.availability: needs a setup refresh: rerun setup --host-bridge --apply; Codex may ask to trust the changed hooks |
| stale/captures-accepted | claude | InstalledHarness | pass | 7 of 7 installed claude hook commands exited 0 with {} through Git Bash (bash.exe) (0.1.9 image). |
| stale/captures-default-shell | codex | InstalledHarness | expected failure (observed) | EXPECTED FAILURE (the known defect, the real U1 outcome for Codex): 0 of 7 installed codex hook commands (0.1.9 image) were accepted through PowerShell 7 (pwsh.exe), the shell Codex 0.164.0 uses by default; 7 failed with a PowerShell parser error. Until setup is rerun Codex hooks fail wherever Codex runs them through PowerShell. First failure: codex:codex-stale SessionStart exit 1 [31;1mParserError: [0m  [31;1m[36;1mLine \|[0m  [31;1m[36;1m[36;1m   1 \| [0m . 2995da3c03fb96af4577a4af03\bell-tower-ingress.exe" --[36;1mprovider[0m  |
| stale/captures-modeled-cmd-fallback | codex | InstalledHarness | pass | Modeled cmd fallback (a model of a Codex whose PowerShell discovery failed, not a claim that Codex falls back to cmd): 7 of 7 installed codex hook commands exited 0 with {} through the command shell (cmd.exe) (0.1.9 image). |
| stale/captures-accepted | mixed | InstalledHarness | pass | Interleaved claude and codex hooks from 6 simultaneous sessions in one launch directory: through each provider's own shell 7 of 14 accepted (claude 7 of 7, codex 0 of 7); modeled cmd fallback for Codex 7 of 7 accepted; the installed tray lists 6 of 6 sessions. |
| stale/native-adoption | claude | InstalledHarness | pass | Native adoption inactive (0.1.9 image): claude started without Bell Tower (hooks through Git Bash (bash.exe)): attachment mode unknown, ownership observed, health verified-live, process identity captured. |
| stale/native-adoption | codex | InstalledHarness | pass | Native adoption inactive (0.1.9 image): codex --no-daemon started without Bell Tower (hooks through the command shell (cmd.exe), modeled cmd fallback): attachment mode unknown, ownership shared-runtime, health unverified, process identity not captured. |
| stale/native-adoption | mixed | InstalledHarness | pass | Both plain sessions are listed together alongside the managed ones; adoption is evaluated per provider by the same shared policy. |
| stale/codex-command-form | codex | InstalledHarness | pass | Repaired Codex hook command inactive (0.1.9 image): the installed 0.1.9 command run through PowerShell 7 (pwsh.exe) exits 1 with a parser error (it only works in the command shell); the candidate's setup writes the PowerShell-safe form. |
| stale/hook-started-tray | claude | InstalledHarness | pass | Hook-started tray inactive (0.1.9 image): a claude hook with the tray stopped left the tray stopped and recorded no start attempt. |
| stale/tray-restored | claude | InstalledHarness | pass | No hook started a tray; the sandbox tray was restarted silently for the next step. |
| stale/hook-started-tray | codex | InstalledHarness | pass | Hook-started tray inactive (0.1.9 image): a codex hook with the tray stopped left the tray stopped and recorded no start attempt. |
| stale/tray-restored | codex | InstalledHarness | pass | No hook started a tray; the sandbox tray was restarted silently for the next step. |
| stale/inactive-features | mixed | InstalledHarness | pass | Until setup is rerun the upgraded hooks keep the 0.1.9 behavior: no native adoption of plain claude or codex --no-daemon, no hook-started tray, and the 0.1.9 Codex command form (fails under PowerShell). See the checks above per provider. |
| stale/baseline-data-preserved | claude | InstalledHarness | pass | 2 identity stores seeded by 0.1.9 (names, change, color pin, checkout pin, palette) read back unchanged through list and from disk. |
| stale/baseline-data-preserved | codex | InstalledHarness | pass | 2 identity stores seeded by 0.1.9 (names, change, color pin, checkout pin, palette) read back unchanged through list and from disk. |
| stale/baseline-data-preserved | mixed | InstalledHarness | pass | All 4 mixed-run identities unchanged; 4 distinct palette accents across them. |
| setup-host-bridge | mixed | InstalledHarness | pass | setup --host-bridge --apply (candidate launcher, --home and --data-dir inside the sandbox) rewrote the owned hooks to the candidate image 64db9c9594b0; the 0.1.9 image stays on disk, so running sessions are not cut off. exit 0 |
| current/doctor-hook-image | claude | InstalledHarness | pass | doctor hookStart.ingressImage.claude: status current, recorded image 64db9c9594b0, no advice. |
| current/doctor-hook-image | codex | InstalledHarness | pass | doctor hookStart.ingressImage.codex: status current, recorded image 64db9c9594b0, no advice. |
| current/doctor-availability | mixed | InstalledHarness | pass | doctor hookStart.availability: available |
| current/captures-accepted | claude | InstalledHarness | pass | 7 of 7 installed claude hook commands exited 0 with {} through Git Bash (bash.exe) (candidate image). |
| current/captures-accepted | codex | InstalledHarness | pass | 7 of 7 installed codex hook commands exited 0 with {} through PowerShell 7 (pwsh.exe) (candidate image). |
| current/captures-accepted | mixed | InstalledHarness | pass | Interleaved claude and codex hooks from 6 simultaneous sessions in one launch directory: through each provider's own shell 14 of 14 accepted (claude 7 of 7, codex 7 of 7); the installed tray lists 6 of 6 sessions. |
| current/native-adoption | claude | InstalledHarness | pass | Native adoption active (candidate image): claude started without Bell Tower (hooks through Git Bash (bash.exe)): attachment mode interactive-client, ownership observed, health verified-live, process identity captured. |
| current/native-adoption | codex | InstalledHarness | pass | Native adoption active (candidate image): codex --no-daemon started without Bell Tower (hooks through PowerShell 7 (pwsh.exe)): attachment mode embedded-runtime, ownership observed, health verified-live, process identity captured. |
| current/native-adoption | mixed | InstalledHarness | pass | Both plain sessions are listed together alongside the managed ones; adoption is evaluated per provider by the same shared policy. |
| current/codex-command-form | codex | InstalledHarness | pass | Repaired Codex hook command active (candidate image): the installed command is the PowerShell call form (& 'image' --provider codex ...), accepted through PowerShell 7 (pwsh.exe) above. |
| current/hook-started-tray | claude | InstalledHarness | pass | Hook-started tray active (candidate image): a claude hook with the tray stopped started the tray and recorded a start attempt (route started-via-shell). That tray ran with the real profile environment by product design (see hook-started-tray-replaced for what the harness did about it). |
| current/hook-started-tray-replaced | claude | InstalledHarness | pass | The hook-started tray (real profile environment) stopped after exit; restarted silently under the sandbox environment and the tray is running. |
| current/hook-started-tray | codex | InstalledHarness | pass | Hook-started tray active (candidate image): a codex hook with the tray stopped started the tray and recorded a start attempt (route started-via-shell). That tray ran with the real profile environment by product design (see hook-started-tray-replaced for what the harness did about it). |
| current/hook-started-tray-replaced | codex | InstalledHarness | pass | The hook-started tray (real profile environment) stopped after exit; restarted silently under the sandbox environment and the tray is running. |
| current/inactive-features | mixed | InstalledHarness | pass | After the setup rerun all three post-0.1.9 hook-side features are active for both providers. |
| current/baseline-data-preserved | claude | InstalledHarness | pass | 2 identity stores seeded by 0.1.9 (names, change, color pin, checkout pin, palette) read back unchanged through list and from disk. |
| current/baseline-data-preserved | codex | InstalledHarness | pass | 2 identity stores seeded by 0.1.9 (names, change, color pin, checkout pin, palette) read back unchanged through list and from disk. |
| current/baseline-data-preserved | mixed | InstalledHarness | pass | All 4 mixed-run identities unchanged; 4 distinct palette accents across them. |

### isolation

| Check | Provider | Evidence | Result | Detail |
| --- | --- | --- | --- | --- |
| production-unchanged | harness | InstalledHarness | pass | No guarded production file, scheduled task or process changed (87 files fingerprinted). |

## Notes

- U1, plainly: after the upgrade and before setup is rerun, Codex hooks fail outright wherever Codex runs them through PowerShell (the stale/captures-default-shell check is an expected-failure check that observed the 0.1.9 command's parser error), so a setup rerun is required for Codex to deliver hooks at all, not only for the new features. Claude Code hooks keep working. The stale-state Codex captures that the tray sees run through a labeled modeled cmd fallback, which models a Codex whose PowerShell discovery failed and does not claim Codex falls back to cmd.
- setup --host-bridge VS Code lines: VS Code focus bridge was not scheduled: use the packaged VSIX with a standard VS Code installation.. The sandbox profile has no VS Code installation (the child USERPROFILE redirect also moves the product's known-folder lookups), so the focus-bridge extension install is not exercised by this scenario; its settings are written only into the fake home. VS Code bridge behavior stays with tasks 2.6 and 4.1.
- Setup registers an on-demand tray-start task for the sandbox data directory in the user's Task Scheduler (task present after the setup rerun). It is the first of two effects outside the sandbox directories; the run deletes it at cleanup and fails the isolation check if any other Bell Tower task changed or the sandbox task remains. The second is the tray a hook starts (see the hook-started-tray checks): it runs with the real profile environment by product design, and the harness replaces it with a sandbox-environment tray right after its probe.
- Codex hook shell: discovered the way Codex 0.164.0 does (pwsh.exe on PATH, then PowerShell 7 at its fixed path, then powershell.exe on PATH, then Windows PowerShell at its fixed path, then cmd) and resolved to PowerShell 7 (pwsh.exe) for this run; Claude Code hooks run through Git Bash. Payloads are the field sets of the native captures in tests/BellTower.Tests/Fixtures/native with sandbox session, directory and transcript values.
- Commands run through the installed launcher (sandbox-relative): `install\bell-tower.exe --version --data-dir <sandbox>\data`; `install\bell-tower.exe setup --clients both --apply --home <sandbox>\home --data-dir <sandbox>\data`; `install\bell-tower.exe start --silent --data-dir <sandbox>\data`; `install\bell-tower.exe doctor --data-dir <sandbox>\data`; `install\bell-tower.exe list --data-dir <sandbox>\data`; `install\bell-tower.exe rename --session claude:claude-pinned --name claude pinned name --color #336699 --change change-claude --checkout <sandbox>\work\main --data-dir <sandbox>\data`; `install\bell-tower.exe rename --session codex:codex-pinned --name codex pinned name --color #993366 --change change-codex --checkout <sandbox>\work\main --data-dir <sandbox>\data`; `install\bell-tower.exe exit --data-dir <sandbox>\data`; `install\bell-tower.exe setup --host-bridge --apply --home <sandbox>\home --data-dir <sandbox>\data`
