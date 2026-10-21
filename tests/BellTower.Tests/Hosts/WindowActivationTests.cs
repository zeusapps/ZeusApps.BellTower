using BellTower.Hosts;
using BellTower.Tray;
using Xunit;

namespace BellTower.Tests.Hosts;

public sealed class WindowActivationTests
{
    private sealed class RecordingWindows(bool minimized, bool foregroundAllowed = true) : IWindowActivationApi
    {
        public List<(string Call, IntPtr Window)> Calls { get; } = [];
        public bool IsMinimized(IntPtr window)
        {
            Calls.Add(("is-minimized", window));
            return minimized;
        }

        public void Restore(IntPtr window) => Calls.Add(("restore", window));
        public bool SetForeground(IntPtr window)
        {
            Calls.Add(("foreground", window));
            return foregroundAllowed;
        }
    }

    [Fact]
    public void VisibleWindowsAreFocusedWithoutAnyRestoreRequest()
    {
        var windows = new RecordingWindows(minimized: false);
        var handle = new IntPtr(1234);
        Assert.True(new WindowActivator(windows).Activate(handle));
        Assert.Equal(new[] { ("is-minimized", handle), ("foreground", handle) }, windows.Calls);
    }

    [Fact]
    public void MinimizedWindowIsRestoredBeforeForegroundActivation()
    {
        var windows = new RecordingWindows(minimized: true);
        var handle = new IntPtr(5678);
        Assert.True(new WindowActivator(windows).Activate(handle));
        Assert.Equal(new[] { ("is-minimized", handle), ("restore", handle), ("foreground", handle) }, windows.Calls);
    }

    [Fact]
    public void DeniedActivationIsReportedWithoutChangingVisibleWindowState()
    {
        var windows = new RecordingWindows(minimized: false, foregroundAllowed: false);
        Assert.False(new WindowActivator(windows).Activate(new IntPtr(1234)));
        Assert.DoesNotContain(windows.Calls, call => call.Call == "restore");
    }

    [Theory]
    [InlineData("window only", "window only; select the session's tab or pane.")]
    [InlineData("exact terminal", "focus this terminal.")]
    [InlineData("unavailable", "navigation is unavailable.")]
    public void ChipExplainsItsActualNavigationCapability(string precision, string expected)
    {
        var chip = new ChipView("codex:one", "episode", "Session name", new("#101c27", "#4b91bb", "#000000"),
            "finished", "00:01:23", "branch: feature/test", precision);
        Assert.Contains(expected, chip.ToolTip);
        Assert.Contains("Session name", chip.ToolTip);
        Assert.Contains("branch: feature/test", chip.ToolTip);
        Assert.Contains("Right-click to dismiss", chip.ToolTip);
    }
}
