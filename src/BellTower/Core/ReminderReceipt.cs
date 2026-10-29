using System.Text.Json;
using System.Text.Json.Serialization;

namespace BellTower.Core;

public sealed record ReminderReceipt(
    string Key,
    string Episode,
    DateTimeOffset FirstEligibleAt,
    DateTimeOffset NextReminderAt,
    DateTimeOffset? LastReminderAt,
    int Ordinal,
    string ReceiptId,
    EscalationEligibility? Escalation = null)
{
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? AdditionalData { get; set; }
}
