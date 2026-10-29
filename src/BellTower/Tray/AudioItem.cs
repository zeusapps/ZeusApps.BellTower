using BellTower.Core;

namespace BellTower.Tray;

public sealed record AudioItem(
    string Key,
    string Episode,
    string Phrase,
    bool Test = false,
    AttentionReason Reason = AttentionReason.Done,
    AudioNotificationKind Kind = AudioNotificationKind.Alert,
    int ReminderOrdinal = 0);
