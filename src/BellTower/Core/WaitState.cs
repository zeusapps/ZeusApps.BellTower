using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace BellTower.Core;

// Human-wait confirmation shared by both providers. Provisional: an approval flow (or question dialog) started but nothing
// proves a person is needed. Confirmed: a supported provider signal (Claude permission_prompt notification, Codex owning-runtime
// status) established a human wait. Unverified: no supported evidence exists (legacy timing-only snapshots, absent signal).
// Resolved waits are removed and leave an ApprovalReceipt so late confirmations cannot resurrect them.
public enum HumanConfirmation
{
    Unverified,
    Provisional,
    Confirmed
}

public sealed record WaitState(AttentionReason Reason, DateTimeOffset Since, string? OperationId, string? Fingerprint, string[]? Candidates = null,
    string? Episode = null, bool Acknowledged = false, bool Viewed = false,
    HumanConfirmation? Confirmation = null, DateTimeOffset? ConfirmedAt = null, TimeSpan? ConfirmationCredit = null, bool Ambiguous = false)
{
    // Episode is the stable opaque wait identity. Candidate membership is part of the current evidence scope because a
    // partially resumed ambiguous wait still represents fewer unresolved requests. Legacy waits use an opaque fallback;
    // neither form exposes operation IDs, fingerprints, nor candidates in serialized runtime evidence.
    [JsonIgnore]
    public string ConfirmationScopeId
    {
        get
        {
            var identity = Episode ?? Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new
            {
                Reason,
                Since = Since.UtcTicks,
                OperationId,
                Fingerprint
            })));
            var candidates = Candidates?.Order(StringComparer.Ordinal).ToArray();
            var membership = Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(candidates)));
            return identity + ":" + membership;
        }
    }

    // Preserves fields written by a newer version so a round trip never drops them.
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? AdditionalData { get; init; }
}

// Causal receipt for a resolved question or approval wait; bounded and cleared at the next prompt, stop, end or replacement.
public sealed record ApprovalReceipt(DateTimeOffset At, string? OperationId, string? Fingerprint);
