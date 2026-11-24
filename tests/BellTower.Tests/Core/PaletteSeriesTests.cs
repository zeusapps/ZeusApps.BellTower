using BellTower.Core;
using Xunit;

namespace BellTower.Tests.Core;

// The ordered OKLCH session color series and the OKLab distance (distinct-session-colors, design decisions 1 and 2).
public sealed class PaletteSeriesTests
{
    private static readonly int[] Order = [0, 6, 3, 9, 1, 7, 4, 10, 2, 8, 5, 11];

    private static double Hue(int step) => 25 + 30 * step;

    [Fact]
    public void SeriesHasTwelveEntriesInTheDesignedHueOrderDerivedFromOklch()
    {
        Assert.Equal(12, Palette.Series.Count);
        for (var index = 0; index < Order.Length; index++)
        {
            var entry = Palette.Series[index];
            Assert.Equal(Palette.FromOklch(Order[index] % 2 == 0 ? .22 : .27, .05, Hue(Order[index])), entry.Background);
            Assert.Equal(Palette.FromOklch(.56, .11, Hue(Order[index])), entry.Accent);
            Assert.Equal(Palette.Contrast(entry.Accent, "#ffffff") >= Palette.Contrast(entry.Accent, "#000000") ? "#ffffff" : "#000000", entry.Foreground);
        }
        Assert.Equal(12, Palette.Series.Select(entry => entry.Accent).Distinct().Count());
    }

    [Fact]
    public void EveryEntryHasADarkBackgroundARelatedBrighterAccentAndReadableText()
    {
        foreach (var entry in Palette.Series)
        {
            Assert.True(Palette.Contrast(entry.Accent, entry.Foreground) >= 4.5, entry.Accent);
            // The terminal text color Bell Tower applies over the background.
            Assert.True(Palette.Contrast(entry.Background, "#f1f5f9") >= 7, entry.Background);
            Assert.True(Palette.OkLab(entry.Background).L < Palette.OkLab(entry.Accent).L);
            // Related: the same hue within a few degrees (OKLab angle).
            var (_, ba, bb) = Palette.OkLab(entry.Background);
            var (_, aa, ab) = Palette.OkLab(entry.Accent);
            var difference = Math.Abs(Math.Atan2(bb, ba) - Math.Atan2(ab, aa)) * 180 / Math.PI;
            Assert.True(Math.Min(difference, 360 - difference) < 5, entry.Accent);
        }
    }

    [Fact]
    public void ConsecutiveEntriesAreFartherApartThanAnyPairOfAdjacentHues()
    {
        var adjacent = Enumerable.Range(0, 12).Select(step => Palette.Distance(
            Palette.FromOklch(.56, .11, Hue(step)), Palette.FromOklch(.56, .11, Hue(step + 1)))).Max();
        for (var index = 1; index < Palette.Series.Count; index++)
        {
            var distance = Palette.Distance(Palette.Series[index - 1].Accent, Palette.Series[index].Accent);
            Assert.True(distance > adjacent, index + ": " + distance + " <= " + adjacent);
        }
        // Every pair of entries stays apart by more than the same-color distance, so a free entry is always recognizable.
        Assert.All(Palette.Series.SelectMany((left, index) => Palette.Series.Skip(index + 1).Select(right => Palette.Distance(left.Accent, right.Accent))),
            distance => Assert.True(distance > Palette.SameColorDistance));
    }

    [Fact]
    public void TerminalBackgroundsOfTheFirstSixEntriesStayVisiblyDifferent()
    {
        // The background is what the terminal shows (T-11), so it needs its own separation, not only the accent.
        var backgrounds = Palette.Series.Take(6).Select(entry => entry.Background).ToArray();
        var minimum = backgrounds.SelectMany((left, index) => backgrounds.Skip(index + 1).Select(right => Palette.Distance(left, right))).Min();
        Assert.True(minimum >= .04, "minimum background distance " + minimum);
        // Hues 30 degrees apart (entries 0 and 4) are the closest pair in the first six.
        Assert.True(Palette.Distance(Palette.Series[0].Background, Palette.Series[4].Background) >= .04);
    }

    [Fact]
    public void DistanceIsOklabNotRgb()
    {
        Assert.Equal(0, Palette.Distance("#438bd1", "#438bd1"));
        Assert.Equal(1, Palette.Distance("#000000", "#ffffff"), 3);
        Assert.Equal(.5198, Palette.Distance("#ff0000", "#00ff00"), 3);
        Assert.Equal(Palette.Distance("#123456", "#abcdef"), Palette.Distance("#abcdef", "#123456"), 12);
    }

    [Fact]
    public void AllocationTakesTheFirstFreeEntryThenTheFarthestWithASeriesOrderTieBreak()
    {
        Assert.Equal(Palette.Series[0], Palette.Allocate([]));
        Assert.Equal(Palette.Series[1], Palette.Allocate([Palette.Series[0].Accent]));
        Assert.Equal(Palette.Series[2], Palette.Allocate([Palette.Series[1].Accent, Palette.Series[0].Accent]));
        var all = Palette.Series.Select(entry => entry.Accent).ToArray();
        var full = Palette.Allocate(all);
        Assert.Equal(full, Palette.Allocate(all.Reverse().ToArray()));
        // With every entry used once the distances are all zero, so the tie-break picks the first series entry.
        Assert.Equal(Palette.Series[0], full);
    }

    [Fact]
    public void RepeatsSpreadOverTheSeriesBeforeAnyColorIsSharedTwice()
    {
        var inUse = Palette.Series.Select(entry => entry.Accent).ToList();
        var allocated = new List<Palette>();
        for (var session = 13; session <= 36; session++)
        {
            var next = Palette.Allocate(inUse);
            allocated.Add(next);
            inUse.Add(next.Accent);
        }
        // Sessions 13 to 24 take each series color once more in order, 25 to 36 a third time.
        Assert.Equal(Palette.Series.Concat(Palette.Series), allocated);
        // The fourteenth session after thirteen live sessions (the full series plus Series[0]) gets Series[1].
        Assert.Equal(Palette.Series[1], Palette.Allocate([.. Palette.Series.Select(entry => entry.Accent), Palette.Series[0].Accent]));
    }

    [Fact]
    public void AnExplicitAccentBlocksTheSeriesEntryItIsNearAndCountsByDistance()
    {
        var near = Palette.Series[0].Accent;
        var custom = near[..5] + (Convert.ToInt32(near[5..], 16) + 1).ToString("x2");
        Assert.True(Palette.Distance(near, custom) < Palette.SameColorDistance);
        Assert.Equal(Palette.Series[1], Palette.Allocate([custom]));
    }
}
