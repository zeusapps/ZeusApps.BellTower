namespace BellTower.Core;

// Provenance of SessionIdentity.Change. Before this model a Change was written both by users (rename, launch) and
// automatically by the superseded status/instructions selector, which also recorded SelectionRoot and
// SelectionEvent and never cleared them except through the rename path. The shape is stable across an
// older-build rollback because the marker is bound to the Change it was recorded for.
public static class ChangeOrigin
{
    public const string User = "user";
    public const string Launch = "launch";
    public const string LegacySelection = "legacy-selection";
    // A legacy selection that strong verified work replaced. The Change stays for rollback but is never a subject.
    public const string Superseded = "legacy-superseded";

    // The origin of the current Change, derived without mutating (null when there is no Change).
    public static string? Of(SessionIdentity identity) => identity.Change is null ? null :
        identity.ChangeSource is not null && identity.ChangeSourceValue == identity.Change ? identity.ChangeSource : Derive(identity);

    // User and launch values are pins; a legacy automatic selection is lowest-tier work evidence.
    public static bool IsPin(SessionIdentity identity) => Of(identity) is User or Launch;

    public static bool IsLegacySelection(SessionIdentity identity) => Of(identity) == LegacySelection;

    public static bool IsSuperseded(SessionIdentity identity) => Of(identity) == Superseded;

    // The Change still takes part in the association (weak evidence may not fill over it).
    public static bool Holds(SessionIdentity identity) => identity.Change is not null && !IsSuperseded(identity);

    // The automatically selected Change that still counts as lowest-tier work evidence, else null.
    public static string? Legacy(SessionIdentity identity) => IsLegacySelection(identity) ? identity.Change : null;

    // One-way: verified work retires a legacy selection, bound to its current value (a rewritten value is classified again).
    public static void Supersede(SessionIdentity identity)
    {
        if (!IsLegacySelection(identity))
        { return; }
        identity.ChangeSource = Superseded;
        identity.ChangeSourceValue = identity.Change;
    }

    // Classifies once and records the result; reclassifies when an older build changed Change. Returns whether
    // the stored marker changed. Unknown fields are untouched.
    public static bool Classify(SessionIdentity identity)
    {
        var origin = Of(identity);
        var value = origin is null ? null : identity.Change;
        if (identity.ChangeSource == origin && identity.ChangeSourceValue == value)
        { return false; }
        identity.ChangeSource = origin;
        identity.ChangeSourceValue = value;
        return true;
    }

    // An explicit association (rename, launch, inherited pin). Clearing it removes the marker.
    public static void Pin(SessionIdentity identity, string? change, string origin = User)
    {
        identity.Change = change;
        identity.ChangeSource = change is null ? null : origin;
        identity.ChangeSourceValue = change;
    }

    private static string Derive(SessionIdentity identity) =>
        identity.SelectionRoot is not null && identity.SelectionEvent is not null ? LegacySelection : User;
}
