namespace BellTower.Core;

public sealed record WaitState(AttentionReason Reason, DateTimeOffset Since, string? OperationId, string? Fingerprint, string[]? Candidates = null,
    string? Episode = null, bool Acknowledged = false, bool Viewed = false);
