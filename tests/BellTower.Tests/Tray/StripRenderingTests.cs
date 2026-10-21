using System.Runtime.ExceptionServices;
using BellTower.Tray;
using Xunit;

namespace BellTower.Tests.Tray;

public sealed class StripRenderingTests
{
    private static ChipView Chip(string key = "codex:one") => new(key, "episode", "A long session name with É and Ω",
        new("#101c27", "#4b91bb", "#000000"), "needs permission", "00:03:41", "branch: feature/test");

    [Fact]
    public void TimerAndMetadataRefreshKeepExistingControlsAndTheirHandles()
    {
        InSta(() =>
        {
            // The form is transparent and nonactivating; no visible test strip or tray is created.
            using var strip = new StripForm { Opacity = 0 };
            var first = Chip();
            strip.UpdateChips([first, Chip("claude:two")]);
            var panel = strip.Controls.OfType<FlowLayoutPanel>().Single();
            var control = panel.Controls.OfType<ChipControl>().Single(chip => chip.Key == first.Key);
            var handle = control.Handle;
            var originalSize = strip.Size;
            for (var second = 0; second < 30; second++)
            {
                strip.UpdateChips([first with { Elapsed = $"00:04:{second:00}" }, Chip("claude:two")]);
                Assert.Same(control, panel.Controls.OfType<ChipControl>().Single(chip => chip.Key == first.Key));
                Assert.Equal(handle, control.Handle);
                Assert.Equal(originalSize, strip.Size);
            }
            var timerSize = control.Size;
            strip.UpdateChips([first with { Elapsed = "" }, Chip("claude:two")]);
            Assert.Equal(handle, control.Handle);
            Assert.Equal(timerSize.Height, control.Height);
            Assert.True(control.Width < timerSize.Width);
            strip.UpdateChips([first, Chip("claude:two")]);
            Assert.Equal(timerSize, control.Size);
            strip.UpdateChips([first with { Name = "Renamed", Reason = "has a question" }]);
            Assert.Same(control, panel.Controls.OfType<ChipControl>().Single());
            Assert.Equal("Renamed", control.Text);
            Assert.Contains("has a question", control.AccessibleName);
            Assert.Equal(panel.PreferredSize, strip.ClientSize);
            strip.UpdateChips([]);
            Assert.False(strip.Visible);
            Assert.Empty(panel.Controls.OfType<ChipControl>());
            Assert.True(control.IsDisposed);
        });
    }

    [Theory]
    [InlineData(96, 10f)]
    [InlineData(120, 10f)]
    [InlineData(144, 10f)]
    [InlineData(192, 10f)]
    [InlineData(144, 18f)]
    public void FontAndDpiLayoutFitsFullTextHeightAndTimer(int dpi, float points)
    {
        InSta(() =>
        {
            using var image = new Bitmap(1200, 400);
            image.SetResolution(dpi, dpi);
            using var graphics = Graphics.FromImage(image);
            using var font = new Font("Segoe UI", points);
            const string elapsed = "123:45:56";
            var layout = ChipLayout.Measure(graphics, font, dpi, elapsed);
            var textSize = TextRenderer.MeasureText(graphics, elapsed, font,
                new Size(int.MaxValue, int.MaxValue), ChipLayout.TextFlags);
            Assert.True(layout.Name.Height >= font.Height);
            Assert.True(layout.Reason.Height >= textSize.Height);
            Assert.True(layout.Elapsed.Width >= textSize.Width);
            Assert.True(layout.Reason.Right < layout.Elapsed.Left);
            Assert.Equal(layout.Name.Top, layout.Reason.Top);
            Assert.Equal(layout.Name.Top, layout.Elapsed.Top);
            Assert.True(layout.Name.Right <= layout.NameSeparator.Left);
            Assert.True(layout.NameSeparator.Right <= layout.Reason.Left);
            Assert.True(layout.Reason.Right <= layout.TimeSeparator.Left);
            Assert.True(layout.TimeSeparator.Right <= layout.Elapsed.Left);
            foreach (var bounds in new[] { layout.Name, layout.Reason, layout.Elapsed, layout.NameSeparator, layout.TimeSeparator })
            {
                Assert.True(new Rectangle(Point.Empty, layout.Size).Contains(bounds));
            }
        });
    }

    [Fact]
    public void IncreasingTheInheritedFontRefitsTheChipAndItsStrip()
    {
        InSta(() =>
        {
            using var strip = new StripForm { Opacity = 0 };
            strip.UpdateChips([Chip()]);
            var panel = strip.Controls.OfType<FlowLayoutPanel>().Single();
            var control = panel.Controls.OfType<ChipControl>().Single();
            var oldHeight = control.Height;
            using var larger = new Font("Segoe UI", 18);
            strip.Font = larger;
            Assert.True(control.Height > oldHeight);
            Assert.Equal(panel.PreferredSize, strip.ClientSize);
            Assert.True(strip.ClientRectangle.Contains(panel.Bounds));
            Assert.True(panel.ClientRectangle.Contains(control.Bounds));
        });
    }

    [Theory]
    [InlineData(96)]
    [InlineData(144)]
    [InlineData(192)]
    public void GitDetailsStayInTheTooltipWithoutAddingChipHeight(int dpi)
    {
        InSta(() =>
        {
            using var image = new Bitmap(1400, 600);
            image.SetResolution(dpi, dpi);
            using var graphics = Graphics.FromImage(image);
            using var font = new Font("Segoe UI", 10);
            var chip = Chip();
            var layout = ChipLayout.Measure(graphics, font, dpi, chip.Elapsed, chip.Name, chip.Reason);
            Assert.Contains(chip.Details, chip.ToolTip);
            Assert.Equal(layout.Name.Height + (int)Math.Round(5 * dpi / 96d) * 2, layout.Size.Height);
            Assert.Equal(layout.Name.Top, layout.Reason.Top);
            Assert.True(new Rectangle(Point.Empty, layout.Size).Contains(layout.Elapsed));
        });
    }

    private static void InSta(Action action)
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                action();
            }
            catch (Exception error)
            {
                failure = error;
            }
        })
        { IsBackground = true };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(15)), "Rendering check timed out.");
        if (failure is not null)
        {
            ExceptionDispatchInfo.Capture(failure).Throw();
        }
    }
}
