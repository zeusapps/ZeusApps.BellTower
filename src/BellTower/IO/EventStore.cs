using BellTower.Core;

namespace BellTower.IO;

public sealed class EventStore(DataPaths paths)
{
    public void Publish(Observation observation)
    {
        Validate(observation);
        AtomicFile.Write(Path.Combine(paths.Inbox, $"{observation.At.UtcTicks:0000000000000000000}-{observation.Id}.json"), Json.Encode(observation));
    }

    public static void Validate(Observation observation)
    {
        if (observation.Provider is not ("claude" or "codex") ||
            string.IsNullOrWhiteSpace(observation.SessionId) || observation.SessionId.Length > 200 ||
            !Guid.TryParseExact(observation.Id, "N", out _) ||
            string.IsNullOrWhiteSpace(observation.AttachmentId) || observation.AttachmentId.Length > 200 ||
            observation.Directory is null || observation.Directory.Length > 32768 ||
            !Enum.IsDefined(observation.Kind) || observation.At > DateTimeOffset.UtcNow.AddMinutes(5) ||
            observation.BackgroundSnapshot is not null && observation.BackgroundSnapshot.Any(task =>
                task is null || string.IsNullOrWhiteSpace(task.Id) || task.Id.Length > 256 ||
                string.IsNullOrWhiteSpace(task.Type) || task.Type.Length > 100))
        {
            throw new InvalidDataException("Invalid event envelope.");
        }
    }
}
