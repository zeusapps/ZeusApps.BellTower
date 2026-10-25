using BellTower.Hosts;
using BellTower.IO;

namespace BellTower.Tray;

/// <summary>Decides what a finished chip navigation changes. Only a confirmed success may hide a wait's timer.</summary>
public static class NavigationOutcome
{
    /// <summary>
    /// Marks the episode viewed for a successful chip navigation and returns true. A failed, denied or partial
    /// result, a click that does not hide timers, or a missing episode changes nothing and returns false.
    /// </summary>
    public static bool ApplyViewed(StateStore store, string key, string? episode, FocusResult result, bool hideTimer)
    {
        if (!result.Success || !hideTimer || episode is null)
        {
            return false;
        }

        store.MarkViewed(key, episode);
        return true;
    }
}
