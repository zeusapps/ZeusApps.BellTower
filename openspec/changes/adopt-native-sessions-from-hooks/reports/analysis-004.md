# Analysis report: adopt-native-sessions-from-hooks / 004

## Review target

- Reviewer / role: setup_analysis / code-analyzer.
- Client / version / mode / model (resolved ID): Codex CLI 0.165.0 / delegated Default mode / gpt-6.1-sol (high).
- Date: 2026-10-29T17:08:30Z.
- Change root / assignment: `openspec/changes/adopt-native-sessions-from-hooks` / ANALYSIS-004 bounded binder architecture investigation.
- Task and requirement references: tasks 1.2-1.3; verified terminal binding; independent runtime access. User retained full terminal-support scope.
- Scope: existing capture, runtime and host implementation; public Codex release-tag source; isolated generated-schema evidence. Read [native-adoption-001](../evidence/native-adoption-001.md) and [native-adoption-002](../evidence/native-adoption-002.md) without repeating their method inventory.
- Baseline: `a69392e9ae6734b7f229d9266ebe4a3144ef49a4`.
- Prior reports / rechecked findings: not a recheck; no implementation finding IDs assigned for correction.
- Before identity:

```text
HEAD: a69392e9ae6734b7f229d9266ebe4a3144ef49a4
Baseline: a69392e9ae6734b7f229d9266ebe4a3144ef49a4
Paths: openspec/changes/adopt-native-sessions-from-hooks/evidence/native-adoption-001.md src/BellTower/Hosts src/BellTower/IO/CodexRuntimeStore.cs src/BellTower/Providers src/BellTower/Sessions/CodexRuntimeHost.cs vscode
Manifest (A added, M modified, D deleted, ? untracked):
  ? openspec/changes/adopt-native-sessions-from-hooks/evidence/native-adoption-001.md
Tracked diff SHA-256: e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855
Untracked file SHA-256:
  9ab6d3dbaed1fcee5799928ad9bed449fa17421ef670b5c6d0b2b099207cabcb  openspec/changes/adopt-native-sessions-from-hooks/evidence/native-adoption-001.md
Summary: head=a69392e baseline=a69392e files=1 diff=e3b0c44298fc untracked=1/ab6df6247f26
```

- The identical command with these exact paths produced matching full output afterward. Concurrent setup/native-probe work was outside this target.
- Public source was fetched from the official `rust-v0.165.0` tag. GitHub's tree API resolved it to tree `360319d8dee62d87e2d59fc0c21805d79519cfed`. This is version-tagged primary-source evidence, not a native execution proof or a byte-for-byte verification of the installed binary.
- Public downloads are ignored research artifacts under `artifacts/binder-analysis-004`; no provider database or production endpoint was opened.

## Analysis performed

| Area | Method / evidence | Outcome / limitation |
| --- | --- | --- |
| Documentation workflow | Context7 library resolution then one docs request for daemon handshake/subscription behavior | Passed retrieval. Its main-branch snippets were orientation only; conclusions below use version-tagged source. |
| Daemon control and handshake | Read tagged daemon client, control-socket acceptor, transport and thread-state manager | Internal connection/subscription maps exist, but the inspected control handshake does not expose their terminal ownership to an observer. Source inspection only. |
| TUI-local diagnostic metadata | Read tagged SQLite log schema, writer and startup wiring | New concrete probe avenue: separate thread metadata and a process UUID that embeds the emitter PID. Default availability and usable event semantics still need native proof. |
| Native hook identity | Read tagged Session identity accessors and SessionStart request construction | Hook session identity is the root/descendant grouping identity, so a concrete thread-ID equality shortcut is unsafe. |
| Other TUI/host channels | Read tagged IDE-context IPC, terminal status, recording logger, native notifications and existing Bell Tower bridge | None of the inspected channels independently supplies the full verified join; details below. This is not an impossibility claim. |
| Process/host lifecycle | Inspected existing creation-time checks and host routing boundaries | Any candidate must join to a fresh TUI process generation and then its verified shell/host; backend ownership remains separate. |
| Native and database trials | No provider launched; no runtime, user home, memory or provider database read | Not performed. Separate native-probe developer owns the private dynamic trial. |
| Target integrity | Review-identity before/after | Passed, exact output match. |
| Performance | No measurement performed | No performance benefit or latency claim made. |

## Concrete next avenue: metadata-only local log correlation

The tagged [log model](https://github.com/openai/codex/blob/rust-v0.165.0/codex-rs/state/src/model/log.rs) has separate thread and process identity columns. The tagged [log writer](https://github.com/openai/codex/blob/rust-v0.165.0/codex-rs/state/src/log_db.rs#L334) associates emitted entries with structured thread metadata and the writer's process UUID. Its [UUID generator](https://github.com/openai/codex/blob/rust-v0.165.0/codex-rs/state/src/log_db.rs#L494) includes the operating-system PID and a random process token. Its queue can delay durable visibility by roughly the configured ten-second flush interval. This is a plausible thread-to-TUI-process evidence channel, not a proved binder.

The [TUI startup](https://github.com/openai/codex/blob/rust-v0.165.0/codex-rs/tui/src/startup_orchestration.rs#L783) obtains a state handle and later [installs its own log layer](https://github.com/openai/codex/blob/rust-v0.165.0/codex-rs/tui/src/startup_orchestration.rs#L913). For a local shared daemon, [state-handle selection](https://github.com/openai/codex/blob/rust-v0.165.0/codex-rs/tui/src/lib.rs#L408) uses the optional existing-state reader. That [reader](https://github.com/openai/codex/blob/rust-v0.165.0/codex-rs/rollout/src/state_db.rs#L208) can return no handle when local state is unavailable or incomplete. Therefore an absent immediate row does not establish a provider restriction.

The tagged [SQLite configuration](https://github.com/openai/codex/blob/rust-v0.165.0/codex-rs/state/src/sqlite.rs#L43) names the database `logs_2.sqlite`. Its resolved location is `logs_home()/logs_2.sqlite`, where the configured local logs directory overrides the SQLite home. Do not hardcode the default user home or assume every version uses one location. The exact effective configuration/default path for an ordinary installed Windows launch was not fully resolved in this bounded source pass; the private probe can locate it only within its known isolated configuration/artifacts.

A bounded private projection can read:

```sql
SELECT id, ts, ts_nanos, target, thread_id, process_uuid
FROM logs
WHERE thread_id IS NOT NULL
ORDER BY id
LIMIT 128;
```

This query deliberately omits log bodies and feedback contents. It is a proposed projection, not a query executed in this review. Open an existing private database read-only, with no create/migrate/recovery action.

An important remaining gap is event semantics: the inspected healthy startup path does not guarantee a TUI-originated event with structured thread metadata. Several explicit thread-scoped TUI events found in [session lifecycle](https://github.com/openai/codex/blob/rust-v0.165.0/codex-rs/tui/src/app/session_lifecycle.rs#L404) are error/fallback paths. Their target is the TUI lifecycle module, but failure-only or background-thread events cannot prove the currently displayed conversation. The private trial must demonstrate a usable ordinary event and its semantic scope. Backend-originated rows with the shared daemon's PID do not solve the join.

## Hook session identity must be mapped explicitly

The tagged [Session accessors](https://github.com/openai/codex/blob/rust-v0.165.0/codex-rs/core/src/session/session.rs#L695) distinguish the concrete thread from the identity shared by its root and descendants. The tagged [SessionStart construction](https://github.com/openai/codex/blob/rust-v0.165.0/codex-rs/core/src/hook_runtime.rs#L156) uses the latter. A private probe must compare actual hook session identity, protocol session-tree identity and concrete log thread identity. It must cover a root conversation plus fork/subagent cases instead of generalizing equality observed for one root.

## Other inspected channels

- [Thread state manager](https://github.com/openai/codex/blob/rust-v0.165.0/codex-rs/app-server/src/thread_state.rs#L345): tracks subscriptions internally. [Connection IDs](https://github.com/openai/codex/blob/rust-v0.165.0/codex-rs/app-server-transport/src/transport/mod.rs#L208) are server counters, not OS process identities. Exposing or guessing them from an observer is not an implemented binder.
- [Daemon client handshake](https://github.com/openai/codex/blob/rust-v0.165.0/codex-rs/app-server-daemon/src/client.rs#L45): performs initialization/version probing. [Control socket acceptance](https://github.com/openai/codex/blob/rust-v0.165.0/codex-rs/app-server-transport/src/transport/unix_socket.rs#L165) negotiates transport and a distinct shutdown route; this is not a client/thread enumeration service. No lifecycle operation was invoked.
- [IDE-context IPC](https://github.com/openai/codex/blob/rust-v0.165.0/codex-rs/tui/src/ide_context/ipc.rs#L680): uses a static TUI source label and a workspace parameter. That request does not carry the conversation ID, so identical-directory clients remain ambiguous even if a pipe peer PID can be established.
- [Terminal program status](https://github.com/openai/codex/blob/rust-v0.165.0/codex-rs/tui/src/terminal_program_status.rs#L237): emits lifecycle/blocking status, without a conversation identifier in the inspected protocol payload. It can inform a host surface but cannot independently join a tracked conversation.
- [TUI session recording](https://github.com/openai/codex/blob/rust-v0.165.0/codex-rs/tui/src/session_log.rs#L84): explicitly opt-in and can record rich events. Enabling recording to recover terminal identity would add a privacy/setup requirement, so this report does not recommend it as the native default binder.
- Existing `vscode/extension.js:71` and `vscode/service.js:202` supply terminal shell identity and fresh window registration. They can complete the final host join only after a concrete TUI/process relationship has been proved; directory/title matching remains inadmissible.

## Strongest next bounded probe

1. Use two private native TUI clients sharing one private backend, in the same directory with identical display names. Record each created process's executable, PID and creation time independently.
2. Accept only the disposable hooks through the native trust UI. Read fresh hook identity and exact runtime metadata without resuming a conversation from the observer or taking approval ownership.
3. After the native logging flush budget, project only the six metadata columns above from the known private database. Require TUI-originated rows whose process tokens/PIDs match the independently observed clients. Record no log bodies.
4. Determine whether those rows identify the displayed/root conversation or merely background subscriptions/error paths. Test conversation switching, one TUI displaying multiple threads, and two TUIs displaying the same conversation. Ambiguous candidates must produce no terminal action.
5. Verify reconnect, detach, late rows and PID reuse. Require log times consistent with the current process generation, retain the random process token, and recheck creation-time identity plus shell/host registration before every action. A stale database row alone cannot establish current ownership.
6. If ordinary shared TUI activity emits no suitable event, record that precise failed avenue and preserve the full requirement. Neither absent rows nor internal inaccessible maps prove all host-assisted binders impossible. Do not replace the requirement with wrappers or weaker matching.

## Findings

**No implementation findings allocated.** This assignment produced a conditional source-supported probe avenue, with explicit unsatisfied proof conditions. ANALYSIS-300 through ANALYSIS-399 remain unused.

## Re-verification

Not applicable: separate architecture investigation, not a correction recheck.

## Assessment and limitations

A metadata-only log projection is the strongest new avenue found in the exact release-tag source. It still requires ordinary native emission, exact conversation semantics, freshness and process-generation proof before implementation can rely on it. No terminal binding has been accepted, no provider restriction declared, and no required terminal surface deferred.

The analyst wrote only this reserved report and ignored public-source research files. No provider database, production session, native user title, terminal input, credential or process memory was accessed or modified. The full assigned target identity matched before and after investigation.

