using BellTower.Core;

namespace BellTower.Providers;

public static class StatusLine
{
    public static string Render(SessionIdentity identity, GitContext git, bool color = true)
    {
        var name = IdentityText.Clean(identity.Name);
        var context = IdentityText.Clean(git.ToString());
        if (string.IsNullOrEmpty(git.ToString()))
        {
            context = "";
        }

        if (!color)
        {
            return $"[ {name} ]" + (context.Length > 0 ? "  " + context : "");
        }

        static string Rgb(string hex) => string.Join(';', Enumerable.Range(0, 3).Select(i => Convert.ToInt32(hex.Substring(1 + i * 2, 2), 16)));
        return $"\u001b[48;2;{Rgb(identity.Palette.Accent)}m\u001b[38;2;{Rgb(identity.Palette.Foreground)}m {name} \u001b[0m" + (context.Length > 0 ? "  " + context : "");
    }
}
