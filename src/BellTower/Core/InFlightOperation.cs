namespace BellTower.Core;

public sealed record InFlightOperation(DateTimeOffset StartedAt, string? Fingerprint);
