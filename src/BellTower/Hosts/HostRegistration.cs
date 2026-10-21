using BellTower.Core;

namespace BellTower.Hosts;

public sealed record HostRegistration(string InstanceId, ProcessIdentity Process, DateTimeOffset UpdatedAt, List<int> TerminalPids);
