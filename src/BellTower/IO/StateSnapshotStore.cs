using System.Text.Json;
using BellTower.Core;

namespace BellTower.IO;

public sealed class StateSnapshotStore(DataPaths paths)
{
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

    public void Save(AppState state)
    {
        var encoded = Json.Encode(state);
        if (System.Text.Encoding.UTF8.GetByteCount(encoded) > 16 * 1024 * 1024)
        {
            throw new InvalidDataException("State exceeds the supported snapshot budget; pending captures were retained.");
        }
        AtomicFile.Write(paths.State, encoded, backup: true);
    }

    private static bool IsValid(AppState? state) => state is not null && state.SchemaVersion == 1 && state.AttachmentHealthVersion is 0 or 1 &&
        state.Sessions is not null && state.AppliedEvents is not null && state.CaptureReceipts is not null &&
        state.CaptureReceipts.All(receipt => receipt.Value is not null && receipt.Value.Length <= 128) && state.Sessions.Values.All(session =>
            session is not null && session.Waits is not null && session.Background is not null &&
            session.CausalEvents is not null && session.CausalEvents.Count <= CausalSessionReducer.Window &&
            session.Operations is not null && session.CompletedOperations is not null &&
            session.Waits.All(wait => wait is not null) && session.Background.All(task => task is not null));
}
