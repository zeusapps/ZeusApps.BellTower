# Tasks

## 1. Color series and distance

- [ ] 1.1 Add the ordered twelve-entry OKLCH series and the OKLab distance to `Palette` (design decisions 1 and 2). Verify with unit tests: the series order matches the design; each entry has a dark background, a related accent and a readable foreground with the contrast thresholds of decision 1; consecutive series entries are farther apart than any pair of adjacent 30° hues; and `Distance` is OKLab-based.

## 2. Allocation against live sessions

- [ ] 2.1 Change `IdentityStore.AllocatePalette` to collect accents from sessions not ended in the monitoring state plus unconsumed launch palettes younger than one hour, under the existing identity lock, and allocate by the rule of design decision 3. Verify with tests:
  - with 80 ended sessions holding series colors and two live sessions, the third live session gets the first series color neither live session uses;
  - twelve live sessions get twelve different colors;
  - a thirteenth gets the farthest color, deterministically;
  - a live session with a custom hex accent counts by distance;
  - concurrent allocations from Claude Code and Codex under the lock get different colors.
- [ ] 2.2 Route managed launch palettes (`LaunchPresentationStore`), native adoption, reset and random color through the same allocation, with reset and random excluding the session's own current color (design decision 4). Verify with matched Claude Code and Codex tests, a mixed managed and native case, and the existing named/hex, resume and restart tests: saved palettes are unchanged after refresh, restart and resume.

## 3. Documentation and parity

- [ ] 3.1 Update the identity color description in `docs/architecture.md` and the user-facing color control documentation: the series, live-session allocation, the repeat rule above twelve sessions, and that existing colors are kept. Verify the docs match the implemented thresholds and rule, and that the capability matrix and doctor wording for color controls stay correct for both providers.

## 4. Acceptance

- [ ] 4.1 Run the Release build, the full .NET suite, format verification and `openspec validate distinct-session-colors --strict`, and record the results.
- [ ] 4.2 One QA review and one code-analyzer review of the stable implementation target. Resolve blocking findings and record suggestions as deferred.
- [ ] 4.3 Installed check on a dev build, with user authorization: open at least six concurrent sessions across Claude Code and Codex, managed and native, in Windows Terminal and VS Code, and confirm with the user that each has a clearly distinct color and that existing sessions kept theirs. Record the result, or Not performed.
