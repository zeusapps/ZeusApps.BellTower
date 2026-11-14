using BellTower.Core;

namespace BellTower.Hosts;

public static class TerminalColors
{
    public const string Foreground = "#f1f5f9";

    /// <summary>Resets the foreground (OSC 110) and background (OSC 111) to the terminal's configured colors, as a managed launch does on exit.</summary>
    public const string Reset = "\u001b]110\u001b\\" + "\u001b]111\u001b\\";

    public static string Apply(string background)
    {
        if (!Palette.IsHex(background))
        {
            throw new ArgumentException("Invalid terminal background.", nameof(background));
        }
        // ST terminates these OSC commands without a BEL or any printable terminal content.
        return "\u001b]11;" + background + "\u001b\\\u001b]10;" + Foreground + "\u001b\\";
    }
}
