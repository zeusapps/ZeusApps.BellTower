# Native provider hook evidence 001

## Scope and candidate

This investigation covers OpenSpec tasks 1.1 and 1.3 only. It used the installed Codex CLI 0.164.0, the pinned Claude Code 2.1.310 executable retained in the existing ignored harness, and a copied ingress helper from candidate package 0.1.9014 (ZIP SHA-256 `8B78EB348A651C1F39FC71BBCE8BE0F63CBECAE20875136AA810133135312EB2`). Provider configs, project state, fake endpoints and data were isolated under ignored `artifacts/codex-hook-native-evidence/`. No provider settings or production data were read or changed.

The candidate source identity before and after the probes was unchanged: `head=aadbfb3 baseline=aadbfb3 files=16 diff=697116446e1c untracked=3/1b89a893996b`. The generated native probe scripts and raw probe artifacts are ignored under `artifacts/codex-hook-native-evidence/`; they are not source changes.

## Task 1.1 — Codex Windows hook runner and argv form

Prior native evidence in `implementation-001.md` remains: Codex 0.164.0 invoked hook strings through PowerShell 7 in two isolated shell-environment variants, and the PowerShell call-form hook delivered a SessionStart capture. The exact resolver rule outside those variants remains unknown.

An additional isolated native attempt configured `hooks.SessionStart[].hooks[].command` as an argv array of five elements (helper path plus provider/data arguments), with isolated `CODEX_HOME`, a disposable project and a loopback fake Responses endpoint. Codex 0.164.0 emitted the native config parser error `invalid type: sequence, expected a string` at the `command` value. The invocation exited with code 2 before any fake-endpoint request or capture. Thus this `command` array form is rejected; the evidence does not establish support for a different `exec` handler shape or a shell-selection rule beyond the two previously tested environments.

Result: **partial**. The tested argv-array form is proven unsupported by the installed parser. Shell behavior has been observed in two environments but its general selection rule remains unverified.

## Task 1.3 — Claude Code 2.1.310 native CLI runner

The actual pinned Claude Code 2.1.310 CLI was started with `-p` in a disposable Git project, an isolated `CLAUDE_CONFIG_DIR`, nonessential traffic and telemetry disabled, and an Anthropic base URL pointed at a loopback fake endpoint. SessionStart settings used the current product command form: the copied 0.1.9014 ingress helper, Bash quoting for a path containing spaces, an apostrophe and Unicode, `shell: bash`, and the current Git Bash path. A second record-only Bash hook captured the synthetic event metadata.

Two sandboxed attempts reached the native CLI but Git Bash failed to create an MSYS named object (`NtCreateDirectoryObject ... 0xC0000022`), so neither produced a capture. The same isolated probe then passed outside the sandbox: the record-only hook logged `SessionStart` with source `startup`, and one durable capture file was written under the disposable `data/captures-v1/` directory. The temporary CLI and fake endpoint processes created by the probe were stopped; a follow-up process check found none of their recorded PIDs still running.

Result: **passed for Claude Code 2.1.310 using the configured Git Bash shell**. The sandboxed failures are environment restrictions, not evidence of a Claude hook-command defect. This result does not cover other Claude versions or alternate Git Bash overrides.

## Commands and retained evidence

- `artifacts/codex-hook-native-evidence/run-codex-argv.ps1` — isolated Codex parser/dispatch probe; the final run is in `codex-0.164.0-argv-array-02/`.
- `artifacts/codex-hook-native-evidence/run-claude-310.ps1` — isolated Claude CLI probe; the successful run is in `claude-2.1.310-03/`. Earlier sandbox attempts are retained in `claude-2.1.310/` and `claude-2.1.310-02/`.
- `scripts/review-identity.ps1 -Baseline aadbfb366bd24906a8e477d09957d08c5ac6bf3d -Paths src tests scripts tools/BellTower.Ingress.Bench docs/hook-ingress.md docs/installation.md docs/compatibility.md docs/follow-ups.md` -> unchanged source identity summary recorded above.
- No test suite or benchmark was run; neither was required for this evidence-only task.

No conclusion is made here about the Codex latency gate, production settings, or user-operated mixed-session acceptance.
