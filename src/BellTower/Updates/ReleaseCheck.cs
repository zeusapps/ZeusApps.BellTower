namespace BellTower.Updates;

public sealed record ReleaseCheck(ReleaseOffer? Offer = null, string? Error = null, DateTimeOffset? RetryAfter = null);
