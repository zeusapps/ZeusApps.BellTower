using BellTower.Core;
using BellTower.IO;
using BellTower.Protocol;

namespace BellTower.Providers;

public sealed class CaptureConsumer(DataPaths paths)
{
    public const int BatchSize = 128;

    // Diagnostics evidence is best effort and never affects ingestion.
    private void RecordEvidence(Observation observation, bool legacyProducer)
    {
        try
        {
            if (observation.HumanPrompt)
            {
                ConfirmationEvidence.Record(paths, observation.Provider, ConfirmationEvidence.ClaudeSignal, observation.PermissionMode, DateTimeOffset.UtcNow);
            }
            ConfirmationEvidence.RecordProducer(paths, observation.Provider, legacyProducer, DateTimeOffset.UtcNow);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
        }
    }

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
                // A producer older than the human-wait fields is re-derived (permission_prompt shape) and reported by doctor.
                var legacyProducer = envelope.Observation.AdapterRevision is null;
                var observation = SessionReducer.Normalize(envelope.Observation with { ProcessedAt = DateTimeOffset.UtcNow });
                var attachments = new AttachmentStore(paths);
                // A provisional capture arrives in the fail-closed shared shape; only this consumer may promote its candidate,
                // and Adopt keeps it only when Reconcile verifies the launch.
                var promoted = NativeAdoption.Promote(envelope.Attachment);
                var wire = promoted?.Attachment ?? envelope.Attachment;
                var direct = envelope.Dedicated || promoted is not null;
                var attachment = attachments.Adopt(wire with { Ownership = direct ? wire.Ownership : "shared-runtime" },
                    observation.At, observation.Kind == EventKind.Register && observation.RegistrationSource == "resume", observation.SessionId);
                // A client re-identified from inherited launch metadata (or failed closed to the shared runtime) keeps none
                // of the launch's palette, name or change.
                var inherited = attachment.Id != wire.Id;
                if (attachment.Id != observation.AttachmentId)
                {
                    // An inherited managed identifier that belongs to another live generation is replaced by this client's own.
                    observation = observation with { AttachmentId = attachment.Id };
                }
                var launches = new LaunchPresentationStore(paths);
                var palette = direct && !inherited ? launches.Consume(attachment.Id, attachment.Host.Shell, envelope.IngressAt, requireLive: false) : null;
                new IdentityStore(paths).Get(observation.Key, observation.Directory,
                    inherited ? null : IdentityText.Optional(promoted?.Name ?? envelope.Name), inherited ? null : IdentityText.Optional(promoted?.Change ?? envelope.Change), palette);
                var transcript = envelope.TranscriptPath is { } candidate && Path.IsPathFullyQualified(candidate) ? candidate : null;
                new IdentityStore(paths).Update(observation.Key, observation.Directory, value =>
                {
                    var bound = transcript is not null && HistoryState.Bind(value, transcript);
                    return CheckoutEvidence.Observe(value, observation.Directory, observation.IsMainThread) | bound;
                });
                new OpenSpecSelectionObserver(paths).Observe(envelope);
                SessionReducer.Apply(state, observation);
                RecordEvidence(observation, legacyProducer);
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
