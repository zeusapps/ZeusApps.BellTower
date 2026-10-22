namespace BellTower.Updates;

public sealed record UpdateCache(DateTimeOffset? Attempted = null, DateTimeOffset? Validated = null, ReleaseOffer? Offer = null, DateTimeOffset? RetryAfter = null);
