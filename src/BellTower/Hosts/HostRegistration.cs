using BellTower.Core;

namespace BellTower.Hosts;

/// <summary>
/// A VS Code bridge registration. Protocol and Window are absent in registrations written by bridges
/// that predate window verification; those remain readable and are treated as partial.
/// </summary>
public sealed record HostRegistration(string InstanceId, ProcessIdentity Process, DateTimeOffset UpdatedAt, List<int> TerminalPids,
    int? Protocol = null, WindowEvidence? Window = null, string? WindowUnavailable = null);
