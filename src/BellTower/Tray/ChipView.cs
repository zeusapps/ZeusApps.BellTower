using BellTower.Core;

namespace BellTower.Tray;

public sealed record ChipView(string Key, string Episode, string Name, Palette Palette, string Reason, string Elapsed, string Details,
    string Navigation = "unavailable")
{
    public string ToolTip => Name + "\n" + Details + "\n" + (Navigation switch
    {
        "window only" => "Left-click to focus the window only; select the session's tab or pane.",
        "exact terminal" => "Left-click to focus this terminal.",
        "tab or window" => "Left-click to locate the session's tab; falls back to the window if it cannot be identified.",
        "exact tab" => "Left-click to select this tab. Split-pane focus is unchanged.",
        _ => "Terminal navigation is unavailable."
    }) + "\nTime: elapsed waiting for the displayed reason, including the 20-second completion grace."
        + "\nSuccessful chip navigation hides the timer for this wait; the chip stays visible."
        + "\nRight-click to dismiss this wait.";
}
