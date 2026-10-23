namespace BellTower.Core;

public sealed record SelectionEvidence(string Id, string Operation, DateTimeOffset At, bool Pre,
    string Change, string? Root, string? Store);
