using BellTower.Core;

namespace BellTower.Tray;

public sealed record ChipView(string Key, string Episode, string Name, Palette Palette, string Reason, string Elapsed, string Details,
    string Navigation = "unavailable", bool Unverified = false)
{
    public string ToolTip => Name + "\n" + Details + "\n" + (Unverified
        ? "Unverified: no supported signal confirms that approval is needed from you, so this chip is silent and plays no sound or reminder.\n" : "") + (Navigation switch
        {
            "window only" => "Left-click to focus the window only; select the session's tab or pane.",
            "exact terminal" => "Left-click to focus this terminal.",
            "terminal only" => "Left-click to select this terminal. Its VS Code window is not yet verified, so it cannot be brought forward. Focus the window once; if it stays unverified (for example after a floating editor window was used) or the extension was just updated, run Developer: Reload Window.",
            "tab or window" => "Left-click to locate the session's tab; falls back to the window if it cannot be identified.",
            "exact tab" => "Left-click to select this tab. Split-pane focus is unchanged.",
            _ => "Terminal navigation is unavailable."
        }) + "\nTime: elapsed waiting for the displayed reason, including the 20-second completion grace."
        + "\nSuccessful chip navigation hides the timer for this wait; the chip stays visible."
        + "\nRight-click to dismiss this wait.";
}
