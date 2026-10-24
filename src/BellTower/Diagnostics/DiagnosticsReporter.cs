using BellTower.IO;
using BellTower.Tray;

namespace BellTower.Diagnostics;

public sealed class DiagnosticsReporter(DataPaths paths)
{
    public object Report(string? transcript = null, string transcriptProvider = "claude") => new
    {
        DataDirectory = paths.Root,
        TrayRunning = new TrayController(paths).IsRunning(),
        Capture = new CaptureHealthReporter(paths).Report(),
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
            NameSynchronization = "dedicated authenticated loopback runtime for new managed local sessions; explicit --no-daemon and existing/remote sessions without a verified runtime remain unavailable; native /rename pauses managed writes",
            ApprovalResolution = "verified owning-runtime waitingOnApproval status gates permission attention; 5-second confirmed-human grace; without fresh runtime verification, 15-second hook fallback until correlated resumption/result; denial-only signal unverified",
            Questions = "blocking local question-tool hooks only; hosted/async paths unverified",
            TrustStatus = "not independently verified; review /hooks",
            Attachment = "managed local sessions own one dedicated runtime; fallback --no-daemon retains local attachment; explicit shared/remote origin remains unverified"
        },
        WindowsTerminal = new
        {
            Navigation = "live marked-console tab selection through UI Automation, with verified-window fallback; exact split-pane focus unavailable",
            Note = "no saved tab index, display-name matching, keyboard input, or terminal creation"
        },
        VsCode = "requires bundled focus bridge",
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
        CapabilityMatrix = new { Claude = CapabilityMatrix.ClaudeVersion, Codex = CapabilityMatrix.CodexVersion, Rows = CapabilityMatrix.Rows },
        IdentityDocumentation = "docs/session-identity.md",
        CapabilitiesFile = "docs/compatibility.md"
    };
    private bool Observed(string provider) => File.Exists(Path.Combine(paths.Root, provider + "-observed.json"));
}
