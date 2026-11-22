# Implementation 007 (DEV-007, fix cycle 6): QA-500 and analysis-006 residual 1

## Changes

- `src/BellTower/IO/RefreshHealth.cs` (CRLF kept): the `Quoted` closing quote is now an apostrophe not followed by a letter or digit, `'(?![\p{L}\p{N}])`, instead of one followed by whitespace, `.`, `,`, `;`, `:`, `)`, `!` or the end. Greedy-to-last-close, the root markers, the 20 ms timeouts and the type-only fallback are unchanged. The comment above the rule is updated.
- `tests/BellTower.Tests/IO/RefreshHealthTests.cs` (CRLF kept): new theory `AClosingQuoteBeforeAnyPunctuationLeavesNoNameFragmentAndKeepsTheTrailingText` (9 inputs): closes before `]`, `['...']`, `?` with a name after an inner quote, `/z q`, `}`, `"`, `>`, `-` and the ellipsis. Each asserts the exact output (trailing text kept), no separator and no name fragment.

## Expectation changed

- `ACraftedMessageCompletesQuicklyAndLeaksNoPath`, unit `'\\ `: the 2,000-character message is cut right after a close, so the kept trailing text is `\\`. The separator assertion now ignores trailing backslashes (`text.TrimEnd('\\')`); `Secret` is still asserted absent. No other existing expectation changed.

## Timings (2,000-character crafted message after `C:\Secret\`, worst of 5 runs, scratch test since deleted)

`\'a` 7 ms (JIT), `'a/` 0, `/'a\ ` 0, `'x\ ` 0, `'/a ` 0, `'C:\a ` 1, `'\\ ` 0, `'C:\a' ` 0, `'/' ` 0, `'/a' ` 0, plus new `'C:\a'] ` 0 and `'/a'? ` 0 ms. All far inside the 20 ms rule budget.

## Results

- Release build: 0 warnings, 0 errors.
- RefreshHealth, StateReporter, BoundedState tests: 131 passed.
- Full suite: 1787 passed, 13 failed: the 11 known HookTrayStartTests, ProcessStopAuthorityTests, and the load-only HookMaintenanceTests (passed in isolation).
- `dotnet format --verify-no-changes --include` on both files: passed.

## Notes

- Residual by design: text after the last close is kept as is, so a path-like tail after a close (for example `'C:\a\x'\Sub\Name`) is not masked unless it starts at a root.
- Awaiting independent verification of QA-500; not marked closed. Not run: nothing against a real data directory, installation or live tray.

## Review identity

`head=5e95554 baseline=5e95554 files=22 diff=9f3ab1aba1d2 untracked=7/06c081ffc9e0`
