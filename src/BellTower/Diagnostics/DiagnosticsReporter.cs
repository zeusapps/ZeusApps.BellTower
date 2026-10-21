using BellTower.IO;
using BellTower.Tray;

namespace BellTower.Diagnostics;

public sealed class DiagnosticsReporter(DataPaths paths)
{
    public object Report() => new
    {
        DataDirectory = paths.Root,
        TrayRunning = new TrayController(paths).IsRunning(),
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
            Footer = "native /statusline can show Git/directory fields; arbitrary colored chip unavailable; managed titles include Bell Tower name and Git context",
            ApprovalResolution = "correlated operation resumption, tool result or next prompt/turn; denial-only signal unverified",
            Questions = "blocking local question-tool hooks only; hosted/async paths unverified",
            TrustStatus = "not independently verified; review /hooks",
            Attachment = "shared-daemon origin unverified; desktop role functions use --no-daemon for local session attachment"
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
            Naming = "explicit Name/Change or directory default; native Codex /rename is independent",
            Tone = new ConfigStore(paths).Read().ToneName ?? "bundled event tones"
        },
        CapabilitiesFile = "docs/compatibility.md"
    };
    private bool Observed(string provider) => File.Exists(Path.Combine(paths.Root, provider + "-observed.json"));
}
