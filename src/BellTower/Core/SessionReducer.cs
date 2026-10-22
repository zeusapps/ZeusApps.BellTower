namespace BellTower.Core;

public static class SessionReducer
{
    public static void Apply(AppState state, Observation observation)
    {
        if (!state.AppliedEvents.Add(observation.Id))
        {
            return;
        }

        if (!state.Sessions.TryGetValue(observation.Key, out var session))
        {
            session = new SessionState
            {
                Key = observation.Key,
                Provider = observation.Provider,
                SessionId = observation.SessionId,
                AttachmentId = observation.AttachmentId
            };
            state.Sessions[observation.Key] = session;
        }

        if (session.AttachmentId != observation.AttachmentId)
        {
            if (observation.Kind != EventKind.Register || observation.At < session.LastObserved)
            {
                return;
            }

            session.AttachmentId = observation.AttachmentId;
            session.Ended = false;
            session.Waits.Clear();
            session.Background.Clear();
            session.Episode = null;
            session.TurnId = null;
            session.CompletedOperations.Clear();
            session.Operations.Clear();
            session.ForegroundStopped = false;
            session.CompletionPending = false;
        }

        if (session.Ended && observation.Kind != EventKind.Register)
        {
            return;
        }

        if (observation.At < session.TurnStarted && observation.Kind != EventKind.BackgroundEnd)
        {
            return;
        }

        if (observation.TurnId is not null && session.TurnId is not null && observation.TurnId != session.TurnId &&
            observation.Kind is not (EventKind.Prompt or EventKind.Register or EventKind.BackgroundEnd))
        {
            return;
        }

        session.LastObserved = observation.At > session.LastObserved ? observation.At : session.LastObserved;
        if (!string.IsNullOrWhiteSpace(observation.Directory))
        {
            session.Directory = observation.Directory;
        }

        for (var index = 0; index < session.Waits.Count; index++)
        {
            var wait = session.Waits[index];
            if (session.Episode is not null && (wait.Episode == session.Episode ||
                wait.Episode is null && wait.Since == session.EpisodeStarted))
            {
                session.Waits[index] = wait with
                {
                    Episode = session.Episode,
                    Acknowledged = wait.Acknowledged || session.AcknowledgedEpisode == session.Episode,
                    Viewed = wait.Viewed || session.ViewedEpisode == session.Episode
                };
            }
        }
        var hadAttention = session.Waits.Count != 0;
        if (observation.OperationId is not null && observation.Kind is EventKind.ToolStart or EventKind.Question)
        {
            session.Operations[observation.OperationId] = new(observation.At, observation.OperationFingerprint);
            session.CompletedOperations.Remove(observation.OperationId);
        }

        switch (observation.Kind)
        {
            case EventKind.Register:
                session.Ended = false;
                break;
            case EventKind.Prompt:
                BeginTurn(session, observation);
                break;
            case EventKind.ToolStart:
                ResumeForeground(session, observation);
                break;
            case EventKind.ToolEnd:
                CompleteOperation(session, observation);
                break;
            case EventKind.Permission:
                AddWait(session, observation, AttentionReason.Permission);
                break;
            case EventKind.Question:
                AddWait(session, observation, AttentionReason.Question);
                break;
            case EventKind.Failure:
                FailForeground(session, observation);
                break;
            case EventKind.Stop:
                StopForeground(session, observation);
                break;
            case EventKind.BackgroundStart:
                StartBackgroundTask(session, observation);
                break;
            case EventKind.BackgroundEnd:
                CompleteBackgroundTask(session, observation);
                break;
            case EventKind.Interrupt:
                InterruptForeground(session, observation);
                break;
            case EventKind.End:
                EndSession(session);
                break;
        }

        if (session.Waits.Count == 0)
        {
            session.Episode = null;
            session.EpisodeStarted = null;
        }
        else if (!hadAttention || session.Episode is null)
        {
            session.Episode = observation.Id;
            session.EpisodeStarted = observation.At;
        }
        else if (session.Waits.All(wait => wait.Episode != session.Episode) &&
            session.Waits.Where(wait => wait.Reason == session.Reason).MaxBy(wait => wait.Since) is { Episode: not null } remaining)
        {
            // Resolving a newer request restores the outstanding blocker's original
            // grace/announcement receipt, rather than giving that older wait a new alert.
            session.Episode = remaining.Episode;
            session.EpisodeStarted = remaining.Since;
            session.AcknowledgedEpisode = remaining.Acknowledged ? remaining.Episode : null;
            session.ViewedEpisode = remaining.Viewed ? remaining.Episode : null;
        }
    }

    private static void BeginTurn(SessionState session, Observation observation)
    {
        if (!observation.IsMainThread)
        {
            return;
        }

        session.TurnId = observation.TurnId;
        session.TurnStarted = observation.At;
        session.ForegroundStopped = false;
        session.CompletionPending = false;
        session.Waits.Clear();
        session.CompletedOperations.Clear();
    }

    private static void ResumeForeground(SessionState session, Observation observation)
    {
        for (var index = session.Waits.Count - 1; index >= 0; index--)
        {
            var wait = session.Waits[index];
            if (wait.Reason != AttentionReason.Permission)
            {
                continue;
            }
            if (wait.Candidates is { } candidates)
            {
                var remaining = candidates.Where(id => id != observation.OperationId && !session.CompletedOperations.Contains(id)).ToArray();
                if (remaining.Length == 0)
                {
                    session.Waits.RemoveAt(index);
                }
                else
                {
                    session.Waits[index] = wait with { Candidates = remaining };
                }
            }
            else if (Matches(wait, observation, session.CompletedOperations))
            {
                session.Waits.RemoveAt(index);
            }
        }
        // A new foreground action establishes resumption after an uncorrelated provider notification.
        if (observation.IsMainThread)
        {
            if (observation.OperationId is not null)
            {
                session.CompletedOperations.Remove(observation.OperationId);
            }

            if (observation.OperationFingerprint is not null)
            {
                session.CompletedOperations.Remove(observation.OperationFingerprint);
            }

            session.ForegroundStopped = false;
            session.CompletionPending = false;
            session.Waits.RemoveAll(wait => wait.Reason is AttentionReason.Done or AttentionReason.Error ||
                (wait.OperationId is null && wait.Fingerprint is null && wait.Reason != AttentionReason.Error));
        }
    }

    private static void CompleteOperation(SessionState session, Observation observation)
    {
        if (observation.OperationId is not null && session.Operations.Remove(observation.OperationId, out var operation) &&
            operation.StartedAt < session.TurnStarted)
        {
            session.CompletedOperations.Add(observation.OperationId);
            return;
        }

        if (observation.OperationId is not null)
        {
            session.CompletedOperations.Add(observation.OperationId);
        }

        if (observation.OperationFingerprint is not null)
        {
            session.CompletedOperations.Add(observation.OperationFingerprint);
        }

        session.Waits.RemoveAll(wait => Matches(wait, observation, session.CompletedOperations));
        if (observation.IsMainThread)
        {
            session.Waits.RemoveAll(wait => wait.Reason == AttentionReason.Done);
        }
    }

    private static void FailForeground(SessionState session, Observation observation)
    {
        if (!observation.IsMainThread)
        {
            return;
        }

        session.ForegroundStopped = true;
        session.CompletionPending = false;
        AddWait(session, observation, AttentionReason.Error);
    }

    private static void StopForeground(SessionState session, Observation observation)
    {
        if (!observation.IsMainThread)
        {
            return;
        }

        session.ForegroundStopped = true;
        session.CompletionPending = true;
        if (session.Waits.RemoveAll(wait => wait.Reason is AttentionReason.Permission or AttentionReason.Question) > 0 && session.Waits.Count == 0)
        {
            session.Episode = null;
        }

        SetBackgroundSnapshot(session, observation);
        if (session.Waits.All(wait => wait.Reason == AttentionReason.Done) && session.Background.Count == 0)
        {
            AddWait(session, observation, AttentionReason.Done);
        }
    }

    private static void StartBackgroundTask(SessionState session, Observation observation)
    {
        var id = observation.BackgroundId ?? observation.OperationId;
        if (id is not null && SuppressesCompletion(observation.BackgroundType ?? "subagent"))
        {
            session.Background.RemoveAll(task => task.Id == id);
            session.Background.Add(new(id, observation.BackgroundType ?? "subagent"));
            session.Waits.RemoveAll(wait => wait.Reason == AttentionReason.Done);
        }
    }

    private static void CompleteBackgroundTask(SessionState session, Observation observation)
    {
        session.Background.RemoveAll(task => task.Id == observation.BackgroundId);
        if (observation.TurnId is null || session.TurnId is null || observation.TurnId == session.TurnId)
        {
            SetBackgroundSnapshot(session, observation);
        }

        if (session.CompletionPending && session.Background.Count == 0 && session.Waits.Count == 0)
        {
            AddWait(session, observation, AttentionReason.Done);
        }
    }

    private static void InterruptForeground(SessionState session, Observation observation)
    {
        if (!observation.IsMainThread)
        {
            return;
        }

        session.Waits.Clear();
        session.ForegroundStopped = true;
        session.CompletionPending = false;
    }

    private static void EndSession(SessionState session)
    {
        session.Ended = true;
        session.CompletionPending = false;
        session.Waits.Clear();
        session.Background.Clear();
    }

    private static bool Matches(WaitState wait, Observation observation, HashSet<string> completed)
    {
        if (wait.Candidates is not null)
        {
            return wait.Candidates.All(completed.Contains);
        }
        if (wait.OperationId is not null)
        {
            return wait.OperationId == observation.OperationId;
        }
        return wait.Fingerprint is not null && wait.Fingerprint == observation.OperationFingerprint;
    }

    private static void AddWait(SessionState session, Observation observation, AttentionReason reason)
    {
        var operationId = observation.OperationId;
        string[]? candidates = null;
        if (operationId is null && observation.OperationFingerprint is not null)
        {
            var matches = session.Operations
                .Where(pair => pair.Value.StartedAt >= session.TurnStarted && pair.Value.Fingerprint == observation.OperationFingerprint)
                .Select(pair => pair.Key).ToArray();
            if (matches.Length == 1)
            {
                operationId = matches[0];
            }
            else if (matches.Length > 1)
            {
                candidates = matches;
            }
        }

        if (reason is AttentionReason.Permission or AttentionReason.Question)
        {
            if (operationId is not null && session.CompletedOperations.Contains(operationId))
            {
                return;
            }

            if (operationId is null && candidates is null && observation.OperationFingerprint is not null &&
                session.CompletedOperations.Contains(observation.OperationFingerprint))
            {
                return;
            }
        }

        session.Waits.RemoveAll(wait => wait.Reason == AttentionReason.Done && reason != AttentionReason.Done);
        if (session.Waits.Any(wait => wait.Reason == reason && wait.OperationId == operationId && wait.Fingerprint == observation.OperationFingerprint))
        {
            return;
        }

        var previousReason = session.Reason;
        session.Waits.Add(new(reason, observation.At, operationId, observation.OperationFingerprint, candidates, observation.Id));
        if (previousReason is null || reason >= previousReason.Value)
        {
            // A distinct blocker needs a new notification even if an older wait is unresolved.
            // Duplicate observations returned above retain their episode and dismissal/view state.
            session.Episode = observation.Id;
            session.EpisodeStarted = observation.At;
        }
    }

    private static void SetBackgroundSnapshot(SessionState session, Observation observation)
    {
        if (observation.BackgroundSnapshot is not null)
        {
            session.Background = observation.BackgroundSnapshot.Where(task => SuppressesCompletion(task.Type)).ToList();
        }
    }

    public static bool SuppressesCompletion(string type) => type is "subagent" or "workflow" or "monitor";
}
