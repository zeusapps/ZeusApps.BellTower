# Project facts for specialists

Shared by the developer, QA, and code-analyzer roles. Role files hold only role-specific rules and link here.

## Commands

- Build: `dotnet build -c Release --no-restore`
- Test: `dotnet test -c Release --no-build --no-restore`
- VS Code extension: `node --test vscode/service.test.js vscode/focus.test.js`
- Format (developer only): on a dirty tree run `dotnet format ZeusApps.BellTower.slnx --no-restore --include <your changed files>`. Use `./scripts/format.ps1` (whole solution) only on a clean tree or when the assignment covers the whole solution.
- Format (reviewers): only verify-only commands; reviewers never run the mutating formatter.
  - Whole solution: `./scripts/format.ps1 -Verify`. Use it when the tree is clean or the assignment covers the whole solution.
  - Dirty tree: `dotnet format ZeusApps.BellTower.slnx --no-restore --verify-no-changes --include <changed files>` for the files in your review target, so unrelated pending edits do not fail your check. Report any failure outside your target as unrelated; do not fix it.
- Review identity: `./scripts/review-identity.ps1 -Baseline <commit> -Paths <scoped path> [<scoped path> ...]` (space-separated paths, relative to the current directory; run from the repository root). It only reads Git state and prints a manifest, hashes and a summary line.

## Layout

- Product code is under `src/` (`BellTower`, `BellTower.Distribution`, `BellTower.Ingress`, `BellTower.Launcher`, `BellTower.Protocol`); xUnit tests are under `tests/BellTower.Tests`.
- Layer responsibilities (`Core`, `IO`, `Providers`, `Sessions`, `Hosts`, `Tray`, `Setup`, `Integration/BellTower.psm1`, `Diagnostics`) and style rules are in [docs/development.md](../../docs/development.md). Follow and judge against it; do not restate it. When it changes, check that this file still agrees.

## Claude Code and Codex parity

Provider-specific behavior belongs in provider adapters (`Providers`, `Setup/ProviderSettingsEditor`, `Integration/BellTower.psm1`, `Sessions/*Name*`); shared policy stays in `Core`. Every product feature or fix supports both clients unless a verified hard provider restriction exists, with matched Claude and Codex scenarios and mixed simultaneous sessions. Reviewers state which client(s) each check covers and flag duplicated per-provider policy and unexplained parity gaps. The policy is in [AGENTS.md](../../AGENTS.md).

## Safety

- Never run `bell-tower setup --desktop --apply`, `exit`, `toggle-voice`, or sound tests against the user's install.
- Never use the default `%LOCALAPPDATA%\ZeusApps.BellTower` data directory; always pass `--data-dir` or set `BELLTOWER_DATA_DIR` to a disposable location.
- Never stop the tray app or the user's agent sessions.
- Never edit provider settings in the user profile (`~/.claude`, `~/.codex`) or other personal configuration.
- Never replay real approvals, play physical audio, or send production messages.
- Use isolated data and settings, fake providers, and silent audio sinks. Record a check that needs the live instance as not performed.
- Your inspection must not change or take over the main session's OpenSpec association or selection.
- Fetch current primary library or API documentation through the repository documentation workflow when you depend on it.
