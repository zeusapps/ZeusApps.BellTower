using System.Text.Json;
using System.Text.Json.Serialization;

namespace BellTower.Core;

public sealed record EscalationEligibility(
    int ProtocolVersion,
    string ProviderSessionKey,
    string Episode,
    AttentionReason Reason,
    DateTimeOffset WaitStartedAt,
    int ReminderOrdinal,
    DateTimeOffset EligibleAt,
    string ReceiptId)
{
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? AdditionalData { get; set; }
}
