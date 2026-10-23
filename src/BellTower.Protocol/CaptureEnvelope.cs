using System.Text.Json;
using System.Text.Json.Serialization;
using BellTower.Core;
using BellTower.Providers;

namespace BellTower.Protocol;

public sealed record CaptureEnvelope
{
    public int Protocol { get; init; } = 1;
    public required string Id { get; init; }
    public required DateTimeOffset IngressAt { get; init; }
    public required Observation Observation { get; init; }
    public required Attachment Attachment { get; init; }
    public ProcessIdentity? HookShell { get; init; }
    public List<ProcessEvidence> Ancestors { get; init; } = [];
    public string? TranscriptPath { get; init; }
    public bool Dedicated { get; init; }
    public string? Name { get; init; }
    public string? Change { get; init; }
    public string? Role { get; init; }
    public OpenSpecSelection? Selection { get; init; }
    public string? SelectionRoot { get; init; }
    public bool CreatedScaffold { get; init; }
    public bool SelectionPre { get; init; }
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? AdditionalData { get; set; }
}
