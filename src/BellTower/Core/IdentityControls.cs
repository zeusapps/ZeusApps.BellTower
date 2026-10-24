namespace BellTower.Core;

// Shared user controls over identity pins and colour (CLI and tray call the same rules).
public static class IdentityControls
{
    // A named or hex colour is honoured even when close to another session's colour; "random" (or null) clears
    // the pin and allocates a distinguishable palette.
    public static void ApplyColor(SessionIdentity identity, string? color, Func<Palette> allocate)
    {
        var random = string.IsNullOrWhiteSpace(color) || string.Equals(color, "random", StringComparison.OrdinalIgnoreCase);
        var palette = random ? allocate() : Palette.FromColor(color!.Trim());
        identity.ColorPin = random ? null : color!.Trim().ToLowerInvariant();
        identity.Palette = palette;
    }

    // Clears the Bell Tower pins (title, change, checkout, colour) and picks a distinguishable palette. Native
    // user titles, live work evidence and provider ownership are kept; the resolver re-derives the
    // automatic subject and checkout on its next pass. Attention state lives in another store and is untouched.
    public static void Reset(SessionIdentity identity, Func<Palette> allocate)
    {
        identity.ManualName = null;
        ChangeOrigin.Pin(identity, null);
        identity.CheckoutPin = null;
        identity.ColorPin = null;
        identity.Palette = allocate();
    }
}
