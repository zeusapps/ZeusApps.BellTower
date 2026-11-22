using System.Security.Cryptography;

namespace BellTower.Core;

public sealed record Palette(string Background, string Accent, string Foreground)
{
    // Ordered series of twelve OKLCH hues, 30 degrees apart starting at 25 degrees. Accent: lightness .56, chroma .11. Background:
    // chroma .05, lightness .22 on even hue steps and .27 on odd ones, so neighbouring hues stay visibly different. The foreground is
    // black or white, whichever contrasts more with the accent. The hex values are the generated table (gamut-clipped, rounded);
    // tests re-derive them. The order takes hue steps 0, 6, 3, 9, 1, 7, 4, 10, 2, 8, 5, 11, so consecutive entries are at least
    // 90 degrees apart and later entries fall between earlier ones.
    public static readonly IReadOnlyList<Palette> Series =
    [
        new("#2e100e", "#ac5853", "#ffffff"),
        new("#002126", "#008693", "#000000"),
        new("#262909", "#737c24", "#000000"),
        new("#28213c", "#7966ae", "#ffffff"),
        new("#391f0c", "#a5612e", "#ffffff"),
        new("#072a3b", "#1c7eab", "#000000"),
        new("#09200b", "#47854a", "#000000"),
        new("#261227", "#955c98", "#ffffff"),
        new("#251800", "#926e10", "#ffffff"),
        new("#0f1932", "#5472b5", "#ffffff"),
        new("#012e25", "#008971", "#000000"),
        new("#391b28", "#a65778", "#ffffff")
    ];

    // Accents closer than this OKLab distance count as the same color. Adjacent series hues are about .057 apart.
    public const double SameColorDistance = .04;

    // A random series color for callers that only need some readable palette. Sessions are allocated with Allocate.
    public static Palette Create() => Series[RandomNumberGenerator.GetInt32(Series.Count)];

    // The first series color that no in-use accent matches. When every series color is in use, the color with the fewest in-use accents
    // within SameColorDistance, then the largest minimum OKLab distance to the in-use accents, then the earliest in the series, so
    // repeats spread over the series (no color is shared twice before each is shared once). The caller passes one accent per live
    // session and holds the shared identity lock so concurrent creations observe each other. A color within SameColorDistance of
    // exclude (the session's own current color for reset and random) is never returned.
    public static Palette Allocate(IReadOnlyCollection<string> inUse, string? exclude = null)
    {
        var distances = Series.Select(entry => inUse.Select(accent => Distance(entry.Accent, accent)).ToArray()).ToArray();
        var allowed = Enumerable.Range(0, Series.Count).Where(index => exclude is null || Distance(Series[index].Accent, exclude) >= SameColorDistance).ToArray();
        var free = allowed.FirstOrDefault(index => distances[index].All(distance => distance >= SameColorDistance), -1);
        if (free >= 0)
        { return Series[free]; }
        var best = allowed[0];
        foreach (var index in allowed.Skip(1))
        {
            var (users, bestUsers) = (distances[index].Count(distance => distance < SameColorDistance), distances[best].Count(distance => distance < SameColorDistance));
            if (users < bestUsers || users == bestUsers && distances[index].Min() > distances[best].Min())
            { best = index; }
        }
        return Series[best];
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

    // Perceptual (OKLab Euclidean) distance between two #RRGGBB colors.
    public static double Distance(string first, string second)
    {
        var (l1, a1, b1) = OkLab(first);
        var (l2, a2, b2) = OkLab(second);
        return Math.Sqrt((l1 - l2) * (l1 - l2) + (a1 - a2) * (a1 - a2) + (b1 - b2) * (b1 - b2));
    }

    internal static (double L, double A, double B) OkLab(string color)
    {
        var rgb = Enumerable.Range(0, 3).Select(index => Linear(Convert.ToInt32(color.Substring(1 + index * 2, 2), 16) / 255d)).ToArray();
        var l = Math.Cbrt(.4122214708 * rgb[0] + .5363325363 * rgb[1] + .0514459929 * rgb[2]);
        var m = Math.Cbrt(.2119034982 * rgb[0] + .6806995451 * rgb[1] + .1073969566 * rgb[2]);
        var s = Math.Cbrt(.0883024619 * rgb[0] + .2817188376 * rgb[1] + .6299787005 * rgb[2]);
        return (.2104542553 * l + .7936177850 * m - .0040720468 * s, 1.9779984951 * l - 2.4285922050 * m + .4505937099 * s,
            .0259040371 * l + .7827717662 * m - .8086757660 * s);
    }

    // The sRGB hex of an OKLCH color (hue in degrees), clipped to the gamut. It derives and verifies the series table.
    internal static string FromOklch(double lightness, double chroma, double hue)
    {
        var a = chroma * Math.Cos(hue * Math.PI / 180);
        var b = chroma * Math.Sin(hue * Math.PI / 180);
        var l = Math.Pow(lightness + .3963377774 * a + .2158037573 * b, 3);
        var m = Math.Pow(lightness - .1055613458 * a - .0638541728 * b, 3);
        var s = Math.Pow(lightness - .0894841775 * a - 1.2914855480 * b, 3);
        var rgb = new[]
        {
            4.0767416621 * l - 3.3077115913 * m + .2309699292 * s,
            -1.2684380046 * l + 2.6097574011 * m - .3413193965 * s,
            -.0041960863 * l - .7034186147 * m + 1.7076147010 * s
        };
        return "#" + string.Concat(rgb.Select(value => ((int)Math.Round(Math.Clamp(Encode(value), 0, 1) * 255)).ToString("x2")));
    }

    public static double Contrast(string first, string second)
    {
        var x = RelativeLuminance(first);
        var y = RelativeLuminance(second);
        return (Math.Max(x, y) + .05) / (Math.Min(x, y) + .05);
    }

    private static double RelativeLuminance(string color)
    {
        var rgb = Enumerable.Range(0, 3)
            .Select(index => Linear(Convert.ToInt32(color.Substring(1 + 2 * index, 2), 16) / 255d)).ToArray();
        return .2126 * rgb[0] + .7152 * rgb[1] + .0722 * rgb[2];
    }

    private static double Linear(double channel) => channel <= .04045 ? channel / 12.92 : Math.Pow((channel + .055) / 1.055, 2.4);
    private static double Encode(double value) => value <= .0031308 ? 12.92 * value : 1.055 * Math.Pow(value, 1 / 2.4) - .055;
}
