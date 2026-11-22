namespace BellTower.Core;

// A bounded replay window makes async delivery deterministic without waiting for a missing hook.
public static class CausalSessionReducer
{
    public const int Window = 256;

    public static void Apply(AppState state, Observation observation)
    {
        if (!state.AppliedEvents.Add(observation.Id))
        {
            return;
        }
        state.Sessions.TryGetValue(observation.Key, out var previous);
        var journal = previous?.CausalEvents ?? [];
        var checkpoint = previous?.CausalCheckpoint;
        var floor = previous?.CausalFloor;
        if (floor is not null && Compare(observation, floor) <= 0)
        {
            return;
        }
        if (journal.Any(e => e.Id == observation.Id))
        {
            return;
        }
        if (previous is not null && journal.Count == 0 && checkpoint is null)
        {
            checkpoint = Copy(previous);
        }
        journal.Add(observation);
        journal.Sort(Compare);
        if (journal.Count > Window)
        {
            var committed = new AppState();
            if (checkpoint is not null)
            {
                committed.Sessions[observation.Key] = Copy(checkpoint);
            }
            SessionReducer.ApplyUnordered(committed, journal[0]);
            checkpoint = Copy(committed.Sessions[observation.Key]);
            floor = journal[0];
            journal.RemoveAt(0);
        }
        var replay = new AppState();
        if (checkpoint is not null)
        {
            replay.Sessions[observation.Key] = Copy(checkpoint);
        }
        foreach (var item in journal)
        {
            SessionReducer.ApplyUnordered(replay, item);
        }
        var result = replay.Sessions[observation.Key];
        result.CausalEvents = journal;
        result.CausalCheckpoint = checkpoint;
        result.CausalFloor = floor;
        if (previous is not null)
        {
            result.AcknowledgedEpisode = previous.AcknowledgedEpisode;
            result.ViewedEpisode = previous.ViewedEpisode;
            result.NativePermissionPending = previous.NativePermissionPending;
            result.NativePermissionSince = previous.NativePermissionSince;
            result.AttachmentHealth = previous.AttachmentId == result.AttachmentId ? previous.AttachmentHealth : "unverified";
            result.AttachmentUnverifiedSince = previous.AttachmentId == result.AttachmentId ? previous.AttachmentUnverifiedSince : null;
            result.AdditionalData = previous.AdditionalData;
            foreach (var wait in previous.Waits.Where(w => w.Acknowledged || w.Viewed))
            {
                for (var i = 0; i < result.Waits.Count; i++)
                {
                    if (result.Waits[i].Episode == wait.Episode)
                    {
                        result.Waits[i] = result.Waits[i] with { Acknowledged = wait.Acknowledged, Viewed = wait.Viewed };
                    }
                }
            }
        }
        state.Sessions[observation.Key] = result;
    }

    /// <summary>
    /// Folds the replay history of an ended session. The journal observations at or before the session's last applied observation
    /// (<see cref="SessionState.LastObserved"/>) are folded away and the floor moves to the latest of them, never backward, so delayed earlier
    /// events stay ignored. Later observations were ignored by the ended session, for example a resumed client's prompt delivered before its
    /// resume registration; they stay in the journal (bounded by the window) against a checkpoint copied from the compacted session, so a late
    /// resume still sorts ahead of them. Operation memory is dropped because ended sessions ignore non-registration events and revival clears
    /// it. Returns whether anything changed; a second call with nothing new to fold changes nothing.
    /// </summary>
    public static bool Compact(SessionState session)
    {
        if (!session.Ended)
        {
            return false;
        }
        var changed = false;
        // The journal is sorted by Compare, which orders by time first, so what is at or before LastObserved is a prefix.
        var folded = session.CausalEvents.TakeWhile(e => e.At <= session.LastObserved).Count();
        if (folded > 0)
        {
            var floor = session.CausalEvents[folded - 1];
            if (session.CausalFloor is null || Compare(floor, session.CausalFloor) > 0)
            {
                session.CausalFloor = floor;
            }
            session.CausalEvents = session.CausalEvents.Skip(folded).ToList();
            session.CausalCheckpoint = null;
            changed = true;
        }
        if (session.CausalEvents.Count == 0)
        {
            if (session.CausalCheckpoint is not null)
            {
                session.CausalCheckpoint = null;
                changed = true;
            }
        }
        if (session.CompletedOperations.Count > 0 || session.Operations.Count > 0)
        {
            session.CompletedOperations = [];
            session.Operations = [];
            changed = true;
        }
        if (folded > 0 && session.CausalEvents.Count > 0)
        {
            // The kept observations replay against the compacted session itself (they change nothing while it is ended).
            session.CausalCheckpoint = Copy(session);
        }
        return changed;
    }

    private static int Compare(Observation first, Observation second)
    {
        var time = first.At.CompareTo(second.At);
        if (time != 0)
        {
            return time;
        }
        var kind = first.Kind.CompareTo(second.Kind);
        return kind != 0 ? kind : StringComparer.Ordinal.Compare(first.Id, second.Id);
    }

    private static SessionState Copy(SessionState source) => new()
    {
        Key = source.Key,
        Provider = source.Provider,
        SessionId = source.SessionId,
        AttachmentId = source.AttachmentId,
        Directory = source.Directory,
        TurnId = source.TurnId,
        TurnStarted = source.TurnStarted,
        LastObserved = source.LastObserved,
        Ended = source.Ended,
        AttachmentHealth = source.AttachmentHealth,
        AttachmentUnverifiedSince = source.AttachmentUnverifiedSince,
        ForegroundStopped = source.ForegroundStopped,
        CompletionPending = source.CompletionPending,
        Background = [.. source.Background],
        Waits = [.. source.Waits],
        ApprovalRevision = source.ApprovalRevision,
        ResolvedApprovals = [.. source.ResolvedApprovals],
        PromptObserved = source.PromptObserved,
        CompletedOperations = [.. source.CompletedOperations],
        Operations = new(source.Operations),
        Episode = source.Episode,
        EpisodeStarted = source.EpisodeStarted,
        AcknowledgedEpisode = source.AcknowledgedEpisode,
        ViewedEpisode = source.ViewedEpisode,
        AdditionalData = source.AdditionalData is null ? null : new(source.AdditionalData)
    };
}
