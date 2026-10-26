using System.Text.Json;

namespace BellTower.IO;

// Latest observation of a provider's human-wait confirmation signal (Claude permission_prompt notification, Codex
// owning-runtime approval status), kept so doctor can distinguish "configured" from "observed". It records no session
// content, only when the signal was last seen and in which permission modes.
public sealed record ConfirmationObservation(DateTimeOffset At, string Signal, List<string> Modes);

// Which observation shape the latest capture from a provider's hook ingress had. A legacy producer is a cached ingress image
// that predates the human-wait fields; its permission_prompt is re-derived, but doctor asks for a setup refresh.
public sealed record ProducerObservation(DateTimeOffset At, bool Legacy, DateTimeOffset? LastLegacy);

public static class ConfirmationEvidence
{
    public const string ClaudeSignal = "Notification permission_prompt";
    public const string CodexSignal = "owning-runtime waitingOnApproval status";
    private const int MaximumModes = 8;

    public static string Path(DataPaths paths, string provider) => System.IO.Path.Combine(paths.Root, provider + "-confirmation-observed.json");

    public static void Record(DataPaths paths, string provider, string signal, string? mode, DateTimeOffset at)
    {
        var path = Path(paths, provider);
        var modes = Read(paths, provider)?.Modes ?? [];
        if (!string.IsNullOrWhiteSpace(mode) && mode.Length <= 64 && !modes.Contains(mode) && modes.Count < MaximumModes)
        {
            modes.Add(mode);
        }
        AtomicFile.Write(path, Json.Encode(new ConfirmationObservation(at, signal, modes)));
    }

    public static string ProducerPath(DataPaths paths, string provider) => System.IO.Path.Combine(paths.Root, provider + "-ingress-revision.json");

    public static void RecordProducer(DataPaths paths, string provider, bool legacy, DateTimeOffset at)
    {
        var previous = ReadProducer(paths, provider);
        if (previous is not null && previous.Legacy == legacy && at - previous.At < TimeSpan.FromMinutes(1))
        {
            return;
        }
        AtomicFile.Write(ProducerPath(paths, provider), Json.Encode(new ProducerObservation(at, legacy, legacy ? at : previous?.LastLegacy)));
    }

    public static ProducerObservation? ReadProducer(DataPaths paths, string provider)
    {
        try
        {
            var path = ProducerPath(paths, provider);
            return File.Exists(path) && new FileInfo(path).Length < 4096 ? JsonSerializer.Deserialize<ProducerObservation>(File.ReadAllText(path), Json.Options) : null;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
        {
            return null;
        }
    }

    public static ConfirmationObservation? Read(DataPaths paths, string provider)
    {
        try
        {
            var path = Path(paths, provider);
            return File.Exists(path) && new FileInfo(path).Length < 4096 ? JsonSerializer.Deserialize<ConfirmationObservation>(File.ReadAllText(path), Json.Options) : null;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
        {
            return null;
        }
    }
}
