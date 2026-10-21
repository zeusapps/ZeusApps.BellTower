using BellTower.Core;

namespace BellTower.Sessions;

public sealed record SessionSummary(string Key, SessionIdentity Identity, string Activity, AttentionReason? Reason, DateTimeOffset? Since, string Navigation);
