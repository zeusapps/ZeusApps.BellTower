namespace BellTower.Core;

public sealed record LaunchPresentation(string AttachmentId, ProcessIdentity Shell, Palette Palette, DateTimeOffset CreatedAt);
