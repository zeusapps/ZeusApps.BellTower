namespace BellTower.Updates;

public sealed record UpdateOutcome(string Id, string State, string Version, string Message, DateTimeOffset At);
