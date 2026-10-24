namespace BellTower.Core;

// A forked conversation inherits its parent's Bell Tower identity once, from a parent proven by transcript evidence.
// It keeps its own palette (a distinguishable colour), its own checkout, its native chat state and its history cursors.
public static class IdentityInheritance
{
    public static void FromParent(SessionIdentity child, SessionIdentity parent, string parentSessionId)
    {
        child.ForkInheritedFrom = parentSessionId;
        // The parent's branch invalidation holds for the fork: copied records older than it are never work. One-way, never lowers.
        if (parent.WorkFloorAt is { } floor && (child.WorkFloorAt is null || child.WorkFloorAt < floor))
        { child.WorkFloorAt = floor; }
        child.ManualName ??= parent.ManualName;
        if (child.Change is null && parent.Change is not null && !ChangeOrigin.IsSuperseded(parent))
        {
            child.Change = parent.Change;
            child.ChangeSource = ChangeOrigin.Of(parent);
            child.ChangeSourceValue = parent.Change;
        }
        if (child.ActiveWork is null && parent.ActiveWork is not null)
        {
            child.ActiveWork = parent.ActiveWork;
            child.WorkWeak = parent.WorkWeak;
            child.WorkSource = "fork:" + (parent.WorkSource ?? "work");
            child.WorkBranch = parent.WorkBranch;
            child.SelectionRoot = parent.SelectionRoot;
            child.SelectionAt = parent.SelectionAt;
            child.HistoryWorkOffset = -1;
        }
    }
}
