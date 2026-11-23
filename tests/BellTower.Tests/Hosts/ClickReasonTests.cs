using BellTower.Core;
using BellTower.Hosts;
using BellTower.IO;
using BellTower.Providers;
using BellTower.Tests.Testing;
using BellTower.Tray;
using Xunit;

namespace BellTower.Tests.Hosts;

/// <summary>
/// Trial 6.1 findings T-4, T-6 and T-7 (DEV-058): a click always produces focus or a stated reason, for both providers and both
/// hosts, and the Sessions window opens in front of the terminal in use.
/// </summary>
public sealed class ClickReasonTests
{
    private static ProcessIdentity Self => ProcessProbe.Identify(Environment.ProcessId)!;
    private static ProcessIdentity Dead => new(int.MaxValue, 1);

    private sealed class ThrowingHost(string kind) : ITerminalNavigator
    {
        public string HostKind => kind;
        public string Precision(Attachment attachment) => throw new InvalidOperationException("A shared conversation must not reach a host.");
        public Task<FocusResult> FocusAsync(Attachment attachment) => throw new InvalidOperationException("A shared conversation must not reach a host.");
    }

    private static Attachment Captured(string provider, string id, ProcessIdentity? agent, string hostKind = "windows-terminal") =>
        new(id, provider, agent, new(hostKind, Self, Self, 1234, "terminal-session") { ShellSource = "ancestry" }, "D:/example", DateTimeOffset.UtcNow)
        { Ownership = "observed" };

    [Theory]
    [InlineData("windows-terminal")]
    [InlineData("vscode")]
    public async Task ASharedBackendClickNamesTheLimitationAndBothSupportedRoutesOnEveryHost(string hostKind)
    {
        var shared = NativeAdoption.Shared(Captured("codex", "a", null, hostKind), "session-1", AgentModes.SharedBackend);
        // Even a record that carries a host kind never reaches the host navigators.
        var navigator = new Navigator([new ThrowingHost("windows-terminal"), new ThrowingHost("vscode")]);
        var result = await navigator.FocusAsync(shared with { Host = shared.Host with { Kind = hostKind } });
        Assert.False(result.Success);
        Assert.Equal("unavailable", result.Precision);
        Assert.Equal(NavigationReasons.SharedBackend, result.Message);
        Assert.Contains("Codex profile", result.Message, StringComparison.Ordinal);
        Assert.Contains("codex --no-daemon", result.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(NavigationReasons.ProcessUnverified, result.Message, StringComparison.Ordinal);
        // The balloon (or the in-app notice) carries it: a refusal always needs a visible message.
        Assert.True(NavigationOutcome.NeedsNotice(result));
    }

    [Fact]
    public async Task ASharedConversationWithAnUnrecognizedModeSaysNoTerminalWasVerified()
    {
        var unknown = NativeAdoption.Shared(Captured("codex", "a", null), "session-1", AgentModes.Unknown);
        var result = await new Navigator([new ThrowingHost("windows-terminal")]).FocusAsync(unknown);
        Assert.False(result.Success);
        Assert.Equal(NavigationReasons.TerminalUnverified, result.Message);
    }

    [Theory]
    [InlineData("claude")]
    [InlineData("codex")]
    public async Task ADirectSessionWhoseProcessIsGoneKeepsTheProcessReasonForBothProviders(string provider)
    {
        var result = await new Navigator([new ThrowingHost("windows-terminal")]).FocusAsync(Captured(provider, "a", Dead));
        Assert.False(result.Success);
        Assert.Equal(NavigationReasons.ProcessUnverified, result.Message);
        var missing = await new Navigator([new ThrowingHost("windows-terminal")]).FocusAsync(null);
        Assert.Equal(NavigationReasons.ProcessUnverified, missing.Message);
    }

    [Fact]
    public async Task MixedSimultaneousSessionsEachGetTheirOwnReason()
    {
        using var workspace = new TestWorkspace();
        var attachments = new AttachmentStore(workspace.Paths);
        var shared = NativeAdoption.Shared(Captured("codex", "shared", null), "codex-session", AgentModes.SharedBackend);
        var direct = NativeAdoption.Apply(Captured("claude", "claude-direct", Self), "claude-session", AgentModes.InteractiveClient,
            AgentModeClassifier.MayShareBackend("claude")).Attachment;
        attachments.Save(shared);
        attachments.Save(direct);
        var navigator = new Navigator([new ThrowingHost("windows-terminal")]);
        Assert.Equal(NavigationReasons.SharedBackend, (await navigator.FocusAsync(attachments.Get(shared.Id))).Message);
        // The direct Claude session is not told it is shared: it reaches its host, which refuses only because the stand-in cannot focus.
        await Assert.ThrowsAsync<InvalidOperationException>(() => navigator.FocusAsync(attachments.Get(direct.Id)));
    }

    // T-6: with the native-presentation setting off, an adopted direct session in Windows Terminal is focused at the window only,
    // and the result states why the tab was not selected.
    private sealed class Console : ISessionConsole
    {
        public string Title = "shell";
        public string ReadTitle() => Title;
        public bool SetTitle(string title) { Title = title; return true; }
        public bool Write(string text) => true;
        public void Dispose() { }
    }

    private sealed class NoTabs : ITerminalTabSource
    {
        public IReadOnlyList<ITerminalTab> ReadTabs(long window) => [];
    }

    private sealed class Windows : IWindowActivationApi
    {
        public bool IsMinimized(IntPtr window) => false;
        public void Restore(IntPtr window) { }
        public bool SetForeground(IntPtr window) => true;
        public bool IsForeground(IntPtr window) => true;
    }

    public static IEnumerable<object[]> DirectClients() =>
    [
        ["claude", AgentModes.InteractiveClient],
        ["codex", AgentModes.EmbeddedRuntime]
    ];

    [Theory]
    [MemberData(nameof(DirectClients))]
    public void WindowOnlyFocusWithTheSettingOffExplainsWhyTheTabWasNotSelected(string provider, string mode)
    {
        using var workspace = new TestWorkspace();
        new ConfigStore(workspace.Paths).Update(config => config.NativePresentation = false);
        var adopted = NativeAdoption.Apply(Captured(provider, "a", Self), "session-1", mode, AgentModeClassifier.MayShareBackend(provider)).Attachment;
        new AttachmentStore(workspace.Paths).Save(adopted);
        var result = new WindowsTerminalFocusService(workspace.Paths, new(new NoTabs()), new(new Windows()), (_, _) => new Console(), _ => true).Focus("a");
        Assert.True(result.Success);
        Assert.Equal("window only", result.Precision);
        Assert.Equal(NavigationReasons.WindowOnlyPresentationOff, result.Message);
        Assert.True(NavigationOutcome.NeedsNotice(result));

        // With the setting on the tab is looked up by its managed title, and a miss keeps the generic reason.
        new ConfigStore(workspace.Paths).Update(config => config.NativePresentation = true);
        var on = new WindowsTerminalFocusService(workspace.Paths, new(new NoTabs()), new(new Windows()), (_, _) => new Console(), _ => true).Focus("a");
        Assert.True(on.Success);
        Assert.NotEqual(NavigationReasons.WindowOnlyPresentationOff, on.Message);
    }

    [Fact]
    public void ALaunchThatOptedOutOfTheManagedTitleIsNotToldToTurnTheSettingOn()
    {
        using var workspace = new TestWorkspace();
        new ConfigStore(workspace.Paths).Update(config => config.NativePresentation = false);
        // A managed launch keeps its own explicit control; the tray setting is not what stops its tab selection.
        var managed = Captured("codex", "a", Self) with { Ownership = "managed", ManagedTitle = false };
        new AttachmentStore(workspace.Paths).Save(managed);
        var result = new WindowsTerminalFocusService(workspace.Paths, new(new NoTabs()), new(new Windows()), (_, _) => new Console(), _ => true).Focus("a");
        Assert.Equal("window only", result.Precision);
        Assert.NotEqual(NavigationReasons.WindowOnlyPresentationOff, result.Message);
    }

    // T-4 and T-6 delivery: a stated reason must be visible even when Windows will not show a balloon.
    public static IEnumerable<object[]> Results() =>
    [
        [new FocusResult(false, "unavailable", NavigationReasons.SharedBackend)],
        [new FocusResult(true, "window only", NavigationReasons.WindowOnlyPresentationOff)],
        [new FocusResult(true, "terminal only", "revealed")],
        [new FocusResult(false, "window only", "Windows declined foreground activation.")]
    ];

    [Theory]
    [MemberData(nameof(Results))]
    public void EveryReasonIsShownInTheOwnNoticeWhateverWindowsNotificationSettingsAre(FocusResult result) =>
        Assert.True(NavigationNotice.ShowsNotice(result));

    [Theory]
    [InlineData("exact tab")]
    [InlineData("exact terminal")]
    public void AnExactDestinationNeedsNoNotice(string precision) =>
        Assert.False(NavigationNotice.ShowsNotice(new FocusResult(true, precision, "ok")));

    [Theory]
    [InlineData(true, "exact tab", "navigation-ok-exact-tab")]
    [InlineData(true, "window only", "navigation-ok-window-only")]
    [InlineData(false, "unavailable", "navigation-refused-unavailable")]
    [InlineData(false, "terminal only", "navigation-refused-terminal-only")]
    [InlineData(false, "C:/Users/someone/secret path", "navigation-refused-other")]
    [InlineData(true, "", "navigation-ok-other")]
    public void TheLogCodeNamesOnlyAKnownPrecision(bool success, string precision, string expected)
    {
        var code = NavigationNotice.LogCode(new FocusResult(success, precision, "message that must not be logged"));
        Assert.Equal(expected, code);
        Assert.Matches("^navigation-(ok|refused)-[a-z-]+$", code);
    }

    [Fact]
    public void ANullFieldFromAReceiptIsNormalizedBeforeAnyConsumerReadsIt()
    {
        var raw = new FocusResult(false, null!, null!);
        var normalized = raw.Normalized();
        Assert.Equal("unavailable", normalized.Precision);
        Assert.False(string.IsNullOrWhiteSpace(normalized.Message));
        Assert.True(NavigationNotice.ShowsNotice(normalized));
        Assert.Equal("navigation-refused-unavailable", NavigationNotice.LogCode(normalized));
        Assert.False(string.IsNullOrWhiteSpace(new FocusResult(true, "window only", " ").Normalized().Message));
    }

    [Theory]
    [InlineData("claude", "windows-terminal")]
    [InlineData("claude", "vscode")]
    [InlineData("codex", "windows-terminal")]
    [InlineData("codex", "vscode")]
    public async Task ANavigatorThatThrowsIsLoggedAndExplainedForEveryProviderAndHost(string provider, string host)
    {
        _ = (provider, host);
        var (result, code) = await NavigationNotice.RunAsync(() => throw new IOException("C:/private/path"));
        Assert.False(result.Success);
        Assert.Equal(NavigationNotice.FailedMessage, result.Message);
        Assert.Equal(NavigationNotice.FailedLog, code);
        Assert.DoesNotContain("private", result.Message, StringComparison.Ordinal);
        Assert.True(NavigationNotice.ShowsNotice(result));
        var (normal, normalCode) = await NavigationNotice.RunAsync(() => Task.FromResult(new FocusResult(true, "exact tab", "ok")));
        Assert.True(normal.Success);
        Assert.Equal("navigation-ok-exact-tab", normalCode);
    }

    [Fact]
    public void AStaleSessionKeyIsLoggedAndExplained()
    {
        var (result, code) = NavigationNotice.Missing();
        Assert.False(result.Success);
        Assert.Equal(NavigationNotice.SessionMissingLog, code);
        Assert.True(NavigationNotice.ShowsNotice(result));
        Assert.False(string.IsNullOrWhiteSpace(result.Message));
    }

    [Fact]
    public void TheNoticeIsTopmostFromCreationNeverActivatesAndDismisses()
    {
        using var notice = new NoticeForm();
        Assert.True(notice.NeverActivates);
        var style = notice.ExtendedStyle;
        Assert.NotEqual(0, style & NoticeForm.WsExTopmost);
        Assert.NotEqual(0, style & NoticeForm.WsExNoActivate);
        Assert.NotEqual(0, style & NoticeForm.WsExToolWindow);
        // The TopMost property is what can activate the form while it is shown; the style carries topmost instead.
        Assert.False(notice.TopMost);
        Assert.Equal(TimeSpan.FromSeconds(8), NoticeForm.Lifetime);
        notice.Dismiss();
        Assert.False(notice.Visible);
        Assert.False(notice.Expiring);
    }

    [Fact]
    public void TheNoticeSitsBelowTheStripOrAboveItWhenThereIsNoRoomAndStaysOnTheScreen()
    {
        var area = new Rectangle(0, 0, 1920, 1040);
        var size = new Size(300, 80);
        Assert.Equal(new Point(100, 156), NoticeForm.Place(new Rectangle(100, 100, 400, 50), size, area));
        Assert.Equal(new Point(100, 914), NoticeForm.Place(new Rectangle(100, 1000, 400, 40), size, area));
        Assert.Equal(1620, NoticeForm.Place(new Rectangle(1900, 100, 400, 50), size, area).X);
    }

    // T-7: the Sessions window is raised in front of the terminal the user is working in.
    private sealed class Foreground(params bool[] foregroundAfterRequest) : IWindowActivationApi
    {
        private int requests;
        public int Requests => requests;
        public bool IsMinimized(IntPtr window) => false;
        public void Restore(IntPtr window) { }
        public bool SetForeground(IntPtr window) { requests++; return false; }
        public bool IsForeground(IntPtr window) => foregroundAfterRequest.Length != 0 && foregroundAfterRequest[Math.Min(requests, foregroundAfterRequest.Length) - 1];
    }

    [Fact]
    public void RaisingAnAcceptedWindowAsksOnceAndDoesNotPulse()
    {
        var api = new Foreground(true);
        var pulses = 0;
        Assert.True(new WindowActivator(api).Raise(new IntPtr(7), () => pulses++));
        Assert.Equal(1, api.Requests);
        Assert.Equal(0, pulses);
    }

    [Fact]
    public void ARefusedForegroundRequestLiftsTheWindowAndAsksAgain()
    {
        var api = new Foreground(false, true);
        var pulses = 0;
        Assert.True(new WindowActivator(api).Raise(new IntPtr(7), () => pulses++));
        Assert.Equal(2, api.Requests);
        Assert.Equal(1, pulses);
    }

    [Fact]
    public void AWindowWindowsNeverMakesForegroundIsStillLiftedOnceAndReportedHonestly()
    {
        var api = new Foreground(false, false);
        var pulses = 0;
        Assert.False(new WindowActivator(api).Raise(new IntPtr(7), () => pulses++));
        Assert.Equal(2, api.Requests);
        Assert.Equal(1, pulses);
    }
}
