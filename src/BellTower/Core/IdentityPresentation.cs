namespace BellTower.Core;

public sealed record IdentityPresentation(string Key, DateTimeOffset At, string Title, string Subject,
    Palette Palette, string Checkout, GitContext Git, string Provenance, int Version = 1);
