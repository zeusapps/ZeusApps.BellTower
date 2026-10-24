using BellTower.Core;
using BellTower.IO;
using BellTower.Protocol;

namespace BellTower.Providers;

public sealed class CaptureConsumer(DataPaths paths)
{
    public const int BatchSize = 128;

    public List<string> Prepare(AppState state)
    {
        var accepted = new List<string>();
        var inbox = Path.Combine(paths.Root, CaptureProtocol.InboxName);
        if (!Directory.Exists(inbox))
        {
            return accepted;
        }
        foreach (var path in Directory.EnumerateFiles(inbox, "*.json").Take(BatchSize))
        {
            CaptureEnvelope envelope;
            try
            {
                using var stream = File.OpenRead(path);
                envelope = CaptureProtocol.Decode(stream);
                if (!Path.GetFileNameWithoutExtension(path).EndsWith("-" + envelope.Id, StringComparison.Ordinal))
                {
                    throw new InvalidDataException("capture-filename");
                }
            }
            catch (Exception exception) when (exception is System.Text.Json.JsonException or InvalidDataException or ArgumentException)
            {
                CaptureSpool.RecordRejection(paths.Root, exception);
                File.Delete(path);
                continue;
            }
            // IO/access failures leave valid captures for retry. Effects below are idempotent by identity.
            state.CaptureReceipts[envelope.Id] = Path.GetFileName(path);
            if (!state.AppliedEvents.Contains(envelope.Id))
            {
                var observation = envelope.Observation with { ProcessedAt = DateTimeOffset.UtcNow };
                var attachments = new AttachmentStore(paths);
                var attachment = attachments.Adopt(envelope.Attachment with { Ownership = envelope.Dedicated ? envelope.Attachment.Ownership : "shared-runtime" },
                    observation.At, observation.Kind == EventKind.Register && observation.RegistrationSource == "resume");
                var launches = new LaunchPresentationStore(paths);
                var palette = envelope.Dedicated ? launches.Consume(attachment.Id, attachment.Host.Shell, envelope.IngressAt, requireLive: false) : null;
                new IdentityStore(paths).Get(observation.Key, observation.Directory,
                    IdentityText.Optional(envelope.Name), IdentityText.Optional(envelope.Change), palette);
                var transcript = envelope.TranscriptPath is { } candidate && Path.IsPathFullyQualified(candidate) ? candidate : null;
                new IdentityStore(paths).Update(observation.Key, observation.Directory, value =>
                {
                    var bound = transcript is not null && HistoryState.Bind(value, transcript);
                    return CheckoutEvidence.Observe(value, observation.Directory, observation.IsMainThread) | bound;
                });
                new OpenSpecSelectionObserver(paths).Observe(envelope);
                SessionReducer.Apply(state, observation);
                AtomicFile.Write(Path.Combine(paths.Root, observation.Provider + "-observed.json"), IO.Json.Encode(new
                {
                    At = envelope.IngressAt,
                    ProcessedAt = observation.ProcessedAt,
                    Hook = observation.Kind.ToString()
                }));
            }
            accepted.Add(path);
        }
        return accepted;
    }
}
