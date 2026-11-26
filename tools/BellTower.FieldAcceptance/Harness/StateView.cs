using System.Text.Json.Nodes;

namespace BellTower.FieldAcceptance.Harness;

/// <summary>
/// One session's record in <c>state.json</c> as raw JSON, so the same reader serves the candidate and an installed 0.1.9 (no installed version's
/// types or policy decode it, ANALYSIS-162): the pending waits, the operations in flight, the completed operation memory and the journal of applied
/// events. Reads tolerate the tray replacing the file; a missing or unreadable file reads as no record.
/// </summary>
public sealed class StateView(JsonNode? session)
{
    public bool Exists => session is not null;
    public int Waits => session?["waits"]?.AsArray().Count ?? 0;
    public IReadOnlySet<string> Operations => session?["operations"] is JsonObject operations ? operations.Select(pair => pair.Key).ToHashSet(StringComparer.Ordinal) : new HashSet<string>();
    public IReadOnlySet<string> Completed => session?["completedOperations"] is JsonArray completed ? completed.Select(node => node?.GetValue<string>() ?? "").ToHashSet(StringComparer.Ordinal) : new HashSet<string>();
    public IReadOnlyList<string> Journal => session?["causalEvents"] is JsonArray events ? [.. events.Select(node => node?["id"]?.GetValue<string>() ?? "")] : [];
    /// <summary>The attachment identity carried by the most recent journal event (what the hook's ingress emitted), which the reducer compares with the session's own attachment.</summary>
    public string LastEventAttachmentId => session?["causalEvents"] is JsonArray { Count: > 0 } events ? events[^1]?["attachmentId"]?.GetValue<string>() ?? "" : "";
    /// <summary>The tool use ids of the journal's events: what reached the tray, whether or not the reducer applied it.</summary>
    public IReadOnlySet<string> JournalOperationIds => session?["causalEvents"] is JsonArray events ? events.Select(node => node?["operationId"]?.GetValue<string>() ?? "").Where(id => id.Length > 0).ToHashSet(StringComparer.Ordinal) : new HashSet<string>();
    public IReadOnlySet<string> JournalAttachmentIds => session?["causalEvents"] is JsonArray events ? events.Select(node => node?["attachmentId"]?.GetValue<string>() ?? "").ToHashSet(StringComparer.Ordinal) : new HashSet<string>();
    /// <summary>The attachment id carried by the journal event of a tool use id (what that hook's ingress emitted), or empty.</summary>
    public string AttachmentOfOperation(string operation) => session?["causalEvents"] is JsonArray events
        ? events.LastOrDefault(node => node?["operationId"]?.GetValue<string>() == operation)?["attachmentId"]?.GetValue<string>() ?? "" : "";
    public bool Ended => session?["ended"]?.GetValue<bool>() ?? false;
    public string AttachmentId => session?["attachmentId"]?.GetValue<string>() ?? "";

    /// <summary>The wait, operation and completion facts that must be equal when nothing happened (the stable-final-state comparison).</summary>
    public string Fingerprint => Waits + "|" + string.Join(",", Operations.Order(StringComparer.Ordinal)) + "|" + string.Join(",", Completed.Order(StringComparer.Ordinal)) + "|" + Journal.Count + "|" + Ended;

    public static StateView Read(Instance instance, string key)
    {
        for (var attempt = 0; attempt < 5; attempt++)
        {
            try
            {
                var state = JsonNode.Parse(File.ReadAllText(Path.Combine(instance.Layout.Data, "state.json")));
                return new(state?["sessions"]?[key]);
            }
            catch (Exception exception) when (exception is IOException or System.Text.Json.JsonException or UnauthorizedAccessException)
            {
                Thread.Sleep(200);
            }
        }
        return new(null);
    }

    /// <summary>The attachment record of a session as raw JSON (health, process identity and registration), or null.</summary>
    public static JsonNode? Attachment(Instance instance, string key)
    {
        var id = Read(instance, key).AttachmentId;
        if (id.Length == 0)
        {
            return null;
        }
        try
        {
            foreach (var file in Directory.EnumerateFiles(Path.Combine(instance.Layout.Data, "attachments"), "*.json"))
            {
                if (JsonNode.Parse(File.ReadAllText(file)) is { } node && node["id"]?.GetValue<string>() == id)
                {
                    return node;
                }
            }
        }
        catch (Exception exception) when (exception is IOException or System.Text.Json.JsonException or UnauthorizedAccessException)
        {
            // The tray replaces these files atomically; the next poll reads the new copy.
        }
        return null;
    }
}
