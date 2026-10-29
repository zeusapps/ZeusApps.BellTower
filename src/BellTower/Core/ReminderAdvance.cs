namespace BellTower.Core;

public sealed record ReminderAdvance(IReadOnlyList<ScheduledReminder> Scheduled, bool Changed);
