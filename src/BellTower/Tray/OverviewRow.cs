using BellTower.Core;

namespace BellTower.Tray;

public sealed record OverviewRow(string Key, SessionState State, SessionIdentity Identity, GitContext Git, string Navigation);
