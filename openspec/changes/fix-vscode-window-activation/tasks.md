# Tasks

## 1. Ownership and platform capability

- [x] 1.1 Verify the lifecycle predecessor supplies honest attachment health and process/start identities; read this design and existing bridge protocol tests before changing focus semantics.
- [x] 1.2 Prototype an instance-bound editor-window mapping in isolated two-window fixtures using current official VS Code/Windows APIs; deliver evidence that extension-host PID alone cannot pick a window and ambiguous mappings fail safely.

## 2. Activation protocol

- [x] 2.1 Extend registration/request capability negotiation with verifiable owning-window evidence and backward-compatible unavailable/partial outcomes; verify old bridge records, expired registrations and PID reuse.
- [x] 2.2 Implement bounded user-initiated foreground activation with minimized-only restore and a verified native fallback where ownership is proven; verify denied activation and preservation of visible maximized/fullscreen state.
- [x] 2.3 Reveal the exact panel/editor terminal after activation and acknowledge only confirmed foreground plus exact reveal; verify duplicate terminal names, multiple editor windows and no global terminal.focus misrouting.
- [x] 2.4 Preserve current episode/wait state and chip timer on failed or stale requests; verify late navigation receipts cannot hide a newer episode's timer.

## 3. Atomic bridge recovery

- [x] 3.1 Clean up uniquely owned temporary files on write/rename failure without deleting the previous valid destination; verify injected failure leaves zero .tmp files and a later tick recovers.
- [x] 3.2 Separate recoverable registration publication errors from verified focus processing with bounded retries/logging; verify focus cannot be indefinitely starved and expired ownership is never used.
- [x] 3.3 Package the matching VSIX and deferred activation notice through existing ownership rules; verify isolated installation preserves settings and does not force an editor/agent restart.

## 4. Acceptance

- [x] 4.1 Run Node bridge suites and native disposable-window process tests for mappings, cleanup, focus denial and show-state preservation; retain actual results with no production window interaction.
- [x] 4.2 Record targeted live Windows 11/RDP checks for an owning window behind another app and minimized, including two windows with duplicate terminal names; mark unsupported input-focus guarantees honestly.
- [x] 4.3 Update compatibility/navigation docs and verify formatting, Release build, Node and relevant host/setup tests without changing Claude or Codex provider transports.


## 5. Provider parity acceptance

- [x] 5.1 Implement and verify this feature for both Claude Code and Codex using shared policy and provider adapters; run matched provider fixtures plus a mixed-session case and retain version/mode-specific evidence.
- [x] 5.2 Audit setup, defaults, configuration, diagnostics and controls for equal feature availability; document any hard provider restriction with its proof, honest shared fallback and regression check, and treat missing implementation or unverified capability as incomplete work.
