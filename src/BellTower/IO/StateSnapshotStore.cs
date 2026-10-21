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

    public void Save(AppState state) => AtomicFile.Write(paths.State, Json.Encode(state), backup: true);

    private static bool IsValid(AppState? state) => state is not null && state.SchemaVersion == 1 &&
        state.Sessions is not null && state.AppliedEvents is not null && state.Sessions.Values.All(session =>
            session is not null && session.Waits is not null && session.Background is not null &&
            session.Operations is not null && session.CompletedOperations is not null &&
            session.Waits.All(wait => wait is not null) && session.Background.All(task => task is not null));
}
