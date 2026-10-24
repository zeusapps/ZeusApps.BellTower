using System.Text.Json;
using BellTower.Core;
using BellTower.Hosts;
using BellTower.IO;

namespace BellTower.Providers;

public sealed class HookIngestor(DataPaths paths)
{
    public void Ingest(string provider, TextReader reader)
    {
        using var document = JsonDocument.Parse(HookInput.ReadBounded(reader));
        var input = document.RootElement;
        var sessionId = HookInput.Text(input, "session_id") ?? throw new InvalidDataException("Session ID missing.");
        var dedicated = provider != "codex" || Environment.GetEnvironmentVariable("BELLTOWER_DEDICATED") == "1";
        var captured = AttachmentCapture.Capture(provider, HookInput.Text(input, "cwd") ?? Environment.CurrentDirectory);
        if (!dedicated)
        {
            captured = captured with
            {
                Id = "codex-session-" + ContentHash.Compute(sessionId)[..24],
                Process = null,
                Host = new("unknown"),
                ManagedTitle = false,
                ManagedBackground = false
            };
        }

        var attachments = new AttachmentStore(paths);
        var attachment = attachments.Adopt(captured with { Ownership = dedicated ? captured.Ownership : "shared-runtime" }, DateTimeOffset.UtcNow,
            HookInput.Text(input, "source") == "resume");
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
