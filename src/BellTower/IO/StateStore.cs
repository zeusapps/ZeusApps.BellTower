using System.Text.Json;
using BellTower.Core;

namespace BellTower.IO;

public sealed class StateStore
{
    private readonly DataPaths paths;
    private readonly StateSnapshotStore snapshots;
    private readonly AttachmentStore attachments;

    public StateStore(DataPaths paths)
    {
        this.paths = paths;
        snapshots = new StateSnapshotStore(paths);
        attachments = new AttachmentStore(paths);
    }

    public AppState Read() => snapshots.Read();

    public AppState Drain()
    {
        using var gate = new DataLock(paths, "state");
        var state = snapshots.Read();
        var accepted = ReplayInbox(state);
        var changed = ReconcileAttachments(state);
        if (accepted.Count != 0 || changed || !File.Exists(paths.State))
        {
            RetainPendingReceipts(state);
            PruneEndedSessions(state);
            snapshots.Save(state);
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

    private List<string> ReplayInbox(AppState state)
    {
        var accepted = new List<string>();
        foreach (var path in Directory.EnumerateFiles(paths.Inbox, "*.json").Order(StringComparer.Ordinal).Take(2000))
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

    private bool ReconcileAttachments(AppState state)
    {
        var changed = false;
        foreach (var session in state.Sessions.Values.Where(session => !session.Ended))
        {
            var attachment = attachments.Get(session.AttachmentId);
            if (attachment?.EndedAt is null && !(attachment?.Process is { } process && !ProcessProbe.IsAlive(process)))
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
        var inboxIds = Directory.EnumerateFiles(paths.Inbox, "*.json")
            .Select(path => Path.GetFileNameWithoutExtension(path) ?? "")
            .Select(name => name[(name.LastIndexOf('-') + 1)..]).ToHashSet(StringComparer.Ordinal);
        state.AppliedEvents.IntersectWith(inboxIds);
    }

    private static void PruneEndedSessions(AppState state)
    {
        var cutoff = DateTimeOffset.UtcNow.AddDays(-30);
        foreach (var key in state.Sessions.Where(pair => pair.Value.Ended && pair.Value.LastObserved < cutoff).Select(pair => pair.Key).ToArray())
        {
            state.Sessions.Remove(key);
        }
    }
}
