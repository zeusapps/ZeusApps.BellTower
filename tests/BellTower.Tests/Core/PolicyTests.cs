using BellTower.Core;
using BellTower.Tests.Testing;
using BellTower.Tray;
using Xunit;

namespace BellTower.Tests.Core;

public sealed class PolicyTests
{
    private static SessionState Waiting(DateTimeOffset since)
    {
        var state = new AppState();
        SessionReducer.Apply(state, TestWorkspace.Event(EventKind.Permission, since));
        return state.Sessions.Values.Single();
    }

    [Fact]
    public void RestoredWaitIsSilentButVisible()
    {
        var now = DateTimeOffset.UtcNow;
        var s = Waiting(now.AddMinutes(-1));
        var policy = new AlertPolicy(now);
        Assert.True(s.RequiresAttention(now));
        Assert.Empty(policy.Collect([s], now, false));
    }

    [Fact]
    public void NewWaitAnnouncesOnlyOnce()
    {
        var now = DateTimeOffset.UtcNow;
        var policy = new AlertPolicy(now);
        var s = Waiting(now.AddSeconds(1));
        Assert.Single(policy.Collect([s], now.AddSeconds(2), false));
        Assert.Empty(policy.Collect([s], now.AddSeconds(3), false));
    }

    [Fact]
    public void MutedWaitIsNotReplayedAfterUnmute()
    {
        var now = DateTimeOffset.UtcNow;
        var s = Waiting(now.AddSeconds(1));
        var policy = new AlertPolicy(now);
        Assert.Empty(policy.Collect([s], now.AddSeconds(2), true));
        Assert.Empty(policy.Collect([s], now.AddSeconds(3), false));
    }

    [Fact]
    public void VoiceAggregationAndSubjectLimit()
    {
        var items = Enumerable.Range(0, 3).Select(i => new AudioItem(i.ToString(), "episode", "phrase")).ToArray();
        Assert.Equal("3 sessions waiting", AudioQueue.SelectPhrase(items, true));
        Assert.Null(AudioQueue.SelectPhrase(items, false));
        var identity = new SessionIdentity
        {
            Key = "x",
            ManualName = "one two three four five six",
            Role = "PM",
            Palette = Palette.Create()
        };
        Assert.Equal("PM, one two three four, needs permission", AlertPolicy.Phrase(Waiting(DateTimeOffset.UtcNow), identity));
    }

    [Fact]
    public void ChimeHasExpectedWaveFormatWithoutPlayback()
    {
        var wave = Chime.Wave();
        Assert.Equal("RIFF", System.Text.Encoding.ASCII.GetString(wave, 0, 4));
        Assert.Equal(880, Chime.Frequency);
        Assert.Equal(400, Chime.Milliseconds);
        Assert.Equal(17684, wave.Length);
    }

    [Fact]
    public void PaletteAlwaysProvidesReadableChipText()
    {
        for (var i = 0; i < 500; i++)
        {
            var palette = Palette.Create();
            Assert.True(Palette.Contrast(palette.Accent, palette.Foreground) >= 4.5);
            Assert.True(Palette.IsHex(palette.Background));
        }
    }

    [Fact]
    public void ElapsedTimeSupportsMoreThanTwentyFourHours()
    {
        var start = DateTimeOffset.UtcNow;
        Assert.Equal("27:02:03", AlertPolicy.Elapsed(start, start.AddHours(27).AddMinutes(2).AddSeconds(3)));
    }

    [Fact]
    public void PositionIsClampedIntoResizedDisplay()
    {
        var point = StripForm.ClampPosition(new(4000, 2000), new(500, 50), new(0, 0, 1000, 700));
        Assert.Equal(new System.Drawing.Point(500, 650), point);
    }
}
