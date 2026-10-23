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
            Footer = "bundled ANSI status line",
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
            Titles = "opt-in role-function console title synchronization; verified process creation times; live host acceptance pending",
            Background = "new local managed launches apply colors before agent startup; resets configured colors on exit; -NoBackground opts out",
            Naming = "manual Name > session-selected OpenSpec change > directory; successful literal OpenSpec CLI operations observed through main-session Bash hooks",
            SelectionCoverage = "new change, status --change and instructions --change; bounded causal receipts; sufficient successful post-only evidence is accepted; JSON success or verified local scaffold; shell programs and hidden nested orchestration are ignored",
            Tone = new ConfigStore(paths).Read().ToneName ?? "bundled event tones"
        },
        CapabilitiesFile = "docs/compatibility.md"
    };
    private bool Observed(string provider) => File.Exists(Path.Combine(paths.Root, provider + "-observed.json"));
}
