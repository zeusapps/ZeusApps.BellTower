using System.Text.Json;
using BellTower.Core;
using BellTower.Hosts;
using BellTower.IO;

namespace BellTower.Providers;

public sealed class HookIngestor(DataPaths paths, Func<string, string, string?, NativeAdoption.Outcome>? resolve = null)
{
    public void Ingest(string provider, TextReader reader)
    {
        using var document = JsonDocument.Parse(HookInput.ReadBounded(reader));
        var input = document.RootElement;
        var sessionId = HookInput.Text(input, "session_id") ?? throw new InvalidDataException("Session ID missing.");
        var outcome = (resolve ?? AttachmentCapture.Resolve)(provider, HookInput.Text(input, "cwd") ?? Environment.CurrentDirectory, sessionId);
        var promoted = NativeAdoption.Promote(outcome.Attachment);
        var captured = promoted?.Attachment ?? outcome.Attachment;
        var dedicated = outcome.Dedicated || promoted is not null;

        var attachments = new AttachmentStore(paths);
        var attachment = attachments.Adopt(captured, DateTimeOffset.UtcNow, HookInput.Text(input, "source") == "resume", sessionId);
        // A client re-identified from inherited launch metadata (or failed closed to the shared runtime) keeps none of the launch's identity.
        dedicated &= attachment.Id == captured.Id;
        var observation = HookAdapter.Parse(provider, input, attachment);
        if (observation is null)
        {
            return;
        }

        var initialPalette = dedicated ? new LaunchPresentationStore(paths).Consume(attachment.Id, attachment.Host.Shell) : null;
        new IdentityStore(paths).Get(observation.Key, observation.Directory,
            dedicated ? IdentityText.Optional(Environment.GetEnvironmentVariable("BELLTOWER_NAME")) : null,
            dedicated ? IdentityText.Optional(Environment.GetEnvironmentVariable("BELLTOWER_CHANGE")) : null, initialPalette);
        try
        {
            var transcript = HookInput.Text(input, "transcript_path") is { } path && Path.IsPathFullyQualified(path) ? path : null;
            new IdentityStore(paths).Update(observation.Key, observation.Directory, value =>
            {
                var bound = transcript is not null && HistoryState.Bind(value, transcript);
                return CheckoutEvidence.Observe(value, HookInput.Text(input, "cwd"), observation.IsMainThread) | bound;
            });
            new OpenSpecSelectionObserver(paths).Observe(observation.Key, observation.Directory, input);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or JsonException)
        {
            new LocalLog(paths).Write("selection-unavailable");
        }
        new EventStore(paths).Publish(observation);
        AtomicFile.Write(Path.Combine(paths.Root, provider + "-observed.json"), IO.Json.Encode(new
        {
            At = DateTimeOffset.UtcNow,
            Hook = observation.Kind.ToString()
        }));
    }
}
