using System.Text.Json;
using BellTower.Core;

namespace BellTower.IO;

public sealed class StateSnapshotStore(DataPaths paths, long budgetBytes = StateSnapshotStore.DefaultBudgetBytes, int fallbackPasses = StateSnapshotStore.FallbackPasses)
{
    public const long DefaultBudgetBytes = 16L * 1024 * 1024;

    /// <summary>Re-encodes after the first over-budget encode; the last pass removes every ended session.</summary>
    internal const int FallbackPasses = 4;

    public long Budget => budgetBytes;

    /// <summary>Whole-state encodes made by the latest <see cref="Save"/>; tests assert the fallback stays bounded.</summary>
    internal int LastSaveEncodes { get; private set; }

    public AppState Read()
    {
        foreach (var path in new[] { paths.State, paths.State + ".bak" })
        {
            try
            {
                var state = AtomicFile.Read<AppState>(path);
                if (IsValid(state))
                {
                    return state!;
                }
            }
            catch (Exception exception) when (exception is IOException or JsonException or InvalidDataException)
            {
                new LocalLog(paths).Write("state-invalid");
            }
        }
        return new();
    }

    /// <summary>
    /// Saves the state within the budget. When the encoding is over budget, <paramref name="relieve"/> (compaction of settled
    /// ended sessions) runs first; only if the state still does not fit are the oldest ended sessions removed from
    /// <paramref name="state"/> in a bounded number of re-encodes. The save is refused only when no ended session remains.
    /// </summary>
    /// <param name="relieve">Shrinks the state without removing a session; returns whether it changed anything.</param>
    public void Save(AppState state, Func<AppState, bool>? relieve = null)
    {
        var encoded = Json.Encode(state);
        LastSaveEncodes = 1;
        var size = System.Text.Encoding.UTF8.GetByteCount(encoded);
        if (size > budgetBytes && relieve is not null && relieve(state))
        {
            encoded = Json.Encode(state);
            LastSaveEncodes++;
            size = System.Text.Encoding.UTF8.GetByteCount(encoded);
        }
        if (size > budgetBytes)
        {
            var ended = state.Sessions.Values.Where(session => session.Ended)
                .OrderBy(session => session.LastObserved).ThenBy(session => session.Key, StringComparer.Ordinal).ToList();
            var weights = ended.Select(session => System.Text.Encoding.UTF8.GetByteCount(Json.Encode(session))).ToList();
            for (var pass = 1; size > budgetBytes && ended.Count > 0; pass++)
            {
                // Remove the oldest sessions that account for the excess; the last pass removes all that remain.
                var excess = size - budgetBytes;
                var count = 0;
                for (long removed = 0; count < ended.Count && (removed < excess || count == 0); count++)
                {
                    removed += weights[count];
                }
                if (pass >= fallbackPasses)
                {
                    count = ended.Count;
                }
                foreach (var session in ended.Take(count))
                {
                    state.Sessions.Remove(session.Key);
                }
                ended.RemoveRange(0, count);
                weights.RemoveRange(0, count);
                encoded = Json.Encode(state);
                LastSaveEncodes++;
                size = System.Text.Encoding.UTF8.GetByteCount(encoded);
            }
            if (size > budgetBytes)
            {
                throw new InvalidDataException("State exceeds the supported snapshot budget with live sessions alone; pending captures were retained.");
            }
        }
        AtomicFile.Write(paths.State, encoded, backup: true);
    }

    private static bool IsValid(AppState? state) => state is not null && state.SchemaVersion == 1 && state.AttachmentHealthVersion is 0 or 1 &&
        state.Sessions is not null && state.AppliedEvents is not null && state.CaptureReceipts is not null && state.ReminderReceipts is not null &&
        state.ReminderReceipts.Count <= 10000 && state.ReminderReceipts.All(pair => pair.Key is not null && pair.Value is not null && pair.Value.Key is not null &&
            pair.Value.Episode is not null && pair.Value.Ordinal is >= 0 and <= 10 && pair.Value.ReceiptId is { Length: <= 64 } &&
            (pair.Value.Escalation is null || pair.Value.Escalation.ReceiptId is { Length: <= 64 } && pair.Value.Escalation.ProtocolVersion == 1)) &&
        state.CaptureReceipts.All(receipt => receipt.Value is not null && receipt.Value.Length <= 128) && state.Sessions.Values.All(session =>
            session is not null && session.Waits is not null && session.Background is not null &&
            session.CausalEvents is not null && session.CausalEvents.Count <= CausalSessionReducer.Window &&
            session.Operations is not null && session.CompletedOperations is not null &&
            session.Waits.All(wait => wait is not null) && session.Background.All(task => task is not null));
}
