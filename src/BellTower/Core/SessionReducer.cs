namespace BellTower.Core;

public static class SessionReducer
{
    public static void Apply(AppState state, Observation observation)
    {
        if (observation.IngressAt is not null || state.Sessions.TryGetValue(observation.Key, out var existing) && existing.CausalEvents.Count > 0)
        {
            CausalSessionReducer.Apply(state, observation);
        }
        else
        {
            ApplyUnordered(state, observation);
        }
    }

    internal static void ApplyUnordered(AppState state, Observation observation)
    {
        if (!state.AppliedEvents.Add(observation.Id))
        {
            return;
        }

        observation = Normalize(observation);
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
            session.ApprovalRevision++;
            session.Ended = false;
            session.Waits.Clear();
            session.Background.Clear();
            session.Episode = null;
            session.TurnId = null;
            session.CompletedOperations.Clear();
            session.Operations.Clear();
            ClearApprovalMemory(session);
            session.PromptObserved = false;
            session.ForegroundStopped = false;
            session.CompletionPending = false;
        }

        if (session.Ended && (observation.Kind != EventKind.Register || observation.At <= session.LastObserved ||
            observation.IngressAt is not null && observation.RegistrationSource != "resume"))
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

        var permissionScopeBefore = PermissionScope(session);

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
            if (session.CompletedOperations.Contains(observation.OperationId))
            {
                return;
            }
            session.Operations[observation.OperationId] = new(observation.At, observation.OperationFingerprint);
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
            case EventKind.Permission when observation.HumanPrompt:
                ConfirmPrompt(session, observation);
                break;
            case EventKind.Permission when observation.QuestionTool:
                ApproveQuestionTool(session, observation);
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

        else if (session.Waits.All(wait => wait.Episode != session.Episode))
        {
            // Resolving a newer request restores the outstanding blocker's original
            // grace/announcement receipt, rather than giving that older wait a new alert.
            // Active attention is preferred; provisional-only waits keep an episode for later confirmation.
            var pool = session.Waits.Where(SessionState.IsConfirmed).ToList() is { Count: > 0 } active ? active : session.Waits;
            var top = pool.Max(wait => wait.Reason);
            if (pool.Where(wait => wait.Reason == top).MaxBy(wait => wait.Since) is { Episode: not null } remaining)
            {
                session.Episode = remaining.Episode;
                session.EpisodeStarted = remaining.Since;
                session.AcknowledgedEpisode = remaining.Acknowledged ? remaining.Episode : null;
                session.ViewedEpisode = remaining.Viewed ? remaining.Episode : null;
            }
        }

        if (!permissionScopeBefore.SequenceEqual(PermissionScope(session), StringComparer.Ordinal))
        {
            session.ApprovalRevision++;
        }
    }

    // A producer older than the human-wait fields (a cached ingress image from before this change) sends permission_prompt as a
    // bare Permission with neither a tool ID nor a fingerprint. Re-derive that legacy shape as human-prompt evidence. A legacy
    // PermissionRequest always carries a tool and input, hence a fingerprint, so a tool-bearing request is never promoted.
    internal static Observation Normalize(Observation observation) =>
        observation.AdapterRevision is null && observation.Kind == EventKind.Permission && !observation.HumanPrompt && !observation.QuestionTool &&
        !observation.RuntimeApproval && observation.OperationId is null && observation.OperationFingerprint is null
            ? observation with { HumanPrompt = true, ProviderDelay = observation.ProviderDelay ?? SessionState.HumanWaitGrace, AdapterRevision = Observation.HumanWaitRevision }
            : observation;

    private static void ClearApprovalMemory(SessionState session)
    {
        session.ResolvedApprovals.Clear();
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
        ClearApprovalMemory(session);
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
                    Resolve(session, wait, observation.At);
                    session.Waits.RemoveAt(index);
                }
                else
                {
                    session.Waits[index] = wait with { Candidates = remaining };
                }
            }
            else if (Matches(wait, observation, session.CompletedOperations))
            {
                Resolve(session, wait, observation.At);
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
            RemoveResolved(session, wait => wait.Reason is AttentionReason.Done or AttentionReason.Error ||
                (wait.OperationId is null && wait.Fingerprint is null && wait.Reason != AttentionReason.Error), observation.At);
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

        RemoveResolved(session, wait => Matches(wait, observation, session.CompletedOperations), observation.At);
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
        ClearApprovalMemory(session);
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
        ClearApprovalMemory(session);
        session.ForegroundStopped = true;
        session.CompletionPending = false;
    }

    private static void EndSession(SessionState session)
    {
        session.Ended = true;
        session.CompletionPending = false;
        ClearApprovalMemory(session);
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

    private static string[] PermissionScope(SessionState session) => session.Waits
        .Where(wait => wait.Reason == AttentionReason.Permission)
        .Select(wait => wait.ConfirmationScopeId)
        .Order(StringComparer.Ordinal)
        .ToArray();

    private static void AddWait(SessionState session, Observation observation, AttentionReason reason, bool confirmed = false)
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
        // A question dialog's approval flow can arrive before its tool start; the later start adopts that wait instead of duplicating it.
        if (reason == AttentionReason.Question && operationId is not null && observation.OperationFingerprint is not null)
        {
            var adopt = session.Waits.FindIndex(wait => wait.Reason == reason && wait.OperationId is null && wait.Fingerprint == observation.OperationFingerprint);
            if (adopt >= 0)
            {
                session.Waits[adopt] = session.Waits[adopt] with { OperationId = operationId };
                return;
            }
        }

        if (session.Waits.Any(wait => wait.Reason == reason && wait.OperationId == operationId && wait.Fingerprint == observation.OperationFingerprint))
        {
            return;
        }

        var previousReason = session.StoredReason;
        var actionable = confirmed || reason != AttentionReason.Permission;
        var created = new WaitState(reason, observation.At, operationId, observation.OperationFingerprint, candidates, observation.Id)
        {
            Confirmation = reason is AttentionReason.Permission or AttentionReason.Question ? HumanConfirmation.Provisional : null
        };
        session.Waits.Add(confirmed ? WithConfirmation(created, observation.At, observation.ProviderDelay, false) : created);
        // A provisional approval request is not attention, so it only owns the episode when nothing else is actionable.
        // This keeps a newer unconfirmed or auto-approved request from re-announcing or un-dismissing an older wait.
        if (previousReason is null || actionable && reason >= previousReason.Value)
        {
            // A distinct blocker needs a new notification even if an older wait is unresolved.
            // Duplicate observations returned above retain their episode and dismissal/view state.
            session.Episode = observation.Id;
            session.EpisodeStarted = observation.At;
        }
    }

    // The shared grace counts the time a person already waited: the credited provider delay or the time seen since the request.
    // Runtime evidence has no earlier delay to credit: its grace starts at the runtime's first report.
    private static WaitState WithConfirmation(WaitState wait, DateTimeOffset at, TimeSpan? providerDelay, bool runtime)
    {
        var elapsed = !runtime && at > wait.Since ? at - wait.Since : TimeSpan.Zero;
        var delay = providerDelay ?? TimeSpan.Zero;
        return wait with
        {
            Confirmation = HumanConfirmation.Confirmed,
            ConfirmedAt = at,
            ConfirmationCredit = elapsed > delay ? elapsed : delay
        };
    }

    // Question dialogs run through the provider's approval flow. That flow keeps the pending question a question (it may be
    // the only evidence a dialog exists) and is never a new permission wait.
    private static void ApproveQuestionTool(SessionState session, Observation observation)
    {
        // After the turn stopped (for example a rejected dialog) a late approval hook cannot start a question.
        if (session.ForegroundStopped || session.Waits.Any(wait => wait.Reason == AttentionReason.Question && Targets(wait, observation)))
        {
            return;
        }
        AddWait(session, observation, AttentionReason.Question);
    }

    private static bool Targets(WaitState wait, Observation observation) =>
        observation.OperationId is { } id ? wait.OperationId == id || wait.Candidates?.Contains(id) == true :
        observation.OperationFingerprint is not null && wait.Fingerprint == observation.OperationFingerprint;

    // A prompt notification racing the resolution of another request is not told apart from a late notification for that request.
    private static readonly TimeSpan RacingResolutionWindow = TimeSpan.FromSeconds(2);

    // A human-prompt signal confirms a pending wait by operation, fingerprint, or (unattributed) the turn scope. When the
    // scope holds both a question and a permission request nothing may be assigned by guess. A provider notification delay
    // gates the source: a request younger than that delay cannot be what the prompt is about.
    private static void ConfirmPrompt(SessionState session, Observation observation)
    {
        if (session.ForegroundStopped)
        {
            return;
        }
        if (observation.RuntimeApproval)
        {
            var inspectedScope = observation.RuntimeApprovalScope;
            if (inspectedScope is not { Length: > 0 } || observation.RuntimeApprovalRevision != session.ApprovalRevision)
            {
                return;
            }

            var permissionIndices = Enumerable.Range(0, session.Waits.Count)
                .Where(index => session.Waits[index].Reason == AttentionReason.Permission).ToList();
            var currentScope = permissionIndices.Select(index => session.Waits[index].ConfirmationScopeId)
                .Order(StringComparer.Ordinal).ToArray();
            var requestedScope = inspectedScope.Order(StringComparer.Ordinal).ToArray();
            // The runtime status is session-wide. If any inspected request resolved, a new one appeared, or the set otherwise
            // changed while the read was in flight, the result cannot be assigned to the remaining/new request by elimination.
            if (currentScope.Length == 0 || !currentScope.SequenceEqual(requestedScope, StringComparer.Ordinal))
            {
                return;
            }

            // The owning runtime reports a pending approval but not which request. While a confirmed permission wait is already
            // outstanding, a continuing positive status is that wait's evidence, not newer requests': credit nothing. Otherwise
            // confirm each provisional one as a set. It never creates a wait.
            if (session.Waits.Any(wait => wait.Reason == AttentionReason.Permission && wait.Confirmation == HumanConfirmation.Confirmed))
            {
                return;
            }
            foreach (var index in permissionIndices)
            {
                // A wait that predates the tray run (RestoredSince) is confirmed back-dated so it restores silently; the
                // observation itself keeps its journal position.
                var restored = observation.RestoredSince is { } anchor && session.Waits[index].Since <= anchor;
                Confirm(session, index, restored ? session.Waits[index].Since.AddMilliseconds(1) : observation.At, null, true, observation.Id, observation.At);
            }
            return;
        }
        session.PromptObserved = true;
        var attributed = observation.OperationId is not null || observation.OperationFingerprint is not null;
        var scope = Enumerable.Range(0, session.Waits.Count).Where(index => session.Waits[index].Reason is AttentionReason.Question or AttentionReason.Permission &&
            (!attributed || Targets(session.Waits[index], observation)) && (session.Waits[index].Reason != AttentionReason.Permission ||
            OldEnoughToBeTheSource(session.Waits[index], observation.At, observation.ProviderDelay))).ToList();
        var questions = scope.Where(index => session.Waits[index].Reason == AttentionReason.Question).ToList();
        var permissions = scope.Where(index => session.Waits[index].Reason == AttentionReason.Permission).ToList();
        if (questions.Count != 0 && permissions.Count != 0)
        {
            foreach (var index in scope)
            {
                session.Waits[index] = session.Waits[index] with { Ambiguous = true };
            }
            // The prompt is never credited to either by guess, and not later by elimination when the question resolves: the
            // question's own dialog is a normal source of it, so a permission request relies on its own later prompt.
            return;
        }
        if (questions.Count != 0)
        {
            if (questions.Count == 1)
            {
                Confirm(session, questions[0], observation.At, observation.ProviderDelay, false, observation.Id, observation.At);
            }
            else
            {
                questions.ForEach(index => session.Waits[index] = session.Waits[index] with { Ambiguous = true });
            }
            return;
        }
        if (permissions.Count != 0)
        {
            if (!attributed && permissions.Any(index => session.Waits[index].Confirmation != HumanConfirmation.Confirmed) &&
                session.ResolvedApprovals.Any(receipt => receipt.At <= observation.At && observation.At - receipt.At <= RacingResolutionWindow))
            {
                // A request just resolved and others remain: the prompt may be its late notification, so confirm none of them.
                permissions.ForEach(index => session.Waits[index] = session.Waits[index] with { Ambiguous = true });
                return;
            }
            // Several parallel candidates are confirmed as a set (a human wait exists among them) without picking one.
            foreach (var index in permissions)
            {
                Confirm(session, index, observation.At, observation.ProviderDelay, false, observation.Id, observation.At);
                session.Waits[index] = session.Waits[index] with { Ambiguous = permissions.Count > 1 };
            }
            return;
        }
        // Confirmation without an observed approval start (missing precursor) is only a wait when it cannot belong to one that resolved.
        if (IsStale(session, observation))
        {
            return;
        }
        AddWait(session, observation, AttentionReason.Permission, confirmed: true);
    }

    private static bool OldEnoughToBeTheSource(WaitState wait, DateTimeOffset promptAt, TimeSpan? providerDelay) =>
        wait.Since + (providerDelay ?? TimeSpan.Zero) <= promptAt;

    private static void Confirm(SessionState session, int index, DateTimeOffset at, TimeSpan? providerDelay, bool runtime, string fallbackEpisode, DateTimeOffset observedAt)
    {
        var wait = session.Waits[index];
        if (wait.Confirmation == HumanConfirmation.Confirmed)
        {
            return;
        }
        var wasActive = SessionState.IsConfirmed(wait);
        var previousReason = session.StoredReason;
        var confirmed = WithConfirmation(wait, at, providerDelay, runtime);
        if (wait.Reason == AttentionReason.Permission && !wasActive && (previousReason is null || wait.Reason >= previousReason.Value))
        {
            // A request that stayed provisional for the unverified grace may already have shown a "possibly waiting" chip, which the
            // user can dismiss or view. That chip was a guess, not an announced human wait, so confirmation starts a fresh episode
            // that dismissal cannot suppress. The decision uses only stored times, never the dismissal receipts, so the causal
            // replay and the unordered path agree. Legacy unverified waits keep their episode so an upgrade never resurrects
            // a dismissed wait.
            var own = wait.Episode ?? fallbackEpisode;
            var seen = wait.Confirmation == HumanConfirmation.Provisional && observedAt - wait.Since >= SessionState.UnverifiedGrace;
            var episode = seen ? fallbackEpisode : own;
            session.Waits[index] = confirmed with { Episode = episode, Acknowledged = seen ? false : confirmed.Acknowledged, Viewed = seen ? false : confirmed.Viewed };
            session.Episode = episode;
            session.EpisodeStarted = wait.Since;
        }
        else
        {
            session.Waits[index] = confirmed;
        }
    }

    private static bool IsStale(SessionState session, Observation observation)
    {
        if (observation.OperationId is { } id && session.CompletedOperations.Contains(id) ||
            observation.OperationFingerprint is { } fingerprint && session.CompletedOperations.Contains(fingerprint))
        {
            return true;
        }
        // An attributed signal is judged by its own identity; an unattributed one cannot be told from a late notification for a
        // wait that just resolved, so a bounded receipt window rejects it.
        return observation.OperationId is not null || observation.OperationFingerprint is not null
            ? session.ResolvedApprovals.Any(receipt => observation.OperationId is { } operation && receipt.OperationId == operation ||
                observation.OperationFingerprint is { } print && receipt.Fingerprint == print)
            : session.ResolvedApprovals.Any(receipt => receipt.At <= observation.At && observation.At - receipt.At <= SessionState.StaleConfirmationWindow);
    }

    private static void Resolve(SessionState session, WaitState wait, DateTimeOffset at)
    {
        if (wait.Reason is not (AttentionReason.Permission or AttentionReason.Question))
        {
            return;
        }
        session.ResolvedApprovals.Add(new(at, wait.OperationId, wait.Fingerprint));
        if (session.ResolvedApprovals.Count > 64)
        {
            session.ResolvedApprovals.RemoveRange(0, session.ResolvedApprovals.Count - 64);
        }
    }

    private static void RemoveResolved(SessionState session, Predicate<WaitState> predicate, DateTimeOffset at)
    {
        foreach (var wait in session.Waits.Where(wait => predicate(wait)).ToList())
        {
            Resolve(session, wait, at);
        }
        session.Waits.RemoveAll(predicate);
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
