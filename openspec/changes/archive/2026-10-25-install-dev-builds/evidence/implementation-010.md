# Implementation 010 (QA-450 line endings)

Changed: tests/BellTower.Tests/Scripts/InstallDevScriptTests.Recovery.cs only, LF -> CRLF (no BOM, content untouched).

Whitespace-only proof: sha256 of the file after stripping CR equals the pre-change copy (77dca3b5c5f30d6e626e98ee586984b410d4377d366a90c34693d0b5ccced202); `git diff --no-index --ignore-cr-at-eol` reports no differences.

Other scoped files audited with `git ls-files --eol`: all .cs files and tracked-modified files are CRLF in the working tree. Still LF: scripts/install-dev.ps1, tests/BellTower.Tests/Scripts/InstallDevFunctionHarness.ps1 (new), docs/releases.md and src/BellTower/BellTower.csproj (tracked, LF already at baseline). Left unchanged: format.ps1 -Verify does not flag them and the repo mixes LF and CRLF for ps1/md/csproj (6 LF vs 4 CRLF ps1, 7 LF csproj), so no convention requires a change.

Verification: `./scripts/format.ps1 -Verify` exit 0; Release build 0 warnings 0 errors; focused tests (InstallDevScriptTests|HookMaintenanceTests|DataLockTests) 70 passed, 0 failed.

Identity: head=b74a12b baseline=b74a12b files=34 diff=1eaa17c65cb6 untracked=16/0c724f50f6d8
