using BellTower.Core;

namespace BellTower.Hosts;

public sealed record DesiredTerminalPresentation(string AttachmentId, string Provider, DateTimeOffset RegisteredAt,
    ProcessIdentity Agent, ProcessIdentity Shell, string Title, string? Background);
