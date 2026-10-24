using System.Security.Cryptography;

namespace BellTower.Core;

public sealed record Palette(string Background, string Accent, string Foreground)
{
    public static Palette Create()
    {
        var hue = RandomNumberGenerator.GetInt32(360);
        var dark = Hsl(hue, .40, .105);
        var accent = Hsl(hue, .60, .42);
        return new(dark, accent, Contrast(accent, "#ffffff") >= Contrast(accent, "#000000") ? "#ffffff" : "#000000");
    }

    // Picks the candidate whose accent is farthest from every accent already in use. The caller must hold the shared
    // identity lock so concurrent creations observe each other's choice.
    public static Palette Allocate(IReadOnlyCollection<string> taken, Func<Palette>? create = null, int attempts = 32)
    {
        create ??= Create;
        var candidates = Enumerable.Range(0, attempts).Select(_ => create()).ToArray();
        return taken.Count == 0 ? candidates[0] : candidates.MaxBy(candidate => taken.Min(accent => Distance(candidate.Accent, accent)))!;
    }

    public static bool IsHex(string value) => value.Length == 7 && value[0] == '#' && value.Skip(1).All(Uri.IsHexDigit);
    public static Palette FromColor(string value)
    {
        var named = value.ToLowerInvariant() switch
        {
            "red" => "#d84b4b",
            "orange" => "#d18a32",
            "yellow" => "#c2ad35",
            "green" => "#39a866",
            "blue" => "#438bd1",
            "purple" => "#9863d1",
            "pink" => "#cd5ca5",
            "cyan" => "#34aeb8",
            _ => value
        };
        if (!IsHex(named))
        { throw new ArgumentException("Use red, orange, yellow, green, blue, purple, pink, cyan or #RRGGBB."); }
        var dark = "#" + string.Concat(Enumerable.Range(0, 3).Select(i => (Convert.ToInt32(named.Substring(1 + i * 2, 2), 16) / 4).ToString("x2")));
        return new(dark, named.ToLowerInvariant(), Contrast(named, "#ffffff") >= Contrast(named, "#000000") ? "#ffffff" : "#000000");
    }

    public static double Distance(string first, string second) => Math.Sqrt(Enumerable.Range(0, 3).Sum(i =>
        Math.Pow(Convert.ToInt32(first.Substring(1 + i * 2, 2), 16) - Convert.ToInt32(second.Substring(1 + i * 2, 2), 16), 2)));
    public static double Contrast(string first, string second)
    {
        var x = RelativeLuminance(first);
        var y = RelativeLuminance(second);
        return (Math.Max(x, y) + .05) / (Math.Min(x, y) + .05);
    }

    private static double RelativeLuminance(string color)
    {
        var rgb = Enumerable.Range(0, 3)
            .Select(index => Convert.ToInt32(color.Substring(1 + 2 * index, 2), 16) / 255d)
            .Select(channel => channel <= .04045 ? channel / 12.92 : Math.Pow((channel + .055) / 1.055, 2.4)).ToArray();
        return .2126 * rgb[0] + .7152 * rgb[1] + .0722 * rgb[2];
    }

    private static string Hsl(double hue, double saturation, double lightness)
    {
        var c = (1 - Math.Abs(2 * lightness - 1)) * saturation;
        var x = c * (1 - Math.Abs(hue / 60 % 2 - 1));
        var m = lightness - c / 2;
        var (r, g, b) = hue switch
        {
            < 60 => (c, x, 0d),
            < 120 => (x, c, 0d),
            < 180 => (0d, c, x),
            < 240 => (0d, x, c),
            < 300 => (x, 0d, c),
            _ => (c, 0d, x)
        };
        return $"#{(int)Math.Round((r + m) * 255):x2}{(int)Math.Round((g + m) * 255):x2}{(int)Math.Round((b + m) * 255):x2}";
    }
}
