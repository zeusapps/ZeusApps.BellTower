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
        CompletedOperations = [.. source.CompletedOperations],
        Operations = new(source.Operations),
        Episode = source.Episode,
        EpisodeStarted = source.EpisodeStarted,
        AcknowledgedEpisode = source.AcknowledgedEpisode,
        ViewedEpisode = source.ViewedEpisode,
        AdditionalData = source.AdditionalData is null ? null : new(source.AdditionalData)
    };
}
