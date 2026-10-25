using BellTower.Core;

namespace BellTower.Hosts;

/// <summary>
/// An editor window observed by its own extension instance: the top-level window that was the
/// foreground window while that instance reported its window as focused.
/// </summary>
public sealed record WindowEvidence(long Handle, ProcessIdentity Owner, string ClassName, DateTimeOffset ObservedAt);
