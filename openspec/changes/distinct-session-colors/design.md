# Design

## Context

See proposal.md (Why). Today:

- `Palette.Create` draws a random hue and builds a dark background (HSL lightness 0.105), an accent (lightness 0.42) and a black or white foreground by contrast.
- `Palette.Allocate` draws 32 candidates and keeps the one with the largest RGB distance to the nearest "taken" accent.
- `IdentityStore.AllocatePalette` builds "taken" from the 64 most recently written identity files (any state, ended included) and launch palettes from the last 24 hours, consumed or not. It runs under the shared "identity" lock so concurrent creations see each other.
- Named colors (`Palette.FromColor`), random color and reset go through `IdentityControls` with `AllocatePalette` as the generator.
- Managed launches allocate through `LaunchPresentationStore`, and native adoption uses the identity path. Both providers share this code.

## Goals / Non-Goals

**Goals:** clearly distinct colors for the sessions open at the same time; deterministic and testable allocation; no change to colors users already have.

**Non-Goals:** recoloring existing sessions automatically; user-defined palettes or themes; changing the badge or title layout; light-theme terminal support beyond today's readable foreground rule.

## Decisions

1. **A fixed, ordered series instead of random hues.**
   - Twelve hues spaced 30° apart in OKLCH, with one fixed lightness and chroma for the accent and a very dark background of the same hue.
   - The order spreads neighbours far apart: indices 0, 6, 3, 9, 1, 7, 4, 10, 2, 8, 5, 11 of the 30° steps. The first few sessions are therefore maximally different, and each later one still lands between existing colors.
   - The series is a static, generated-once table in `Palette`, with each entry's contrast verified by a test: accent against foreground at least 4.5:1, and the background against the light terminal text Bell Tower assumes at least 7:1.
   - Alternative rejected: keep random draws with a better distance metric. It stays non-deterministic, is hard to test, and can still cluster.

2. **Perceptual distance.** Accents are compared in OKLab (Euclidean ΔE) instead of RGB. It is simple, needs no dependency, and matches perceived difference far better than RGB for these saturated mid-tones. It is used only when all series colors are taken.

3. **Allocate against live sessions only.**
   - "In use" is the accents of sessions not ended in the current monitoring state, plus launch palettes prepared but not yet consumed by a registered session within the last hour.
   - Ended sessions and identity files no longer count.
   - The allocation reads the state snapshot read-only under the existing identity lock, so the concurrency guarantee is unchanged.
   - Rule: the first series entry, in series order, whose accent no live or pending session uses. Otherwise the entry with the largest minimum OKLab distance to the in-use accents, ties broken by series order.
   - An explicit custom (non-series) accent of a live session counts as in use through its distance.
   - Alternative rejected: count the most recent identities, as today. That is the cause of T-11.

4. **Stability and controls.**
   - A saved palette is never reallocated by refreshes, restarts or resume.
   - Reset and "random color" use the same allocation and exclude the session's own current color, so the user visibly gets a different one.
   - Named and hex colors (`FromColor`) are applied exactly, as today.
   - Existing identity files keep their palettes, so no data migration is needed and old builds can still read new palettes, which keep the same three-hex-field shape.

5. **Parity.** One allocation path for Claude Code and Codex, managed and native, Windows Terminal and VS Code. Provider adapters are untouched.

## Risks / Trade-offs

- [More than twelve live sessions] → Colors repeat, but the farthest-distance rule keeps them as different as the series allows. This is documented.
- [A series color looks too similar on a particular terminal theme] → The contrast tests guard readability, and users can pin a named or hex color.
- [Reading the monitoring state inside the identity lock adds latency] → It is a bounded, read-only snapshot read, and the existing 16 MiB budget and bounded-state work keep it small. Measure it in the tests.

## Migration Plan

No migration. New and reset palettes use the series, and existing ones stay. Rolling back to an older build keeps every saved palette readable.
