using BellTower.IO;

namespace BellTower.Diagnostics;

/// <summary>Doctor's view of bounded monitoring state: file size against the budget, live and ended counts, and refresh health. Read-only; shared by both providers.</summary>
public sealed class StateReporter(DataPaths paths, TimeProvider? time = null)
{
    public static readonly TimeSpan PersistentAfter = TimeSpan.FromMinutes(1);

    public object Report(bool trayRunning)
    {
        var now = (time ?? TimeProvider.System).GetUtcNow();
        long? bytes = null;
        object? sessions = null;
        string? problem = null;
        try
        {
            if (File.Exists(paths.State))
            {
                bytes = new FileInfo(paths.State).Length;
                var state = new StateSnapshotStore(paths).Read();
                var ended = state.Sessions.Values.Count(session => session.Ended);
                sessions = new
                {
                    Live = state.Sessions.Count - ended,
                    Ended = ended,
                    ByProvider = state.Sessions.Values.GroupBy(session => session.Provider, StringComparer.Ordinal)
                    .OrderBy(group => group.Key, StringComparer.Ordinal).ToDictionary(group => group.Key, group => new { Live = group.Count(s => !s.Ended), Ended = group.Count(s => s.Ended) })
                };
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            problem = "state.json could not be read: " + exception.GetType().Name;
        }
        var record = RefreshHealth.Read(paths);
        var failing = record?.FailingSince is not null;
        var persistent = failing && trayRunning && now - record!.FailingSince > PersistentAfter;
        return new
        {
            StateFile = bytes is null ? (problem ?? "not present (no state has been written yet)") : (object)new
            {
                Bytes = bytes,
                BudgetBytes = StateSnapshotStore.DefaultBudgetBytes,
                PercentOfBudget = Math.Round(100.0 * bytes.Value / StateSnapshotStore.DefaultBudgetBytes, 1)
            },
            Sessions = sessions ?? "unavailable (no readable state file)",
            Retention = $"an ended session is compacted after {(int)StateStore.SettleInterval.TotalMinutes} minutes, at most {StateStore.MaxEndedSessions} ended sessions are kept (oldest removed first) with the {(int)StateStore.EndedRetention.TotalDays}-day limit, and a save over the budget removes the oldest ended sessions; live sessions are never removed",
            Refresh = record is null
                ? (object)"no refresh failure recorded (no refresh-health.json)"
                : new
                {
                    Failing = failing,
                    FailingSince = record.FailingSince?.ToString("O"),
                    Reason = record.Reason,
                    LastSuccess = record.LastSuccess?.ToString("O"),
                    Persistent = persistent,
                    Status = !failing ? "healthy: the last recorded failure was recovered"
                        : persistent ? "persistent failure: the tray is running and refresh has been failing for more than a minute; captures stay queued"
                        : trayRunning ? "failing for under a minute" : "a failure was recorded and the tray is not running"
                }
        };
    }
}
