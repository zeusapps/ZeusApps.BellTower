using BellTower.Core;

namespace BellTower.Updates;

public sealed record TrayPresence(ProcessIdentity Process, string Version, string DataDirectory, string? Activation = null);
