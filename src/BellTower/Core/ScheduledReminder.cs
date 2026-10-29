namespace BellTower.Core;

public sealed record ScheduledReminder(string Key, string Episode, AttentionReason Reason, int Ordinal);
