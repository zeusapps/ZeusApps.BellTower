using System.Text.Json;
using System.Text.Json.Serialization;

namespace BellTower.Core;

public sealed class ReminderSettings
{
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? AdditionalData { get; set; }
    public bool Enabled { get; set; } = true;
    public int InitialDelayMinutes { get; set; } = 5;
    public int IdleThresholdMinutes { get; set; } = 5;
    public int RepeatIntervalMinutes { get; set; } = 10;
    public int MaxPerEpisode { get; set; } = 3;
    public int EscalationAfterReminder { get; set; } = 1;

    public void Validate()
    {
        if (InitialDelayMinutes is < 1 or > 1440 || IdleThresholdMinutes is < 1 or > 1440 ||
            RepeatIntervalMinutes is < 1 or > 1440 || MaxPerEpisode is < 1 or > 10 ||
            EscalationAfterReminder is < 1 || EscalationAfterReminder > MaxPerEpisode)
        {
            throw new ArgumentOutOfRangeException(nameof(ReminderSettings), "Reminder values are outside their supported bounds.");
        }
    }
}
