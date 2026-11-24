using BellTower.IO;
using BellTower.Tray;

namespace BellTower.Diagnostics;

public sealed class DiagnosticsReporter(DataPaths paths, IMicrophoneActivitySource? microphoneSource = null)
{
    private readonly IMicrophoneActivitySource microphone = microphoneSource ?? new WindowsMicrophoneActivitySource();
    private readonly IUserIdleSource userIdle = new WindowsUserIdleSource();
    public object Report(string? transcript = null, string transcriptProvider = "claude")
    {
        var microphoneSnapshot = microphone.GetSnapshot();
        var trayRunning = new TrayController(paths).IsRunning();
        return new
        {
            Version = BellTower.Cli.ApplicationInfo.Display,
            DataDirectory = paths.Root,
            TrayRunning = trayRunning,
            State = new StateReporter(paths).Report(trayRunning),
            Capture = new CaptureHealthReporter(paths).Report(),
            HookStart = new HookStartReporter(paths).Report(),
            HookDelivery = new HookDeliveryReporter(paths).Report(),
            TranscriptTimeouts = transcript is null ? null : new TranscriptTimeoutReader().Read(transcriptProvider, transcript),
            Claude = new
            {
                HookStatus = Observed("claude") ? "observed; see timestamp" : "not observed; check setup/trust",
                Footer = "bundled ANSI status line; without a fresh tray snapshot it reads this conversation's checkout from Git metadata files without starting a process",
                NameSynchronization = BellTower.Core.NativeNamePolicy.ClaudeWriteStatus + "; native titles are only read from custom-title transcript records (generated, Bell Tower and SDK fork titles never count as user titles)",
                Background = "managed desktop sessions apply the stored dark palette through scoped OSC colors; -NoBackground disables it",
                TrustStatus = "not independently verified; review /hooks"
            },
            Codex = new
            {
                HookStatus = Observed("codex") ? "observed; see timestamp" : "not observed; review /hooks",
                Footer = "native thread-name/git-branch fields; setup --status-line or --desktop configures them reversibly; native styling",
                NameSynchronization = CapabilityMatrix.CodexNameModes,
                ApprovalResolution = "PermissionRequest is provisional; verified owning-runtime or shared-backend waitingOnApproval status (the shared backend is a human-wait confirmer alongside the owning runtime, read on the observer path) confirms a human wait (5-second grace from the first report) and a fresh negative status suppresses a stale request; without a verified runtime (or when the status read fails) the request is never confirmed: after 15 seconds it shows a visibly unverified \"possibly waiting for approval\" chip with no sound, reminder or escalation (no timing-based confirmation); denial-only signal unverified",
                Questions = "request_user_input question waits from PreToolUse (no approval hook); owning-runtime waitingOnUserInput status read as confirmation evidence only; Default-mode rejections clear at Stop; request_user_input_async not offered at 0.164.0 (unverified)",
                TrustStatus = "not independently verified; review /hooks",
                Attachment = "managed local sessions own one dedicated runtime; fallback --no-daemon retains local attachment; a shared backend is observed without ownership and its conversations carry no terminal binding: navigation, title and color are unfinished by user scope decision 2026-11-13, not a provider restriction (supported routes: the Codex profile and codex --no-daemon); remote origin remains unverified"
            },
            WindowsTerminal = new
            {
                Navigation = "live marked-console tab selection through UI Automation, with verified-window fallback; exact split-pane focus unavailable",
                Note = "no saved tab index, display-name matching, keyboard input, or terminal creation"
            },
            VsCode = new VsCodeBridgeReporter(paths).Report(),
            Desktop = new
            {
                ShellModule = File.Exists(Path.Combine(paths.Root, "shell", "BellTower.psm1")) ? "installed; open a new terminal tab" : "not installed; use setup --desktop",
                Titles = "shared change-driven isolated title writes; tray-owned five-second checkout cache; bounded failure backoff and owned-writer cleanup; live host acceptance pending",
                AttachmentHealth = "both providers: verified-live, confirmed-ended, unverified or unavailable; shell liveness alone does not verify an agent; shared-runtime mode remains explicitly classified",
                Background = "new local managed launches apply colors before agent startup; resets configured colors on exit; -NoBackground opts out",
                Naming = "shared: Bell Tower title > verified native user title > pinned change / active work / unique branch change / ticket / branch / directory",
                SelectionCoverage = "both providers: successful main-session change edits, patches, supported workflow skills and literal new-change commands; inspection never selects work; bounded history recovery and separate working-checkout controls",
                Checkout = "user pin > provider hook cwd verified against the launch repository's Git worktree list > unique work mapping (ticket-named worktree first) > launch directory; ambiguity stays explicit; a cwd in an unrelated repository is not used (unverified policy)",
                IdentityControls = "rename --name/--change/--checkout/--color/--reset; Sessions offers equivalent controls for both providers",
                History = "bounded read-only history with separate backward and tail cursors; derived-only receipts; " + BellTower.Sessions.IdentityHistoryReader.Capabilities,
                Tone = new ConfigStore(paths).Read().ToneName ?? "bundled event tones"
            },
            MicrophoneAwareVoice = new
            {
                Enabled = new ConfigStore(paths).Read().MicrophoneAwareVoice,
                State = microphoneSnapshot.State.ToString().ToLowerInvariant(),
                ObservedAt = microphoneSnapshot.ObservedAt,
                Reason = microphoneSnapshot.Reason,
                Scope = "Current interactive Windows user's active capture endpoints, shared-mode audio sessions only. Exclusive-mode capture may be missed; RDP and query failures report unknown."
            },
            AttentionReminders = new
            {
                Settings = new ConfigStore(paths).Read().Reminders,
                Idle = userIdle.GetSnapshot(),
                EscalationCandidates = (int?)null,
                EscalationCandidateStatus = "Not evaluated: doctor has no current provider projection; only a fresh tray-owned eligibility check can expose candidates.",
                Scope = "Current connected Windows interactive session; unavailable or disconnected state disables reminders. GetLastInputInfo reports a 32-bit millisecond uptime tick; elapsed values above half the uint32 range (about 24.8 days) are treated as ambiguous, and future, stale or out-of-range observations are unknown."
            },
            ConfirmationCoverage = new ConfirmationCoverageReporter(paths).Report(),
            NativeSessions = NativeSessionsReport(),
            CapabilityMatrix = new { Claude = CapabilityMatrix.ClaudeVersion, Codex = CapabilityMatrix.CodexVersion, Rows = CapabilityMatrix.Rows },
            IdentityDocumentation = "docs/session-identity.md",
            CapabilitiesFile = "docs/compatibility.md"
        };
    }
    private bool Observed(string provider) => File.Exists(Path.Combine(paths.Root, provider + "-observed.json"));

    // Per provider state of the adopted direct sessions' navigation, title and color, and of plain default Codex. The setting is read
    // from the selected data directory's configuration; the texts are the matrix rows' account in short form.
    private object NativeSessionsReport()
    {
        var enabled = new ConfigStore(paths).Read().NativePresentation;
        var titles = new ProviderTitleState(paths);
        var claudeTitle = titles.Report("claude");
        var codexTitle = titles.Report("codex");
        // Per session: the provider's disabling setting is in its actual settings file (set by the step or by the user) and the session
        // started after it took effect. Doctor states the setting and the time; the session test is the same call the writer makes.
        string Title((bool Present, DateTimeOffset? Since, string File) state) => state.Present
            ? "Bell Tower title for sessions started after " + state.Since!.Value.ToString("O") + " (the provider's own title is disabled in its settings); a session started earlier keeps the provider's title"
            : "provider-owned (Bell Tower writes color only; setup --provider-titles, or the provider's own setting, lets it write a title in new sessions)";
        string Now((bool Present, DateTimeOffset? Since, string File) state) => !enabled ? "window only (Windows Terminal)" : state.Present ? "tab (Windows Terminal) for sessions started after the setting took effect" : "window only (Windows Terminal), the provider owns the tab title";
        const string navigation = "Windows Terminal: window only with the setting off or while the provider owns the tab title, the session's tab with the setting on and the provider's own title in effect disabled for that session; VS Code: the registered terminal through the bridge";
        const string presentation = "supported behind the setting (on by default, switch it off in the tray): identity palette background at the first accepted hook (Claude Code sends it at launch; codex --no-daemon sends none at launch, so its color appears a few seconds after the first prompt is submitted, during the first turn; about 4 s measured for both providers), not before startup, and the session title only while the provider's own title is in effect disabled for that session (set by the opt-in step or by the user, and the session started after it took effect; the provider overwrites a title otherwise); exit, switch-off or loss of the verified agent or shell resets the colors with OSC 110/111 and gives back the prior title on the recorded shell only; " + CapabilityMatrix.ExitResetEvidence;
        return new
        {
            Setting = enabled ? "on" : "off",
            SettingName = "Title and color native sessions (tray menu)",
            ClaudeDirect = new { Navigation = navigation, NavigationNow = Now(claudeTitle), Title = Title(claudeTitle), TitleAndColor = presentation },
            CodexNoDaemon = new { Navigation = navigation, NavigationNow = Now(codexTitle), Title = Title(codexTitle), TitleAndColor = presentation },
            CodexPlainDefault = new
            {
                Supported = "tracking, alerts, name sync and permission status (the shared backend confirms a human wait alongside the owning runtime)",
                Unfinished = "navigation, title and color: unfinished by user scope decision 2026-11-13, not a provider restriction; the setting does not apply",
                Routes = "the Codex profile (Start-Codex) or codex --no-daemon"
            },
            ClaudeModeCrossCheck = "not applicable: Claude Code has no shared backend, so its mode keeps the command-line classification"
        };
    }
}
