# Live acceptance from this conversation

Recorded on 2026-10-21. The user's hands-on testing is live acceptance evidence. Automated probes supplement it; they do not invalidate it or require the user to repeat confirmed checks.

| Area | Evidence already supplied | Remaining specific uncertainty |
| --- | --- | --- |
| Session startup and hooks | Codex PM profile launched, normal hook review appeared, and completion triggered Bell Tower | Full sanitized hook recordings were not retained; rare denial, interruption and background combinations are not all evidenced |
| Strip refresh | User explicitly confirmed flicker was gone after applying the update | Display/RDP resizing is untested and explicitly deferred as nonblocking |
| Viewed timer | User confirmed clicking a chip hides the timer while keeping the name and reason visible | A new wait's timer and restart persistence are covered by automated tests; no contrary live report |
| Drag handle | User confirmed the strip can be moved using the three-dot handle | Saved-position restoration is not separately reported; display/RDP resizing is explicitly deferred as nonblocking |
| Tray lifetime | User confirmed Bell Tower continues running for other sessions after the starting terminal tab closes | No starting-tab lifetime retest remains |
| Right-click dismissal | User confirmed right-click hides the current chip until the session needs attention again | No dismissal retest remains |
| Parallel waits | User confirmed several waiting sessions appear as separate chips | No simultaneous-chip retest remains |
| Display/RDP resizing | User has not checked it and explicitly does not require it for now | Deferred follow-up, nonblocking for this change; not claimed as passed |
| Windows Terminal navigation | User explicitly confirmed correct tab selection, fullscreen preservation, multiple windows and reordered tabs | Hidden headers and split panes are not explicitly reported |
| Session backgrounds | User confirmed Windows Terminal color application and immediate startup color for both Claude and Codex, before Codex's first response | No startup-timing retest remains |
| Claude identity | User confirmed the name badge and branch appeared | A real linked-worktree switch is not explicitly reported |
| VS Code navigation | User explicitly confirmed that chip clicks now bring forward the correct window and select the correct terminal | Dedicated duplicate-name/multiple-window edge-case coverage is not separately specified |
| Permission notifications | User saw the first permission notification, granted approval, and reported a silent subsequent request; read-only state inspection supported the defect | The 0.1.7 fix passed regression tests but has not been live-retested. The user explicitly designated this a separate issue/follow-up that must not block this change |
| Session naming | User confirmed directory-based titles and explained that simultaneous sessions need change/worktree names | Automatic agent-selected change/worktree association is deferred; explicit naming is implemented |

This is an incremental acceptance record. The normal workflow has been exercised extensively. Outstanding task checkboxes should be interpreted against the observations above, rather than described as eleven unbuilt features or a new request to run the entire test matrix.

This round of user-reported manual checks is complete. Repeated-permission regression retesting and display/RDP resizing are explicitly nonblocking follow-ups. Automatic agent-selected change/worktree naming remains deferred scope. The remaining engineering bookkeeping is to reconcile the planning artifacts with accepted refinements and this evidence. Complete four-way hook recordings and rare host/provider cases remain explicitly unobserved rather than implicitly passed; they must not be represented as completed tests.

The checklist was reconciled during close-out using PowerShell after the patch helper failed to write OpenSpec files. It records 28/33 tasks complete; five comprehensive hook-capture/fixture-dependent tasks remain unchecked. The user explicitly requested archival with the recorded acceptance and follow-ups. Missing captures are preserved as evidence gaps rather than claimed as passed tests.
