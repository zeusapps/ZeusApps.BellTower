using BellTower.Hosts;
using BellTower.IO;

namespace BellTower.Tray;

/// <summary>Decides what a finished chip navigation changes. A confirmed success or a shown permanent limit may hide a wait's timer.</summary>
public static class NavigationOutcome
{
    /// <summary>
    /// Marks the episode viewed for a successful chip navigation or one that stated a permanent limit (no terminal destination
    /// exists for the session) and returns true. A temporary failure (declined activation, stale session, navigator failure,
    /// unverified terminal), a click that does not hide timers, or a missing episode changes nothing and returns false.
    /// </summary>
    public static bool ApplyViewed(StateStore store, string key, string? episode, FocusResult result, bool hideTimer)
    {
        if (!(result.Success || NavigationReasons.IsPermanentLimit(result)) || !hideTimer || episode is null)
        {
            return false;
        }

        store.MarkViewed(key, episode);
        return true;
    }

    /// <summary>
    /// The status after the viewed-state save failed: it keeps the click's own stated result and adds only the save failure, so a refused
    /// permanent-limit click never reads as a focused session.
    /// </summary>
    public static string SaveFailedStatus(FocusResult result) => result.Success
        ? "Session focused; the timer preference could not be saved."
        : $"{result.Message} The timer preference could not be saved.";

    /// <summary>
    /// True when a chip click must also show a visible message: a failure, or a success below an exact destination (window only,
    /// terminal only). The window may already be in front, so focusing it alone looks like nothing happened. Only an exact tab or
    /// terminal needs no extra notice. Applies to both providers and both hosts.
    /// </summary>
    public static bool NeedsNotice(FocusResult result) =>
        !result.Success || !result.Precision.StartsWith("exact", StringComparison.Ordinal);
}
