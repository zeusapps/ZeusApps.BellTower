# Architecture

Bell Tower uses one .NET 10 Windows executable, responsibility-based folders, and a separate xUnit test project. WorkLog is an organizational reference and has no project/runtime dependency.

| Area | Responsibility |
| --- | --- |
| Core | Provider-neutral observations, session activity/attention reduction, palettes, grace and episode/audio policy |
| IO | Separate snapshot/attachment/identity/config stores, atomic local files, serialized state transactions, process identity/liveness, bounded logs, read-only Git queries |
| Providers | Sanitize Claude/Codex hook JSON; render the ANSI Claude status line |
| Sessions | Agent launching and lifecycle; session listing, naming and focus actions |
| Hosts | Capture attachments; route through ITerminalNavigator; separate VS Code request protocol and Windows Terminal activation; scoped title helper |
| Tray | Nonactivating strip, session overview, position/refitting, serialized chime/SAPI voice, silent audio sink |
| Setup | Plan/review/apply hooks, badge, PowerShell module/imports, terminal profiles and VS Code bridge; preserve unrelated settings; legacy metadata import |
| Cli | System.CommandLine command classes, typed options/validation, generated help, neutral hook errors, literal agent argument forwarding |
| Diagnostics | Report setup observations and capability limits without modifying configuration |
| vscode | Dependency-free local registration/focus bridge, separately tested with Node's built-in runner |

Hook invocations extract only needed fields and publish unique normalized records to the inbox. Tool/prompt bodies and transcript contents are not retained. Tool input is hashed in a canonical order for correlation when permission events omit tool-use IDs. Event-specific success output is an empty JSON object and does not approve, deny, or continue work.

The tray holds a per-data-directory instance mutex and reduces inbox records under a state lock. It commits an atomic snapshot before removing accepted records; replay is idempotent. Previous snapshots provide corruption recovery. An attachment's process ID is paired with process creation time. Inactive processes are never inferred dead solely from elapsed time, and no agent/terminal is terminated by Bell Tower.

Foreground activity, known background tasks, and outstanding attention are independent. Acknowledgment belongs to an episode; metadata updates never reset it. Completed operation IDs/fingerprints prevent delayed permission observations from reviving known completed calls. Provider-native turn IDs protect turn boundaries where available. Synchronous bounded hooks reduce delivery races when a provider lacks turn IDs.

Identity is stored independently under a hash of provider plus session ID. Manual name overrides explicit user-selected change, which overrides the directory default. Initial launch metadata is optional. Color is generated once and persists. Git inspection uses the actual checkout and a bounded cache.

Audio runs on one STA worker. Alerts carry their reason to the sink, which selects the bundled event tone unless a custom WAV or the classic 880 Hz, 400 ms chime is selected. PCM normalization bounds both source and converted size. Aggregated alerts use the highest-priority reason's tone. SAPI prefers an installed Zira voice and falls back to tone-only; no invariant-globalization switch is set. Tests never invoke the physical sink.

Distinct blockers at the visible attention priority renew the episode; exact duplicate waits retain it. The persisted episode start determines new-notification eligibility independently of the oldest outstanding wait. This prevents a stale unmatched wait or an earlier viewed/dismissed chip from silencing the next permission request, while preserving silent restoration and duplicate suppression. Provider command fingerprints use Bash/apply_patch command content across hook stages; other tools retain canonical complete inputs.

The VS Code bridge registers its extension-host process creation time and current terminal shell PIDs. It accepts only its window instance's unexpired requests, verifies shell creation time, and acknowledges terminal reveal. Actual cross-window foreground activation still requires live-host acceptance. Windows Terminal activation validates its window owner and restores only minimized windows. A bounded disposable helper marks the verified session console's title, selects a unique matching live UI Automation tab, and restores the prior title under the presentation lock. Ambiguity falls back to the verified window. No saved tab index or friendly name identifies a session. Split-pane focus remains separate.

Desktop ownership stores optional project directories and selected providers. Setup generates named project profiles additively and supplies explicit Windows Terminal starting directories. The PowerShell session functions accept a directory, invoke the native agent there, and restore the invoking shell's location afterward. Scoped console presentation applies the stored identity palette only when the launch opted into background ownership; the wrapper resets configured profile colors on exit.

The packaged application has no updater or external service. Setup and legacy import are explicit commands; ordinary startup does not modify agent settings.

The desktop workflow is terminal profile -> PowerShell role function -> native agent. The executable launcher is optional. The bundled shell module sets attachment metadata, resolves the native command, starts the tray separately, and restores shell environment/title on exit. Hook observations bind the provider session ID to that attachment. Local Codex role functions use its dedicated mode so the backend does not inherit another session's terminal metadata.

Title synchronization runs in a separate short-lived helper. It validates both the shell and agent creation times, checks both processes belong to the attached console, and sets only the title. It never injects text/keystrokes, changes foreground focus, or modifies global themes. Detachment and title writes share a presentation lock so the module restores the old title after pending writes finish. Plain/native sessions without managed-title metadata remain untouched.

See [development conventions](development.md) for command structure, responsibility boundaries, formatting, and checks.
