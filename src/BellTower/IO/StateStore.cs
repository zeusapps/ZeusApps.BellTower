using System.Text.Json;
using BellTower.Core;
using BellTower.Protocol;
using BellTower.Providers;

namespace BellTower.IO;

public sealed class StateStore
{
    private readonly DataPaths paths;
    private readonly StateSnapshotStore snapshots;
    private readonly AttachmentStore attachments;
    private readonly ConfigStore configuration;
    private readonly Func<ProcessIdentity, ProcessLiveness> inspect;
    private readonly TimeProvider time;

    /// <summary>An ended session untouched for this long is compacted: it keeps what it displays and its ordering floor, not its replay history.</summary>
    public static readonly TimeSpan SettleInterval = TimeSpan.FromMinutes(10);
    public const int MaxEndedSessions = 200;
    private const int InboxBatchSize = 2000;
    public static readonly TimeSpan EndedRetention = TimeSpan.FromDays(30);

    /// <param name="inspect">Process liveness check (PID plus creation time); injectable for deterministic tests.</param>
    /// <param name="time">Clock for attachment reconciliation; injectable for deterministic tests.</param>
    /// <param name="budgetBytes">Snapshot budget; production keeps the default, tests inject a reduced one.</param>
    public StateStore(DataPaths paths, Func<ProcessIdentity, ProcessLiveness>? inspect = null, TimeProvider? time = null,
        long budgetBytes = StateSnapshotStore.DefaultBudgetBytes)
    {
        this.time = time ?? TimeProvider.System;
        this.inspect = inspect ?? ProcessProbe.Inspect;
        this.paths = paths;
        snapshots = new StateSnapshotStore(paths, budgetBytes);
        attachments = new AttachmentStore(paths);
        configuration = new ConfigStore(paths);
    }

    public AppState Read() => snapshots.Read();

    public IReadOnlyList<ScheduledReminder> AdvanceReminders(
        IReadOnlyCollection<SessionState> actionable,
        DateTimeOffset now,
        TimeSpan? idle,
        ReminderSettings settings,
        string runId)
    {
        using var gate = new DataLock(paths, "state");
        var state = snapshots.Read();
        var current = actionable.Where(session => IsCurrentActionable(state, session, now)).ToArray();
        var retained = state.Sessions.Values.Where(session => !session.Ended && session.Episode is not null && session.AcknowledgedEpisode != session.Episode)
            .Select(session => ReminderPolicy.Key(session)).ToHashSet(StringComparer.Ordinal);
        var advance = ReminderPolicy.Advance(state, current, now, idle, settings, runId, retained);
        if (advance.Changed)
        {
            snapshots.Save(state);
        }

        return advance.Scheduled;
    }

    public IReadOnlyList<EscalationEligibility> Escalations(
        IReadOnlyCollection<SessionState> currentSessions,
        DateTimeOffset now)
    {
        var settings = configuration.Read().Reminders;
        if (!settings.Enabled)
        {
            return [];
        }

        using var gate = new DataLock(paths, "state");
        var state = snapshots.Read();
        var current = currentSessions.Where(session => IsCurrentActionable(state, session, now))
            .Select(session => ReminderPolicy.Key(session)).ToHashSet(StringComparer.Ordinal);
        return state.ReminderReceipts.Where(pair => pair.Value.Escalation is not null && current.Contains(pair.Key))
            .Select(pair => pair.Value.Escalation!)
            .OrderBy(receipt => receipt.EligibleAt)
            .ToArray();
    }

    private static bool IsCurrentActionable(AppState state, SessionState session, DateTimeOffset now) =>
        session.AttachmentHealth == "verified-live" && session.RequiresAttention(now) && session.Episode is not null && session.Reason is not null &&
        state.Sessions.TryGetValue(session.Key, out var persisted) && !persisted.Ended && persisted.AttachmentHealth == "verified-live" &&
        persisted.AttachmentId == session.AttachmentId && persisted.Episode == session.Episode && persisted.AcknowledgedEpisode != persisted.Episode &&
        persisted.Waits.Any(wait => (wait.Episode == persisted.Episode || wait.Episode is null && wait.Since == persisted.EpisodeStarted) && wait.Reason == session.Reason &&
            (wait.Reason != AttentionReason.Permission || wait.Confirmation == HumanConfirmation.Confirmed || session.NativePermissionPending == true));

    /// <param name="facts">Read-only observations for shared conversations, gathered by the caller before the locks are taken; none until the binder and runtime transport exist.</param>
    public AppState Drain(ConversationEvidenceSnapshot? facts = null)
    {
        using var gate = new DataLock(paths, "state");
        var state = snapshots.Read();
        // A backlog is applied over several drains; compaction waits until it is consumed so a late resume is not dropped, unless the save would exceed the budget.
        var backlog = HasBacklog();
        var accepted = new CaptureConsumer(paths).Prepare(state);
        accepted.AddRange(ReplayInbox(state));
        var changed = ReconcileAttachments(state, facts ?? ConversationEvidenceSnapshot.Empty);
        // Retention runs before the gate so a quiet state is also bounded; accepted captures were applied first.
        var retained = !backlog && CompactSettledSessions(state, time.GetUtcNow());
        retained |= PruneEndedSessions(state, time.GetUtcNow());
        if (accepted.Count != 0 || changed || retained || !File.Exists(paths.State))
        {
            if (state.Sessions.Count > 0)
            {
                state.AttachmentHealthVersion = 1;
            }
            RetainPendingReceipts(state);
            // A backlog skips compaction above, but a save over the budget compacts settled sessions before it removes any.
            snapshots.Save(state, backlog ? s => CompactSettledSessions(s, time.GetUtcNow()) : null);
        }
        // Commit before deletion so accepted observations survive a crash and replay idempotently.
        foreach (var path in accepted)
        {
            File.Delete(path);
        }

        return state;
    }

    public void Acknowledge(string key)
    {
        using var gate = new DataLock(paths, "state");
        var state = snapshots.Read();
        if (state.Sessions.TryGetValue(key, out var session))
        {
            session.AcknowledgedEpisode = session.Episode;
        }

        snapshots.Save(state);
    }

    public void MarkViewed(string key, string episode)
    {
        using var gate = new DataLock(paths, "state");
        var state = snapshots.Read();
        if (!state.Sessions.TryGetValue(key, out var session) || session.Episode != episode)
        {
            return;
        }
        session.ViewedEpisode = episode;
        snapshots.Save(state);
    }

    /// <summary>Whether this drain cannot take every pending capture or inbox file, so later drains still apply older events.</summary>
    private bool HasBacklog()
    {
        var captures = Path.Combine(paths.Root, CaptureProtocol.InboxName);
        return Directory.Exists(captures) && Directory.EnumerateFiles(captures, "*.json").Take(CaptureConsumer.BatchSize + 1).Count() > CaptureConsumer.BatchSize ||
            Directory.Exists(paths.Inbox) && Directory.EnumerateFiles(paths.Inbox, "*.json").Take(InboxBatchSize + 1).Count() > InboxBatchSize;
    }

    private List<string> ReplayInbox(AppState state)
    {
        var accepted = new List<string>();
        foreach (var path in Directory.EnumerateFiles(paths.Inbox, "*.json").Order(StringComparer.Ordinal).Take(InboxBatchSize))
        {
            try
            {
                var observation = AtomicFile.Read<Observation>(path) ?? throw new InvalidDataException();
                EventStore.Validate(observation);
                if (!Path.GetFileNameWithoutExtension(path).EndsWith("-" + observation.Id, StringComparison.Ordinal))
                {
                    throw new InvalidDataException("Invalid event filename.");
                }

                SessionReducer.Apply(state, observation);
                accepted.Add(path);
            }
            catch (Exception exception) when (exception is IOException or JsonException or InvalidDataException or ArgumentException)
            {
                new LocalLog(paths).Write("event-rejected");
                // Never retain raw invalid input; only normalized records enter this directory.
                try
                {
                    File.Delete(path);
                }
                catch (IOException) { }
            }
        }
        return accepted;
    }

    private bool ReconcileAttachments(AppState state, ConversationEvidenceSnapshot facts)
    {
        var changed = false;
        var now = time.GetUtcNow();
        var grace = TimeSpan.FromMinutes(new ConfigStore(paths).Read().AttachmentEvidenceGraceMinutes);
        foreach (var session in state.Sessions.Values)
        {
            if (session.Ended)
            {
                if (session.AttachmentHealth != "confirmed-ended")
                {
                    session.AttachmentHealth = "confirmed-ended";
                    changed = true;
                }
                continue;
            }
            using var presentation = new DataLock(paths, "presentation");
            var attachment = attachments.Get(session.AttachmentId);
            var evaluated = attachment is null ? null
                : new AttachmentHealth(inspect).Evaluate(attachment, now, grace, facts.Find(session.Key));
            if (evaluated is not null && evaluated != attachment)
            {
                attachments.Save(evaluated);
            }
            var since = evaluated?.UnverifiedSince ?? session.AttachmentUnverifiedSince ?? now;
            var health = evaluated?.Health ?? (now - since >= grace ? "unavailable" : "unverified");
            var effectiveSince = health == "verified-live" ? (DateTimeOffset?)null : since;
            if (session.AttachmentHealth != health || session.AttachmentUnverifiedSince != effectiveSince)
            {
                session.AttachmentHealth = health;
                session.AttachmentUnverifiedSince = effectiveSince;
                changed = true;
            }
            if (health != "confirmed-ended")
            {
                continue;
            }

            session.Ended = true;
            session.Waits.Clear();
            session.Background.Clear();
            session.Episode = null;
            changed = true;
        }
        return changed;
    }

    private void RetainPendingReceipts(AppState state)
    {
        // Include committed batches awaiting deletion after a crash.
        var captures = Path.Combine(paths.Root, CaptureProtocol.InboxName);
        foreach (var receipt in state.CaptureReceipts.ToArray())
        {
            if (Path.GetFileName(receipt.Value) != receipt.Value || !File.Exists(Path.Combine(captures, receipt.Value)))
            {
                state.CaptureReceipts.Remove(receipt.Key);
            }
        }
        var inboxIds = Directory.EnumerateFiles(paths.Inbox, "*.json")
            .Select(path => Path.GetFileNameWithoutExtension(path) ?? "")
            .Select(name => name[(name.LastIndexOf('-') + 1)..]).ToHashSet(StringComparer.Ordinal);
        inboxIds.UnionWith(state.CaptureReceipts.Keys);
        state.AppliedEvents.IntersectWith(inboxIds);
    }

    private static bool CompactSettledSessions(AppState state, DateTimeOffset now)
    {
        var changed = false;
        foreach (var session in state.Sessions.Values)
        {
            if (session.Ended && now - session.LastObserved > SettleInterval)
            {
                changed |= CausalSessionReducer.Compact(session);
            }
        }
        return changed;
    }

    private static bool PruneEndedSessions(AppState state, DateTimeOffset now)
    {
        var cutoff = now - EndedRetention;
        var remove = state.Sessions.Where(pair => pair.Value.Ended && pair.Value.LastObserved < cutoff).Select(pair => pair.Key).ToHashSet(StringComparer.Ordinal);
        var kept = state.Sessions.Where(pair => pair.Value.Ended && !remove.Contains(pair.Key))
            .OrderBy(pair => pair.Value.LastObserved).ThenBy(pair => pair.Key, StringComparer.Ordinal).ToArray();
        remove.UnionWith(kept.Take(Math.Max(0, kept.Length - MaxEndedSessions)).Select(pair => pair.Key));
        foreach (var key in remove)
        {
            state.Sessions.Remove(key);
        }
        return remove.Count != 0;
    }
}
