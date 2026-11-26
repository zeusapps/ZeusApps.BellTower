namespace BellTower.FieldAcceptance.Harness;

/// <summary>
/// The mechanism of each documented hook loss, as pure predicates over one session's raw state (QA-241, ANALYSIS-243, -262, -263). A loss is real only when the
/// probe event reached the tray's journal (so delivery, spool and rejection causes are excluded), was not applied, and carries exactly the attachment id the
/// loss is documented to carry while the session sits on exactly the id it is documented to sit on. A reversed or different pair is another defect and fails.
/// </summary>
public static class LossPredicates
{
    /// <summary>The probe's own journal event was ignored because its attachment id is <paramref name="eventId"/> while the session is on <paramref name="sessionId"/>.</summary>
    public static bool IdLoss(StateView state, string operation, string sessionId, string eventId) =>
        state.JournalOperationIds.Contains(operation) && !state.Completed.Contains(operation) && state.AttachmentId == sessionId && state.AttachmentOfOperation(operation) == eventId;

    /// <summary>
    /// A plain launch with no identifiable process: every hook got its own random attachment id, so the later probe event (a pre hook) is in the journal under an id
    /// other than the session's, it made no wait, and the journal holds at least the four events sent.
    /// </summary>
    public static bool FreshIdLoss(StateView state, string operation) =>
        state.Waits == 0 && state.Journal.Count >= 4 && state.JournalOperationIds.Contains(operation) && state.AttachmentOfOperation(operation) is { Length: > 0 } id && id != state.AttachmentId &&
        state.JournalAttachmentIds.Count > 1;
}

/// <summary>When a state that depends on a clock (the attachment grace) can be compared once instead of retried (ANALYSIS-261).</summary>
public static class StableState
{
    /// <summary>How long to wait so that every conversation's last hook is older than the grace plus a margin; zero when that is already so.</summary>
    public static TimeSpan RemainingQuiet(IEnumerable<DateTime> lastHookUtc, TimeSpan grace, TimeSpan margin, DateTime nowUtc)
    {
        var last = lastHookUtc.DefaultIfEmpty(DateTime.MinValue).Max();
        var due = last == DateTime.MinValue ? nowUtc : last + grace + margin;
        return due > nowUtc ? due - nowUtc : TimeSpan.Zero;
    }
}
