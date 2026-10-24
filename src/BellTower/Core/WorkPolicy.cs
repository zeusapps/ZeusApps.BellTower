namespace BellTower.Core;

// Normalized provider-neutral evidence that a conversation worked on an OpenSpec change.
// Offset is the history record position (-1 for live hook observations).
public sealed record WorkEvidence(string Change, string Root, string Source, bool Weak, DateTimeOffset At, long Offset = -1);

// One policy for both providers. Adapters only translate native tool calls into WorkEvidence.
public static class WorkPolicy
{
    // Applies evidence to an identity. Weak evidence only fills an empty association; strong evidence
    // replaces weak or older strong work. History never overrides newer live work and older history
    // never overrides newer history, so replay order and chunk order do not change the outcome.
    public static bool Apply(SessionIdentity identity, WorkEvidence evidence)
    {
        var history = evidence.Offset >= 0;
        // History stamped before the last branch invalidation never becomes work again; an unparseable stamp is not newer.
        if (history && identity.WorkFloorAt is { } floor && evidence.At < floor)
        { return false; }
        // A legacy automatic selection yields only to strictly newer verified work.
        if (history && identity.ActiveWork is null && ChangeOrigin.IsLegacySelection(identity) &&
            identity.SelectionAt is { } legacyAt && evidence.At <= legacyAt)
        { return false; }
        if (evidence.Weak)
        {
            if (ChangeOrigin.Holds(identity) || identity.ActiveWork is not null)
            {
                return false;
            }
        }
        else if (identity.ActiveWork is not null && !identity.WorkWeak)
        {
            if (identity.ActiveWork == evidence.Change && identity.SelectionRoot == evidence.Root && !history)
            {
                // Repeated work on the same change only refreshes the receipt time.
                if (identity.SelectionAt is { } known && known >= evidence.At)
                {
                    return false;
                }
            }
            else if (history)
            {
                var newer = identity.HistoryWorkOffset >= 0 ? evidence.Offset > identity.HistoryWorkOffset &&
                    (identity.SelectionAt is null || evidence.At >= identity.SelectionAt) : identity.SelectionAt is null || evidence.At > identity.SelectionAt;
                if (!newer || identity.ActiveWork == evidence.Change && identity.SelectionRoot == evidence.Root && identity.HistoryWorkOffset >= 0)
                {
                    return false;
                }
            }
            // Live evidence has already been ordered causally by the observer, so a later arrival
            // that wins that ordering replaces earlier work regardless of receipt time.
        }

        if (!evidence.Weak)
        { ChangeOrigin.Supersede(identity); }
        identity.ActiveWork = evidence.Change;
        identity.WorkWeak = evidence.Weak;
        identity.WorkSource = evidence.Source;
        identity.WorkBranch = identity.ObservedBranch;
        identity.SelectionRoot = evidence.Root;
        identity.SelectionAt = evidence.At;
        identity.HistoryWorkOffset = history ? evidence.Offset : -1;
        return true;
    }

    // The single place that clears work evidence. Branch keeps the planning root (the change folders still exist) and
    // writes a time floor so work stamped before the switch cannot be recovered again, whatever the cursors do;
    // History is a transcript restart: the cursors are reset with it but the floor of the conversation survives.
    public static void Clear(SessionIdentity identity, WorkClear scope, DateTimeOffset? now = null)
    {
        identity.ActiveWork = null;
        identity.WorkBranch = null;
        identity.WorkSource = null;
        identity.WorkWeak = false;
        identity.HistoryWorkOffset = -1;
        identity.SelectionAt = null;
        if (scope == WorkClear.History)
        {
            identity.SelectionRoot = null;
        }
        else
        {
            var at = now ?? DateTimeOffset.UtcNow;
            identity.WorkFloorAt = identity.WorkFloorAt is { } known && known > at ? known : at;
        }
    }
}

public enum WorkClear
{
    Branch,
    History
}
