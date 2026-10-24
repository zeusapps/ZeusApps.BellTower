namespace BellTower.Core;

// Cursor and derived-state rules for bounded history recovery (provider neutral).
public static class HistoryState
{
    public const string ClaudeTitleSource = "claude-custom-title";

    // Binds the identity to a transcript. A different file restarts recovery and discards state derived from the old one.
    public static bool Bind(SessionIdentity identity, string path)
    {
        if (identity.TranscriptPath == path)
        {
            return false;
        }
        identity.TranscriptPath = path;
        Reset(identity);
        return true;
    }

    // Clears cursors and everything derived from history. Pins, manual names, Bell Tower-written
    // names (NativeLastName), palettes and live work evidence are not derived from history and stay.
    public static void Reset(SessionIdentity identity)
    {
        identity.HistoryOffset = 0;
        identity.HistoryLength = 0;
        identity.HistoryFingerprint = null;
        identity.HistoryResults.Clear();
        identity.HistoryCalls.Clear();
        identity.NativeTitleOffset = -1;
        identity.HistoryWorkOffset = -1;
        identity.ForkParent = null;
        identity.ForkBoundaryOffset = -1;
        if (identity.NativeTitleSource == ClaudeTitleSource)
        {
            identity.NativeTitle = null;
            identity.NativeTitleSource = null;
        }
        if (identity.WorkSource?.StartsWith("history:", StringComparison.Ordinal) == true)
        {
            WorkPolicy.Clear(identity, WorkClear.History);
        }
    }
}
