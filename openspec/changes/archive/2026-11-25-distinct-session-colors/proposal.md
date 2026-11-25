# Proposal

## Why

In the 2026-11-22 native-session trial (finding T-11 in `adopt-native-sessions-from-hooks`), six concurrent sessions in Windows Terminal and VS Code received background colors that were hard to tell apart. Today `Palette.Allocate` draws 32 random hues and keeps the one whose accent is farthest from "taken" accents. But "taken" means the 64 most recently written identities, mostly ended sessions, plus launch palettes from the last 24 hours, not the sessions that are open now. Distance is also plain RGB distance, which does not match perceived difference. With many saved identities, two live sessions can get nearly the same shade.

## What Changes

- Replace random hue drawing with an ordered series of perceptually spaced session colors. Each entry is a dark background, a brighter related accent and a readable foreground, and all entries are checked for contrast.
- Allocate against the sessions that are live now, plus launch palettes prepared but not yet consumed within a short window, instead of recent saved identities. Prefer the first series color no live session uses. When every color is in use, pick the one whose accent is perceptually farthest from all live accents, with a deterministic tie-break.
- Measure color difference perceptually (OKLab distance) instead of in RGB.
- Keep the existing guarantees. A session keeps its color for its lifetime, across Bell Tower restarts and conversation resume. Explicit named or hex colors and user resets keep working. The "random color" control now picks a distinct series color not in use when one is available.
- Existing saved palettes are not rewritten. Only new allocations and resets use the series.
- One shared policy for Claude Code and Codex, managed and native sessions, Windows Terminal and VS Code. No provider-specific behavior.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `session-identity`: "Shared readable session palette" now requires distinct colors from an ordered series, allocated against live sessions, instead of a random dark background. "Shared provider presentation controls" defines "random color" and reset as choosing a distinct available color.

## Impact

- `src/BellTower/Core/Palette.cs`: the color series, perceptual distance and allocation.
- `src/BellTower/IO/IdentityStore.cs`: `AllocatePalette` collects live-session and pending-launch accents instead of recent identity files.
- `src/BellTower/IO/LaunchPresentationStore.cs`: managed launches use the same allocation.
- Identity control paths (tray, CLI and bridge reset and random color) through the shared `IdentityControls`.
- Tests: palette, identity, presentation and mixed launch/native tests for both providers.
- Docs: the identity and color sections of `docs/architecture.md` and the user-facing color controls.
- Release: part of the next release's identity scope in `validate-next-release-field-acceptance`.
