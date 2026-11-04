using System.Text.Json;
using System.Text.Json.Serialization;

namespace BellTower.Core;

/// <summary>Verified classification of the agent process that hosts a hook. Persisted as text so unknown future values survive.</summary>
public static class AgentModes
{
    public const string Unknown = "unknown";
    public const string InteractiveClient = "interactive-client";
    public const string EmbeddedRuntime = "embedded-runtime";
    public const string SharedBackend = "shared-backend";
    public const string Headless = "headless";
}

/// <summary>Who ends the conversation's work. Independent of whether a terminal is known.</summary>
public static class LifecycleOwners
{
    public const string Unknown = "unknown";
    public const string BellTower = "bell-tower";
    public const string AgentProcess = "agent-process";
    public const string SharedBackend = "shared-backend";
}

public static class EvidenceStatus
{
    public const string Unverified = "unverified";
    public const string HookObserved = "hook-observed";
    public const string None = "none";
}

/// <summary>How Bell Tower can reach the provider runtime for status. Access never implies lifecycle ownership.</summary>
public sealed record RuntimeEvidence
{
    public string Access { get; init; } = EvidenceStatus.None;
    public string? Source { get; init; }
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? AdditionalData { get; set; }
}

/// <summary>
/// Whether a terminal destination has been verified for this conversation. Unverified by default. Client, shell, ordering
/// key and observation time are reserved for the shared-Codex endpoint binder (task 4.2); nothing writes them yet.
/// </summary>
public sealed record TerminalEvidence
{
    public string Binding { get; init; } = EvidenceStatus.Unverified;
    public string? Source { get; init; }
    public ProcessIdentity? Client { get; init; }
    public ProcessIdentity? Shell { get; init; }
    public string? OrderingKey { get; init; }
    public DateTimeOffset? ObservedAt { get; init; }
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? AdditionalData { get; set; }
}

/// <summary>
/// The direct attachment a provisional capture would become. It travels inside evidence so the wire shape of the capture
/// stays fail closed (shared runtime) for every consumer; only a consumer that verifies the launch record may promote it.
/// </summary>
public sealed record DirectCandidate
{
    public required string Id { get; init; }
    public required ProcessIdentity Process { get; init; }
    public HostTarget Host { get; init; } = new();
    public bool ManagedTitle { get; init; }
    public bool ManagedBackground { get; init; }
    public string? Name { get; init; }
    public string? Change { get; init; }
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? AdditionalData { get; set; }
}

/// <summary>
/// Independent facets of an attachment: conversation health, runtime access, terminal binding and lifecycle ownership.
/// The legacy Health and Ownership fields still describe the process attachment; this record is purely additive and
/// older readers retain it as extension data. It is stamped with the process generation it describes and a schema
/// revision; a mismatched stamp is treated as absent and the conservative derived view is used instead.
/// </summary>
public sealed record AttachmentEvidence
{
    public const int CurrentRevision = 1;

    public int Revision { get; init; } = CurrentRevision;
    public ProcessIdentity? Generation { get; init; }
    public string Mode { get; init; } = AgentModes.Unknown;
    // Set by a capture that needs the launch record to be verified before it may stay direct; cleared by NativeAdoption.Reconcile.
    public bool Provisional { get; init; }
    // Wire form only: the direct candidate of a provisional capture. Never persisted after adoption.
    public DirectCandidate? Candidate { get; init; }
    // The hooked agent's direct parent (creation time included), used to prove a profile launch from its recorded shell.
    public ProcessIdentity? AgentParent { get; init; }
    public string ConversationHealth { get; init; } = EvidenceStatus.Unverified;
    public DateTimeOffset? ConversationObservedAt { get; init; }
    public RuntimeEvidence? Runtime { get; init; } = new();
    public TerminalEvidence? Terminal { get; init; } = new();
    public string LifecycleOwner { get; init; } = LifecycleOwners.Unknown;
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? AdditionalData { get; set; }

    [JsonIgnore]
    public RuntimeEvidence RuntimeAccess => Runtime ?? new();
    [JsonIgnore]
    public TerminalEvidence TerminalBinding => Terminal ?? new();

    public static AttachmentEvidence ForManagedLaunch(ProcessIdentity? generation = null) =>
        new() { LifecycleOwner = LifecycleOwners.BellTower, Generation = generation };

    /// <summary>The single derivation of the lifecycle owner from ownership and process evidence.</summary>
    public static string LifecycleFor(string ownership, bool hasProcess) => ownership switch
    {
        "managed" => LifecycleOwners.BellTower,
        "shared-runtime" => LifecycleOwners.SharedBackend,
        "observed" when hasProcess => LifecycleOwners.AgentProcess,
        _ => LifecycleOwners.Unknown
    };

    /// <summary>The evidence an attachment carries, deriving a conservative view for records without usable evidence.</summary>
    public static AttachmentEvidence Of(Attachment attachment) =>
        attachment.Evidence is { Revision: CurrentRevision } stored && stored.Generation == attachment.Process ? stored : new AttachmentEvidence
        {
            Generation = attachment.Process,
            LifecycleOwner = LifecycleFor(attachment.Ownership, attachment.Process is not null)
        };

    public AttachmentEvidence Stamped(ProcessIdentity? generation) => this with { Revision = CurrentRevision, Generation = generation };

    /// <summary>Records a hook as conversation evidence; a later event may recover a missed registration without changing other facets.</summary>
    public static AttachmentEvidence Observe(AttachmentEvidence evidence, DateTimeOffset at) => evidence with
    {
        ConversationHealth = EvidenceStatus.HookObserved,
        ConversationObservedAt = evidence.ConversationObservedAt is { } known && known > at ? known : at
    };

    /// <summary>
    /// Merges a new capture into the stored evidence. An established mode, lifecycle owner, terminal binding and runtime
    /// access are never replaced by a later hook (mode and owner may be learned from unknown); only an explicit replacement
    /// generation adopts the new capture's facets, keeping unknown stored fields.
    /// </summary>
    public static AttachmentEvidence Merge(Attachment previous, Attachment captured, bool replacement, DateTimeOffset at)
    {
        var stored = Of(previous);
        var incoming = Of(captured);
        if (replacement)
        {
            return Observe(incoming with
            {
                ConversationObservedAt = stored.ConversationObservedAt,
                Runtime = incoming.RuntimeAccess with { AdditionalData = Retain(stored.RuntimeAccess.AdditionalData, incoming.RuntimeAccess.AdditionalData) },
                Terminal = incoming.TerminalBinding with { AdditionalData = Retain(stored.TerminalBinding.AdditionalData, incoming.TerminalBinding.AdditionalData) },
                AdditionalData = Retain(stored.AdditionalData, incoming.AdditionalData)
            }, at);
        }
        return Observe(stored with
        {
            Mode = stored.Mode == AgentModes.Unknown ? incoming.Mode : stored.Mode,
            LifecycleOwner = stored.LifecycleOwner == LifecycleOwners.Unknown ? incoming.LifecycleOwner : stored.LifecycleOwner,
            Provisional = false
        }, at);
    }

    private static Dictionary<string, JsonElement>? Retain(Dictionary<string, JsonElement>? stored, Dictionary<string, JsonElement>? incoming)
    {
        if (stored is null)
        {
            return incoming;
        }
        var result = new Dictionary<string, JsonElement>(stored);
        foreach (var field in incoming ?? [])
        {
            result[field.Key] = field.Value;
        }
        return result;
    }
}
