# QA 003 owner-race result

- Target: `head=b74a12b baseline=b74a12b files=27 diff=5b2efc8567ad untracked=10/3e9c4782a768`.
- Fixture: new `artifacts/qa-003-owner-race`, stable payload and launcher from the prior disposable 0.1.8 fixture, dev package `bell-tower-0.1.9002-win-x64.zip`; no maintainer path used.
- Reproduction: `pwsh -NoProfile -File openspec/changes/install-dev-builds/evidence/qa-003-owner-race.ps1`.
- Preflight passed. During `switch-before-selection`, after the stable tray had been stopped and while Switch held only the update gate, the harness started the fixture's 0.1.8 tray through its launcher. Presence reported version `0.1.8`, PID `39376`. The harness released the barrier after presence appeared.
- Result: Switch exited 0, selected `0.1.9002`, reported `trayRestarted:false`, while the 0.1.8 tray stayed running. This fails the requirement to refuse an unexpected tray owner and leaves a running payload inconsistent with selection. Finding QA-150.
- Disposable final selection: `0.1.9002`, previous `0.1.8`. The test-owned tray process exited in harness cleanup (PID 39376 is absent); its remaining presence file is only in the isolated fixture data. No user tray or provider process was touched.
- Full stdout values were: `switchExitCode=0`; `switchStdout.status=running-candidate`; `switchStdout.selectedVersion=0.1.9002`; `switchStdout.trayPid=null`; `switchStdout.trayRestarted=false`; `selectedVersionAfterRelease=0.1.9002`; observed `trayVersion=0.1.8`.
