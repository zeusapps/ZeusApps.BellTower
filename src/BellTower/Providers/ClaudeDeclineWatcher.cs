using System.Globalization;
using System.Text.Json;
using BellTower.Core;
using BellTower.IO;

namespace BellTower.Providers;

/// <summary>
/// Clears a Claude Code permission wait at the user's decline (design decision 12, task 4.9). Claude fires no hook for a manual decline,
/// so while a session has an outstanding wait the end of its transcript is read (bounded, in the tray, never in the hook) for the rejected
/// tool result. The decline is published as the same interrupt observation Codex's own Interrupt hook produces, so one shared reducer rule
/// clears the waits; nothing here times out a wait.
/// </summary>
public sealed class ClaudeDeclineWatcher(DataPaths paths, TimeProvider? time = null) : IDisposable
{
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(1);

    // What a check needs from a session, copied on the caller's thread: the background read never touches live session objects.
    private sealed record Snapshot(string Key, string SessionId, string AttachmentId, string Directory, string? TurnId, DateTimeOffset Since, string[] ToolUseIds);

    private readonly TimeProvider time = time ?? TimeProvider.System;
    private readonly HashSet<string> published = [];
    private readonly HashSet<string> logged = [];
    private int running;
    private DateTimeOffset next;
    private volatile bool disposed;

    public void Update(IReadOnlyList<SessionState> sessions)
    {
        var now = time.GetUtcNow();
        if (disposed || now < next || Interlocked.CompareExchange(ref running, 1, 0) != 0)
        {
            return;
        }
        next = now + Interval;
        var snapshots = Capture(sessions);
        _ = Task.Run(() =>
        {
            try
            {
                Check(snapshots);
            }
            finally
            {
                Interlocked.Exchange(ref running, 0);
            }
        });
    }

    /// <summary>Checks every Claude session with an outstanding wait once. Returns the number of declines published.</summary>
    public int Check(IReadOnlyList<SessionState> sessions) => Check(Capture(sessions));

    private static Snapshot[] Capture(IReadOnlyList<SessionState> sessions) => [.. sessions
        .Where(session => session.Provider == "claude" && !session.Ended && session.Waits.Any(wait => wait.Reason is AttentionReason.Permission or AttentionReason.Question))
        .Select(session =>
        {
            var waits = session.Waits.Where(wait => wait.Reason is AttentionReason.Permission or AttentionReason.Question).ToArray();
            return new Snapshot(session.Key, session.SessionId, session.AttachmentId, session.Directory, session.TurnId, waits.Min(wait => wait.Since),
                [.. waits.SelectMany(wait => (wait.Candidates ?? []).Append(wait.OperationId)).OfType<string>().Distinct(StringComparer.Ordinal)]);
        })];

    private int Check(Snapshot[] snapshots)
    {
        var count = 0;
        var known = 0;
        foreach (var snapshot in snapshots)
        {
            try
            {
                if (new IdentityStore(paths).Get(snapshot.Key, snapshot.Directory).TranscriptPath is not { } transcript)
                {
                    continue;
                }
                known++;
                if (ClaudeDeclineEvidence.DeclinedAt(TranscriptTail.Read(transcript), snapshot.ToolUseIds, snapshot.Since) is not { } declinedAt)
                {
                    continue;
                }
                // Keyed by the decline itself: the same rejection is published once, and a later rejection of a later prompt still can be.
                var key = snapshot.Key + "|" + declinedAt.UtcTicks.ToString(CultureInfo.InvariantCulture);
                if (published.Contains(key))
                {
                    continue;
                }
                new EventStore(paths).Publish(new Observation
                {
                    Id = ContentHash.Compute(key)[..32],
                    Provider = "claude",
                    SessionId = snapshot.SessionId,
                    AttachmentId = snapshot.AttachmentId,
                    Kind = EventKind.Interrupt,
                    At = declinedAt,
                    Directory = snapshot.Directory,
                    TurnId = snapshot.TurnId,
                    IsMainThread = true,
                    AdapterRevision = Observation.HumanWaitRevision
                });
                published.Add(key);
                count++;
                DeclineEvidenceRecord.Update(paths, known: null, published: 1, failure: null);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidDataException or JsonException or ArgumentException)
            {
                // Logged once per session, so a persistent failure is visible but does not repeat every second.
                if (logged.Add(snapshot.Key))
                {
                    new LocalLog(paths).Write("decline-evidence-unavailable");
                }
                DeclineEvidenceRecord.Update(paths, known: null, published: 0, failure: exception.GetType().Name);
            }
        }
        if (snapshots.Length != 0)
        {
            DeclineEvidenceRecord.Update(paths, known, published: 0, failure: null, keepFailure: true);
        }
        return count;
    }

    public void Dispose() => disposed = true;
}

/// <summary>
/// What the Claude decline adapter has done in this data directory, for doctor: transcripts it could name, declines it published and the
/// last read failure. Counts only; no path, identifier or text.
/// </summary>
public static class DeclineEvidenceRecord
{
    public sealed record State(int KnownTranscripts, int DeclinesPublished, string? LastFailure, DateTimeOffset? At);

    private static string PathFor(DataPaths paths) => System.IO.Path.Combine(paths.Root, "decline-evidence.json");

    public static State Read(DataPaths paths)
    {
        try
        {
            return AtomicFile.Read<State>(PathFor(paths)) ?? new(0, 0, null, null);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
        {
            return new(0, 0, null, null);
        }
    }

    public static void Update(DataPaths paths, int? known, int published, string? failure, bool keepFailure = false)
    {
        try
        {
            var current = Read(paths);
            var next = new State(known ?? current.KnownTranscripts, current.DeclinesPublished + published, keepFailure ? current.LastFailure : failure ?? current.LastFailure,
                DateTimeOffset.UtcNow);
            if (next with { At = null } != current with { At = null })
            {
                Directory.CreateDirectory(paths.Root);
                AtomicFile.Write(PathFor(paths), Json.Encode(next));
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // Diagnostics only.
        }
    }
}
