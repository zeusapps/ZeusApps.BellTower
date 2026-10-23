using System.Text.Json;
using BellTower.Core;
using BellTower.Providers;

namespace BellTower.Protocol;

public static class CaptureFactory
{
    public static CaptureEnvelope? Create(string provider, JsonElement input, Attachment attachment,
        DateTimeOffset ingressAt, ProcessIdentity? hookShell = null, List<ProcessEvidence>? ancestors = null,
        bool dedicated = true, string? name = null, string? change = null, string? role = null)
    {
        var observation = HookAdapter.Parse(provider, input, attachment, ingressAt);
        if (observation is null)
        {
            return null;
        }
        var shellAt = hookShell is null ? (DateTimeOffset?)null : new DateTimeOffset(hookShell.StartedUtcTicks, TimeSpan.Zero);
        // A persistent agent/shell creation time is ownership, not event ordering.
        if (shellAt > ingressAt || shellAt < ingressAt.AddMinutes(-1))
        {
            shellAt = null;
        }
        observation = observation with
        {
            At = shellAt ?? ingressAt,
            IngressAt = ingressAt,
            HookShellStartedAt = shellAt,
            OrderConfidence = shellAt is null ? "ingress-only" : "hook-shell"
        };
        var selection = OpenSpecSelectionReader.Read(input);
        var root = selection is null ? null : OpenSpecSelectionReader.SuccessfulRoot(input, selection, checkScaffold: false);
        var created = selection is { Verb: "new", Store: null } && HookInput.Text(input, "hook_event_name") == "PostToolUse" &&
            input.TryGetProperty("tool_response", out var response) &&
            (response.ValueKind == JsonValueKind.String ? response.GetString() : HookInput.Text(response, "stdout")) is { } output &&
            output.Contains("Created change '" + selection.Change + "' at ", StringComparison.Ordinal) &&
            !(response.ValueKind == JsonValueKind.Object &&
                (response.TryGetProperty("exit_code", out var code) && (!code.TryGetInt32(out var exit) || exit != 0) ||
                 response.TryGetProperty("interrupted", out var interrupted) && interrupted.ValueKind == JsonValueKind.True));
        var envelope = new CaptureEnvelope
        {
            Id = observation.Id,
            IngressAt = ingressAt,
            Observation = observation,
            Attachment = attachment,
            HookShell = hookShell,
            Ancestors = ancestors ?? [],
            Dedicated = dedicated,
            Name = dedicated ? name : null,
            Change = dedicated ? change : null,
            Role = dedicated ? role : null,
            TranscriptPath = HookInput.Text(input, "transcript_path"),
            Selection = selection,
            SelectionPre = HookInput.Text(input, "hook_event_name") == "PreToolUse",
            SelectionRoot = root,
            CreatedScaffold = created
        };
        CaptureProtocol.Validate(envelope);
        return envelope;
    }
}
