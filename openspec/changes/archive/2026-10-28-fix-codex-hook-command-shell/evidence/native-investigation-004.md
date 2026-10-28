# Mixed native-session investigation 004

## Scope and candidate identity

This was a bounded attempt at the task 5.2 isolated mixed-provider probe using the final handed-off Bell Tower development candidate 0.1.9018, Claude Code 2.1.310, and Codex CLI 0.164.0. The intended probe would apply product-generated hooks to disposable provider homes, start both real clients against loopback fake model endpoints, and observe their captures through the candidate's packaged tray consumer in one temporary data directory.

The package ZIP SHA-256 was `fe5e164d189c6e423d6d79c2115a291bbd81f64c6c7cd8716cf540910f327a79`; the packaged ingress SHA-256 was `82cc115629b60f2d34ace16f91d72009e6eab1e8b28d80eb0ce796d150ad5707`. The supplied pre-run implementation identity was `head=aadbfb3 baseline=aadbfb3 files=21 diff=959d86f7d758195d9f52f47110ae24ea8d4f5a0fde610a8ee7ce89d6da550da3 untracked=3/cce5a39616ce`.

## Isolated setup failure

The harness was confined to `artifacts/codex-hook-native-mixed/`. It used a new scratch trial directory, disposable git projects, fake loopback endpoints, and the explicit temporary `--data-dir`. It requested `setup --clients both --home <scratch provider home> --apply`, without `--desktop` or `--status-line`. One recorded setup attempt exited with code `-2147450721`. The setup did not complete, so the clients and packaged consumer were not launched in that attempt. The exit code is recorded as observed; its cause was not established.

## Interrupted diagnostic and scope audit

A separate diagnostic command was intended to use a new temporary home, but assigned it to `$home`. PowerShell treats variable names case-insensitively and `$HOME` is a read-only automatic variable. The assignment failed; subsequent command construction therefore used the profile value for `--home` and set `CODEX_HOME` beneath that profile. The attempted arguments were structurally:

```text
<candidate bell-tower.exe> --data-dir <new scratch data directory> setup --clients both --home <profile path> --apply
```

There was no `--desktop` or `--status-line` flag. The command used hidden/no-window process startup and requested a 120-second wait, but was interrupted after the tool yielded at about 10 seconds. It produced no exit code, process ID, or recorded process start time. The earlier isolated harness tracked its own process IDs and start times in memory and ran its cleanup path; no mixed-provider client trial followed.

The command named the real profile as the setup home, so this report does not claim that reading real provider settings was impossible. No provider settings were intentionally inspected or restored. The orchestrator's independent audit confirmed that both real provider settings files retained their before-snapshot hashes, and that no candidate 0.1.9018 Bell Tower process with the diagnostic trial root in its command line remained. No lasting real hook changes were observed. These audit facts do not turn the interrupted diagnostic into an isolated test.

## Result and limits

- **Mixed native acceptance: not run.** No evidence was collected for concurrent Claude and Codex process lifetimes, provider captures, distinct sessions, or packaged-consumer drain.
- **Setup startup: failed/unexplained.** One isolated setup attempt returned `-2147450721`; no cause is inferred.
- **Diagnostic: interrupted with home-path collision.** Its command structure could target real provider hook settings; the follow-up audit found no lasting setting changes.
- **No hard restriction claim.** This partial harness outcome says nothing about provider parity, shell support, or whether the intended mixed native scenario can pass with a corrected isolated runner.

No mixed-provider native acceptance is claimed, and this evidence does not satisfy task 5.2.
