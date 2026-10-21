namespace BellTower.Hosts;

public sealed record FocusRequest(string Id, string InstanceId, string AttachmentId, int ShellPid, string ShellStartedUtcTicks, DateTimeOffset ExpiresAt);
