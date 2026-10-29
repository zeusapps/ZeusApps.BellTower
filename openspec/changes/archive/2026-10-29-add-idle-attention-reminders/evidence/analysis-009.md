# Analysis 009 evidence: archive and specification sync

Date: 2026-10-29. Light archive review only. Baseline/HEAD `36406ddd7cccc86fd8be8d9a2cfa2d9ed07c250e`; reviewed [archive target](review-target-archive-001.txt).

## Preservation and history

Compared every path/hash in ignored `artifacts/idle-reminder-archive-20261029/preimage.json` with the archived tree:

```text
Original files: 48
Missing originals: 0
Byte-identical originals: 47
Changed original: reports/findings.md
```

Compared the changed ledger with its baseline Git content. Its only differences are the two archive-review reservation rows, the user authorization/archive activity entry, and the successor-documentation link gaining one parent traversal for the new archive depth. Prior finding/report history is retained. No original report/evidence file was changed.

The metadata file remains `schema: spec-driven` / `created: 2026-10-23` and hash-matches the preimage. Tasks remain 13 checked and zero unchecked. The interrupted analysis-004 reservation and subsequent report numbering remain present; no missing historical report is fabricated.

## Main specification

Compared the archived delta with new `openspec/specs/attention-reminders/spec.md`, normalizing only CRLF/LF, the top title, and `## ADDED Requirements` to `## Requirements`:

```text
Normalized full specification equal: True
Requirements: 4
Scenarios: 10
Main spec SHA-256: 90f05998277dd7128353cfeed08bfa2b632929d122a8c9a0bcc0b396e753736c
```

Purpose, requirement bodies, order and all scenarios are retained exactly under that normalization. Both-provider and mixed-session scope is unchanged. The orchestrator reports all nine main specs passing strict validation; the analyzer did not redundantly rerun the CLI.

`git diff --name-only 36406dd -- src tests openspec/specs` returned no tracked changes. The only untracked main-spec path was the new attention-reminders capability. Thus this archive introduces no unrelated spec or product-source edit.

## Links and identity

Checked 187 local relative Markdown file targets from original archived Markdown plus the new main spec: zero missing targets. HTTP/mailto and fragment-only links were excluded; this is local file-target resolution, not remote availability or universal anchor validation. The ledger's updated successor link resolves to the existing documentation at the corrected five-parent depth.

Before and after, the complete review-identity output exactly matched the reserved archive target:

`head=36406dd baseline=36406dd files=1 diff=e3b0c44298fc untracked=1/6f1f2fbcf554`.

The target covers the new main spec and excludes concurrent reviewer/ledger writes. Archive preservation was verified separately using all original hashes and the scoped ledger comparison.

## Carry-forward and limitations

Accepted runtime evidence carries forward: QA-006's 1,089/1,089 default suite, analysis-007's 14 independent checks, and the light EOF-only reviews remain valid because no product code changed. No build, probe, broad test, formatter, documentation fetch, installation or production action was run. Own report/evidence were inspected for sensitive content; the archive's original files retain their previously reviewed bytes except the explicitly inspected orchestrator ledger changes.

Only reserved report/evidence 009 were written. No implementation, plan, task, ledger, historical report, staging or branch change was made by this reviewer.
